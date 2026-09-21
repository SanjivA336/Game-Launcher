using Game_Launcher.Helpers;
using Game_Launcher.Models;
using Game_Launcher.Services;
using Game_Launcher.Services.Steam;
using Game_Launcher.ViewModels.Controls;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;

namespace Game_Launcher.ViewModels.Pages {
    internal class GameOptionsVM : BaseVM {

        private GameMapping _game = null!;
        public GameMapping Game {
            get => _game;
            set {
                if (_game != value) {
                    _game = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsInstalled));
                    OnPropertyChanged(nameof(IsHidden));
                    OnPropertyChanged(nameof(CoverBrush));
                    OnPropertyChanged(nameof(HasCover));
                    OnPropertyChanged(nameof(IsCustomCover));
                    OnPropertyChanged(nameof(CoverSourceText));
                    OnPropertyChanged(nameof(PlayStatsText));
                    RebuildTagGroups();
                    RefreshExecutables();
                }
            }
        }

        public bool IsInstalled => Game.HasTag("Installed");

        /// <summary> "Never played", or "Last played 3 hours ago · 5 launches". </summary>
        public string PlayStatsText {
            get {
                if (Game.LastPlayed is not DateTime lastPlayed) {
                    return "Never played";
                }

                string launches = Game.LaunchCount == 1 ? "1 launch" : $"{Game.LaunchCount} launches";
                return $"Last played {RelativeTime.Format(lastPlayed)} · {launches}";
            }
        }

        private IReadOnlyList<TagGroupVM> _tagGroups = [];
        /// <summary> The tags the user can switch on for this game, grouped (Players, Connection, Features). </summary>
        public IReadOnlyList<TagGroupVM> TagGroups {
            get => _tagGroups;
            private set {
                _tagGroups = value;
                OnPropertyChanged();
            }
        }

        // Rebuilt whenever the game object is swapped (Cancel/Reset) so the chips always match the game's real tags
        private void RebuildTagGroups() {
            TagGroups = TagCatalog.AssignableTags
                .GroupBy(t => t.Group)
                .Select(g => new TagGroupVM(g.Key, g.Select(t => new TagChipVM(t, Game.HasTag(t.Name), OnTagChipChanged)).ToList()))
                .ToList();
            UpdateGroupVisibility();
        }

        private void OnTagChipChanged(TagChipVM chip) {
            if (chip.IsSelected) {
                Game.AddTag(chip.Name);

                // In a pick-one group (Completion), turning one tag on turns the other off
                if (TagCatalog.IsSingleChoice(chip.Definition.Group)) {
                    foreach (var other in TagGroups.SelectMany(g => g.Tags).Where(t => t != chip && t.Definition.Group == chip.Definition.Group && t.IsSelected).ToList()) {
                        other.SetSelectedSilently(false);
                        Game.RemoveTag(other.Name);
                    }
                }
            }
            else {
                Game.RemoveTag(chip.Name);
            }
            UpdateGroupVisibility();
        }

        // Completion only makes sense for a game with a story, so it shows once the game is a Campaign (or already has a completion tag)
        private void UpdateGroupVisibility() {
            bool campaign = Game.HasTag(TagCatalog.Campaign);
            foreach (var group in TagGroups) {
                group.IsVisible = group.Name != TagCatalog.CompletionGroup || campaign || group.Tags.Any(t => t.IsSelected);
            }
        }

        #region Clean name / suggested tags
        private readonly Func<string, string, CancellationToken, Task<TagSuggestion>> _suggest;

        private string _nameMessage = string.Empty;
        /// <summary> Result of the last "Clean name" press (only says something when there was nothing to clean). </summary>
        public string NameMessage {
            get => _nameMessage;
            private set { _nameMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNameMessage)); }
        }
        public bool HasNameMessage => _nameMessage.Length > 0;

        private string _tagsMessage = string.Empty;
        /// <summary> Result of the last "Add suggested tags" press. </summary>
        public string TagsMessage {
            get => _tagsMessage;
            private set { _tagsMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTagsMessage)); }
        }
        public bool HasTagsMessage => _tagsMessage.Length > 0;

        private bool _isSuggesting;
        public bool IsSuggesting {
            get => _isSuggesting;
            private set { _isSuggesting = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSuggest)); }
        }
        public bool CanSuggest => !_isSuggesting;

        public ICommand CleanNameCommand { get; }
        public ICommand SuggestTagsCommand { get; }

        /// <summary> Puts the cleaned-up version of the name in the Name box (a change like any other: Save keeps it, Cancel drops it). </summary>
        public void CleanName() {
            string cleaned = NameCleaner.Clean(Game.Name);
            if (cleaned == Game.Name) {
                NameMessage = "Already clean.";
                return;
            }

            NameMessage = string.Empty;
            Game.Name = cleaned;
        }

        /// <summary> Looks the game up on Steam and turns on the tags Steam suggests. It only adds tags; yours stay, and you can edit the result. </summary>
        public async Task SuggestTagsAsync() {
            IsSuggesting = true;
            TagsMessage = "Looking on Steam…";
            try {
                var result = await _suggest(Game.Name, Game.DirPathRaw, CancellationToken.None);
                switch (result.Outcome) {
                    case SuggestOutcome.NotFound:
                        TagsMessage = "Not found on Steam.";
                        break;
                    case SuggestOutcome.Failed:
                        TagsMessage = "Couldn't reach Steam.";
                        break;
                    default:
                        var added = result.Tags.Where(t => !Game.HasTag(t)).ToList();
                        foreach (string tag in added) Game.AddTag(tag);
                        RebuildTagGroups(); // the chips show what was added
                        TagsMessage = added.Count == 0 ? "No new tags." : $"Added {string.Join(", ", added)}.";
                        break;
                }
            }
            finally {
                IsSuggesting = false;
            }
        }
        #endregion

        public Brush CoverBrush => CoverArt.CreateCoverBrush(Game.DirPathRaw, Game.CoverPath, out _);
        public bool HasCover => !string.IsNullOrEmpty(Game.CoverPath) && File.Exists(CoverArt.FullPath(Game.CoverPath));

        /// <summary> True when the user picked this cover (their own image, or one chosen on SteamGridDB). </summary>
        public bool IsCustomCover => Game.CoverIsCustom;

        /// <summary> Where the cover came from, so a wrong automatic match is easy to spot. Empty when there is no cover. </summary>
        public string CoverSourceText {
            get {
                if (!HasCover) {
                    return string.Empty;
                }

                bool hasTitle = !string.IsNullOrEmpty(Game.CoverMatchedName);
                if (Game.CoverIsCustom) {
                    return hasTitle ? $"Chosen from SteamGridDB: {Game.CoverMatchedName}" : "Your own image";
                }

                if (Game.CoverSource == "Steam") {
                    return hasTitle ? $"Cover from Steam: {Game.CoverMatchedName}" : "Cover from Steam";
                }

                return hasTitle ? $"Cover from SteamGridDB: {Game.CoverMatchedName}" : string.Empty;
            }
        }

        /// <summary> Raised when the user wants to change the cover; the page opens the picker window (a ViewModel doesn't open windows itself). </summary>
        public event EventHandler? ChangeCoverRequested;

        public ICommand ChangeCoverCommand { get; }
        public ICommand UseAutomaticCoverCommand { get; }

        /// <summary> Re-reads the cover details from disk. Covers are saved by the picker and the downloader on their own, not by Save. </summary>
        public void RefreshCoverFromStore() {
            var stored = GameMappingManager.GetMapping(Game.DirPathRaw, out _);
            if (stored is not null) {
                Game.CoverPath = stored.CoverPath;
                Game.CoverMatchedName = stored.CoverMatchedName;
                Game.CoverIsCustom = stored.CoverIsCustom;
                Game.CoverLookupPending = stored.CoverLookupPending;
            }

            OnPropertyChanged(nameof(CoverBrush));
            OnPropertyChanged(nameof(HasCover));
            OnPropertyChanged(nameof(IsCustomCover));
            OnPropertyChanged(nameof(CoverSourceText));
        }

        // Gives up a custom cover: its file is deleted and the game is looked up on SteamGridDB again (that happens when you return to the library)
        private void UseAutomaticCover() {
            GameMappingManager.UseAutomaticCover(Game.DirPathRaw);
            RefreshCoverFromStore();
        }
        public bool IsHidden {
            get => Game.HasTag("Hidden");
            set {
                if (value && !Game.HasTag("Hidden")) {
                    Game.AddTag("Hidden");
                    OnPropertyChanged(nameof(IsHidden));
                }
                else if (!value && Game.HasTag("Hidden")) {
                    Game.RemoveTag("Hidden");
                    OnPropertyChanged(nameof(IsHidden));
                }
            }
        }

        // Cached on purpose: WPF's ListBox matches SelectedItem to its items by reference, and
        // Game.Executables builds brand-new FileInfo objects on every read, so they'd never match.
        private ObservableCollection<FileInfo> _executables = new();
        public ObservableCollection<FileInfo> Executables => _executables;

        public FileInfo? SelectedExecutable {
            get => Game.PrimaryExecutableIndex >= 0 && Game.PrimaryExecutableIndex < _executables.Count
                ? _executables[Game.PrimaryExecutableIndex]
                : null;
            set {
                int index = value == null ? -1 : _executables.IndexOf(value);
                if (index >= 0) {
                    Game.PrimaryExecutableIndex = index;
                    OnPropertyChanged();
                }
            }
        }

        private void RefreshExecutables() {
            _executables = new ObservableCollection<FileInfo>(Game.Executables);
            OnPropertyChanged(nameof(Executables));
            OnPropertyChanged(nameof(SelectedExecutable));
        }

        public ICommand OpenFolderCommand { get; }
        public ICommand ChangeFolderCommand { get; }

        private readonly Func<string, string?> _pickFolder;

        private string _folderMessage = string.Empty;
        /// <summary> Result of the last "Change folder" attempt (success note or the reason it was refused). </summary>
        public string FolderMessage {
            get => _folderMessage;
            private set { _folderMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFolderMessage)); }
        }
        public bool HasFolderMessage => _folderMessage.Length > 0;

        private bool _folderMessageIsError;
        public bool FolderMessageIsError {
            get => _folderMessageIsError;
            private set { _folderMessageIsError = value; OnPropertyChanged(); }
        }

        private void ChangeFolder() {
            string? picked = _pickFolder("Choose the folder the game is in now");
            if (picked is not null) {
                RepointTo(picked);
            }
        }

        /// <summary>
        /// Points this game at a different folder right away (like cover changes, this saves immediately because the game's identity
        /// is its folder). Name and tag edits still waiting for Save stay in place and are saved under the new folder.
        /// </summary>
        public bool RepointTo(string folder) {
            if (!GameMappingManager.Repoint(Game.DirPathRaw, folder, out var updated, out string? error) || updated is null) {
                FolderMessageIsError = true;
                FolderMessage = error ?? "Couldn't change the folder.";
                return false;
            }

            // Bring the copy being edited along, without disturbing the edits the user hasn't saved yet
            Game.DirPathRaw = updated.DirPathRaw;
            Game.ExecutablesRaw = updated.ExecutablesRaw.ToList();
            Game.PrimaryExecutableIndex = updated.PrimaryExecutableIndex;
            Game.AddTag(TagCatalog.Installed);

            RefreshExecutables();
            RefreshCoverFromStore();
            OnPropertyChanged(nameof(IsInstalled));

            FolderMessageIsError = false;
            FolderMessage = "Folder changed. The name, tags, play history and cover were kept.";
            return true;
        }

        public ICommand AddExecutableCommand { get; }
        public ICommand RemoveExecutableCommand { get; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetCommand { get; }

        /// <summary> Raised when the user is done with this page (saved or cancelled); the page decides where to go next. </summary>
        public event EventHandler? CloseRequested;


        /// <param name="suggest"> Looks a game (name, folder) up on Steam. Replaceable in tests.</param>
        public GameOptionsVM(GameMapping game, Func<string, string?>? pickFolder = null, Func<string, string, CancellationToken, Task<TagSuggestion>>? suggest = null) {
            _pickFolder = pickFolder ?? FolderPicker.Pick;
            _suggest = suggest ?? SteamTagSuggester.Shared.SuggestAsync;
            CleanNameCommand = new RelayCommand(_ => CleanName());
            SuggestTagsCommand = new RelayCommand(async _ => await SuggestTagsAsync(), _ => CanSuggest);
            Game = game;

            OpenFolderCommand = new RelayCommand(_ => OpenFolder());
            ChangeFolderCommand = new RelayCommand(_ => ChangeFolder());

            ChangeCoverCommand = new RelayCommand(_ => ChangeCoverRequested?.Invoke(this, EventArgs.Empty));
            UseAutomaticCoverCommand = new RelayCommand(_ => UseAutomaticCover());

            AddExecutableCommand = new RelayCommand(_ => AddExecutable());
            RemoveExecutableCommand = new RelayCommand(_ => RemovePrimaryExecutable(), _ => Game.ExecutablesRaw.Count > 1);

            SaveCommand = new RelayCommand(_ => SaveChanges());
            CancelCommand = new RelayCommand(_ => CancelChanges());
            ResetCommand = new RelayCommand(_ => ResetChanges());
        }

        private void OpenFolder() {
            if(!Game.OpenFolder(out string? error)) {
                Debug.WriteLine($"Error opening game folder: {error}");
            }
        }

        private void AddExecutable() {
            var dlg = new OpenFileDialog
            {
                Title = "Select Executable",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                InitialDirectory = Game.DirPathRaw
            };

            if (dlg.ShowDialog() == true) {
                var fullPath = new FileInfo(dlg.FileName);
                var relativePath = Path.GetRelativePath(Game.DirPathRaw, fullPath.FullName);

                if (!Game.ExecutablesRaw.Contains(relativePath, StringComparer.OrdinalIgnoreCase)) {
                    // Assign a new list (instead of .Add) so the model raises its change notifications
                    Game.ExecutablesRaw = [.. Game.ExecutablesRaw, relativePath];
                    RefreshExecutables();
                }
                else {
                    Debug.WriteLine("Executable already exists in list.");
                }
            }
        }

        private void RemovePrimaryExecutable() {
            if (Game.ExecutablesRaw.Count <= 1) {
                Debug.WriteLine("Cannot remove the only executable.");
                return;
            }

            if (Game.PrimaryExecutableIndex >= Game.ExecutablesRaw.Count) {
                Game.PrimaryExecutableIndex = 0;
            }

            var remaining = Game.ExecutablesRaw.ToList();
            remaining.RemoveAt(Game.PrimaryExecutableIndex);
            Game.PrimaryExecutableIndex = 0;
            Game.ExecutablesRaw = remaining;

            RefreshExecutables();
        }

        private void SaveChanges() {
            if(string.IsNullOrWhiteSpace(Game.Name)) {
                Debug.WriteLine("Game name cannot be empty.");
                Game.Name = GameMappingManager.GetMapping(Game.DirPathRaw, out string? err)?.Name ?? GameMapping.UNKNOWN_PLACEHOLDER;
                Debug.WriteLine($"Setting game name to default: {Game.Name}");
            }


            if (!GameMappingManager.UpdateMapping(Game, out string? error)) {
                // Stay on the page so the user's edits aren't silently thrown away
                Debug.WriteLine($"Error saving game mapping: {error}");
                return;
            }

            Debug.WriteLine($"Changes saved: {Game}");
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CancelChanges() {
            // Nothing to undo on disk: edits only live in memory until Save, and the library reloads from disk when shown
            Debug.WriteLine($"Changes cancelled: {Game}");
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ResetChanges() {
            var errors = new List<string>();
            var temp = GameMappingManager.ScanSingleGame(Game.DirPath!, errors);

            // Could not find or scan the mapping at all
            if (temp == null) {
                Debug.WriteLine($"Error resetting game mapping: {string.Join(", ", errors)}");
                return;
            }

            // Reset undoes settings (name, executables, tags); it shouldn't erase when/how often the game was played
            temp.LastPlayed = Game.LastPlayed;
            temp.LaunchCount = Game.LaunchCount;
            temp.DateAdded = Game.DateAdded;

            // The cover isn't a setting either, so it stays as it is. But Reset does count as "the game was updated":
            // once saved, an automatic cover is looked up again (a cover the user picked is never replaced).
            temp.CoverPath = Game.CoverPath;
            temp.CoverMatchedName = Game.CoverMatchedName;
            temp.CoverIsCustom = Game.CoverIsCustom;
            temp.CoverLookupPending = true;
            temp.CoverSource = Game.CoverSource;
            temp.Source = Game.Source; // which launcher it came from is not something Reset should forget

            Game = temp;

            // Successfully scanned a new mapping
            if (errors.Count == 0) {
                Debug.WriteLine("Game mapping reset successfully.");
            }
            // Returned preexisting mapping, but with issues
            else {
                Debug.WriteLine($"Game mapping reset with issues: {string.Join(", ", errors)}");
            }

            Debug.WriteLine($"Reset changes: {Game}");
        }
    }
}
