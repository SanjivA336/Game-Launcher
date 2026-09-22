using Game_Launcher.Helpers;
using Game_Launcher.Models;
using Game_Launcher.Services;
using Game_Launcher.ViewModels.Pages;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Windows {
    internal class PreferencesWindowVM : BaseVM {

        Preferences Preferences { get; }
        private readonly Action _close;

        private string _steamGridDbApiKey;
        /// <summary> The key used to download cover art from SteamGridDB. </summary>
        public string SteamGridDbApiKey {
            get => _steamGridDbApiKey;
            set {
                if (_steamGridDbApiKey != value) {
                    _steamGridDbApiKey = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _cleanNewGameNames;
        /// <summary> Whether games found from now on get a cleaned-up name. Saved when Save is pressed. </summary>
        public bool CleanNewGameNames {
            get => _cleanNewGameNames;
            set {
                if (_cleanNewGameNames != value) {
                    _cleanNewGameNames = value;
                    OnPropertyChanged();
                }
            }
        }

        #region Data folder
        /// <summary> Where Nexus keeps its settings, game details and covers. </summary>
        public string DataFolderPath => AppPaths.DataDirectory;

        public ICommand OpenDataFolderCommand { get; }

        private void OpenDataFolder() {
            try {
                Directory.CreateDirectory(AppPaths.DataDirectory); // it only exists once something has been saved
                Process.Start(new ProcessStartInfo { FileName = AppPaths.DataDirectory, UseShellExecute = true, Verb = "open" });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) {
                Debug.WriteLine($"Could not open the data folder: {ex.Message}");
            }
        }
        #endregion

        #region Clean up existing names
        // Cleaning ALL names can't be undone (it overwrites names the user may have typed), so it takes two steps:
        // press the button to see what would change, then confirm.
        private List<GameMappingManager.NameChange> _pendingCleanup = new();

        public ObservableCollection<string> CleanupExamples { get; } = new();

        private bool _isConfirmingCleanup;
        public bool IsConfirmingCleanup {
            get => _isConfirmingCleanup;
            private set {
                if (_isConfirmingCleanup != value) {
                    _isConfirmingCleanup = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _cleanupQuestion = string.Empty;
        public string CleanupQuestion {
            get => _cleanupQuestion;
            private set {
                _cleanupQuestion = value;
                OnPropertyChanged();
            }
        }

        private string _cleanupStatus = string.Empty;
        public string CleanupStatus {
            get => _cleanupStatus;
            private set {
                _cleanupStatus = value;
                OnPropertyChanged();
            }
        }

        /// <summary> Every game the last clean-up renamed ("old  ->  new"). </summary>
        public ObservableCollection<string> CleanupRenamed { get; } = new();

        /// <summary> Adds Steam's suggested tags to every game (with progress, and lists of what changed and what wasn't found). </summary>
        public BulkTagsVM Tags { get; } = new();

        #region SteamGridDB key
        private readonly Func<string, CancellationToken, Task<CoverOutcome>> _testKey;

        private string _keyStatus = string.Empty;
        /// <summary> Result of pressing "Test key". </summary>
        public string KeyStatus {
            get => _keyStatus;
            private set { _keyStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasKeyStatus)); }
        }
        public bool HasKeyStatus => _keyStatus.Length > 0;

        public ICommand OpenKeyPageCommand { get; }
        public ICommand TestKeyCommand { get; }

        private void OpenKeyPage() {
            try {
                Process.Start(new ProcessStartInfo { FileName = "https://www.steamgriddb.com/profile/preferences/api", UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) {
                Debug.WriteLine($"Could not open the SteamGridDB page: {ex.Message}");
            }
        }

        /// <summary> Tries the key in the box (saved or not) with one small search. </summary>
        public async Task TestKeyAsync() {
            string key = (SteamGridDbApiKey ?? string.Empty).Trim();
            if (key.Length == 0) {
                KeyStatus = "Paste your key first.";
                return;
            }

            KeyStatus = "Testing…";
            KeyStatus = await _testKey(key, CancellationToken.None) switch {
                CoverOutcome.InvalidKey => "SteamGridDB rejected this key.",
                CoverOutcome.Unavailable => "Couldn't reach SteamGridDB.",
                _ => "Key works.",
            };
        }
        #endregion

        public ICommand StartCleanupCommand { get; }
        public ICommand ConfirmCleanupCommand { get; }
        public ICommand CancelCleanupCommand { get; }

        private void StartCleanup() {
            _pendingCleanup = GameMappingManager.PreviewNameCleanup();
            CleanupExamples.Clear();
            CleanupStatus = string.Empty;
            CleanupRenamed.Clear();

            if (_pendingCleanup.Count == 0) {
                IsConfirmingCleanup = false;
                CleanupStatus = "All game names are already clean.";
                return;
            }

            foreach (var change in _pendingCleanup.Take(8)) {
                CleanupExamples.Add($"{change.OldName}  →  {change.NewName}");
            }
            if (_pendingCleanup.Count > 8) {
                CleanupExamples.Add($"…and {_pendingCleanup.Count - 8} more");
            }

            CleanupQuestion = _pendingCleanup.Count == 1 ? "Rename 1 game? This can't be undone." : $"Rename {_pendingCleanup.Count} games? This can't be undone.";
            IsConfirmingCleanup = true;
        }

        private void ConfirmCleanup() {
            var renamed = _pendingCleanup.ToList();
            int changed = GameMappingManager.CleanUpAllNames();
            IsConfirmingCleanup = false;
            CleanupExamples.Clear();
            CleanupRenamed.Clear();
            foreach (var change in renamed) {
                CleanupRenamed.Add($"{change.OldName}  →  {change.NewName}");
            }
            CleanupStatus = changed == 1 ? "Renamed 1 game." : $"Renamed {changed} games.";
        }

        private void CancelCleanup() {
            IsConfirmingCleanup = false;
            CleanupExamples.Clear();
        }
        #endregion

        #region Tabs
        private enum Tab { Sources, Apps, General }
        private Tab _tab = Tab.Sources;

        // One property per tab so each radio button can bind to its own bool
        public bool IsSourcesTab { get => _tab == Tab.Sources; set { if (value) SelectTab(Tab.Sources); } }
        public bool IsAppsTab { get => _tab == Tab.Apps; set { if (value) SelectTab(Tab.Apps); } }
        public bool IsGeneralTab { get => _tab == Tab.General; set { if (value) SelectTab(Tab.General); } }

        private void SelectTab(Tab tab) {
            if (_tab == tab) {
                return;
            }

            _tab = tab;
            OnPropertyChanged(nameof(IsSourcesTab));
            OnPropertyChanged(nameof(IsAppsTab));
            OnPropertyChanged(nameof(IsGeneralTab));

            // Looking for installed launchers takes a moment, so it only happens when the Apps tab is actually opened
            if (tab == Tab.Apps) {
                _ = Apps.LoadAsync();
            }
        }
        #endregion

        /// <summary> The Apps tab. Unlike the rest of Settings its changes are saved immediately (they live in apps.json). </summary>
        public AppsVM Apps { get; } = new();

        /// <summary> The editable scan folders, excluded folders and ignore words (staged until Save). </summary>
        public SourcesEditorVM Sources { get; }

        private bool _dealsPageEnabled;
        /// <summary> Whether the Deals page appears in the sidebar. </summary>
        public bool DealsPageEnabled {
            get => _dealsPageEnabled;
            set {
                if (_dealsPageEnabled != value) {
                    _dealsPageEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand CloseCommand { get; }

        public ICommand SaveCommand { get; }
        public ICommand RescanCommand { get; }
        public ICommand CancelCommand { get; }

        #region Save and rescan
        private bool _isScanning;
        public bool IsScanning {
            get => _isScanning;
            private set {
                _isScanning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEdit));
            }
        }

        private bool _scanFinished;
        /// <summary> True once a save (with or without a rescan) has something to report, so its summary line shows. Saving
        /// again, whether or not anything changed, updates it, so the window never has to be closed to keep editing. </summary>
        public bool ScanFinished {
            get => _scanFinished;
            private set { _scanFinished = value; OnPropertyChanged(); }
        }

        /// <summary> The form is locked only while a scan is actively running. </summary>
        public bool CanEdit => !_isScanning;
        public bool ShowSaveButtons => !_isConfirmingOrphanRemoval;

        private string _scanSummary = string.Empty;
        public string ScanSummary {
            get => _scanSummary;
            private set { _scanSummary = value; OnPropertyChanged(); }
        }

        /// <summary> Folders the scan couldn't read, shown under the summary. </summary>
        public ObservableCollection<string> ScanErrors { get; } = new();

        /// <summary> The Save button says "Save and rescan" when the scan folders or ignore words changed. </summary>
        public string SaveLabel => Sources.HasChanges ? "Save and rescan" : "Save";

        #region Removing games a scan folder no longer covers
        // Removing a scan folder (or excluding a folder that already has games in it) can leave games in the library whose
        // folder Nexus no longer looks at. That's a step the user asked for explicitly, but it throws away tags and play
        // history (their cover file is kept, so it comes straight back if the game is ever added again), so Save asks first.
        private List<GameMapping> _pendingOrphans = new();
        private bool _orphanRemovalConfirmed;

        public ObservableCollection<string> OrphanExamples { get; } = new();

        private bool _isConfirmingOrphanRemoval;
        public bool IsConfirmingOrphanRemoval {
            get => _isConfirmingOrphanRemoval;
            private set {
                _isConfirmingOrphanRemoval = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSaveButtons));
            }
        }

        private string _orphanQuestion = string.Empty;
        public string OrphanQuestion {
            get => _orphanQuestion;
            private set { _orphanQuestion = value; OnPropertyChanged(); }
        }

        public ICommand ConfirmOrphanRemovalCommand { get; }
        public ICommand CancelOrphanRemovalCommand { get; }

        private void CancelOrphanRemoval() {
            IsConfirmingOrphanRemoval = false;
            OrphanExamples.Clear();
            _pendingOrphans.Clear();
        }
        #endregion

        private void ApplyToPreferences() {
            Preferences.SteamGridDbApiKey = (SteamGridDbApiKey ?? string.Empty).Trim();
            Preferences.CleanNewGameNames = CleanNewGameNames;
            Preferences.DealsPageEnabled = DealsPageEnabled;
            Sources.BuildPreferences(Preferences);
            Preferences.Save();
        }

        private async Task SaveAsync(bool forceRescan) {
            if (!_orphanRemovalConfirmed) {
                _pendingOrphans = GameMappingManager.GamesOutOfScope(Sources.BuildPreferences(new Preferences()));
                if (_pendingOrphans.Count > 0) {
                    OrphanExamples.Clear();
                    foreach (var game in _pendingOrphans.Take(8)) {
                        OrphanExamples.Add(game.Name);
                    }
                    if (_pendingOrphans.Count > 8) {
                        OrphanExamples.Add($"…and {_pendingOrphans.Count - 8} more");
                    }
                    OrphanQuestion = _pendingOrphans.Count == 1
                        ? "1 game's folder is no longer covered by a scan folder. Remove it from your library too?"
                        : $"{_pendingOrphans.Count} games' folders are no longer covered by a scan folder. Remove them from your library too?";
                    IsConfirmingOrphanRemoval = true;
                    return;
                }
            }
            _orphanRemovalConfirmed = false;
            IsConfirmingOrphanRemoval = false;

            bool rescan = forceRescan || Sources.HasChanges;
            ApplyToPreferences();

            if (_pendingOrphans.Count > 0) {
                GameMappingManager.RemoveGamesOutOfScope(Preferences);
                _pendingOrphans.Clear();
                OrphanExamples.Clear();
                rescan = true; // reflect the removal in the summary even if nothing else changed
            }

            if (!rescan) {
                Sources.MarkSaved();
                ScanSummary = "Saved.";
                ScanErrors.Clear();
                ScanFinished = true;
                return;
            }

            IsScanning = true;
            ScanSummary = "Scanning your folders…";
            ScanErrors.Clear();
            try {
                var errors = new List<string>();
                var result = await Task.Run(() => GameMappingManager.ScanGames(errors, Preferences));

                string added = result.NewGames == 0 ? "no new games" : result.NewGames == 1 ? "1 new" : $"{result.NewGames} new";
                ScanSummary = $"Found {result.TotalGames} games ({added}).";
                if (result.Errors.Count > 0) {
                    ScanSummary += result.Errors.Count == 1 ? " 1 folder couldn't be read:" : $" {result.Errors.Count} folders couldn't be read:";
                    foreach (string error in result.Errors.Take(5)) {
                        ScanErrors.Add(error);
                    }
                }
            }
            catch (Exception ex) {
                ScanSummary = $"The scan failed: {ex.Message}";
            }
            finally {
                IsScanning = false;
                ScanFinished = true;
                Sources.MarkSaved();
            }
        }
        #endregion

        /// <param name="testKey"> Tries a SteamGridDB key. Replaceable in tests.</param>
        public PreferencesWindowVM(Action minimize, Action maximize, Action close, Func<string, string?>? pickFolder = null, Func<IReadOnlyList<LauncherLibrary>>? findLibraries = null, Func<string, CancellationToken, Task<CoverOutcome>>? testKey = null, Func<string, string?>? pickExecutable = null) {
            _testKey = testKey ?? CoverService.TestKeyAsync;
            OpenKeyPageCommand = new RelayCommand(_ => OpenKeyPage());
            TestKeyCommand = new RelayCommand(async _ => await TestKeyAsync());
            Preferences = Preferences.Load();
            _steamGridDbApiKey = Preferences.SteamGridDbApiKey;
            _cleanNewGameNames = Preferences.CleanNewGameNames;
            _dealsPageEnabled = Preferences.DealsPageEnabled;
            _close = close;
            Sources = new SourcesEditorVM(Preferences, pickFolder, findLibraries, openApps: () => IsAppsTab = true, pickExecutable: pickExecutable);
            Sources.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(SourcesEditorVM.HasChanges)) {
                    OnPropertyChanged(nameof(SaveLabel));
                }
            };

            OpenDataFolderCommand = new RelayCommand(_ => OpenDataFolder());
            StartCleanupCommand = new RelayCommand(_ => StartCleanup());
            ConfirmCleanupCommand = new RelayCommand(_ => ConfirmCleanup());
            CancelCleanupCommand = new RelayCommand(_ => CancelCleanup());

            MinimizeCommand = new RelayCommand(_ => minimize());
            MaximizeCommand = new RelayCommand(_ => maximize());
            CloseCommand = new RelayCommand(_ => close());

            // async void is only acceptable here because a button click is an event handler; the task's own errors are caught inside SaveAsync
            SaveCommand = new RelayCommand(async _ => await SaveAsync(forceRescan: false));
            RescanCommand = new RelayCommand(async _ => await SaveAsync(forceRescan: true));
            CancelCommand = new RelayCommand(_ => _close());
            ConfirmOrphanRemovalCommand = new RelayCommand(async _ => { _orphanRemovalConfirmed = true; await SaveAsync(forceRescan: false); });
            CancelOrphanRemovalCommand = new RelayCommand(_ => CancelOrphanRemoval());
        }
    }
}
