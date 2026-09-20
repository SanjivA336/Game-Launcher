using Game_Launcher.Models;
using Game_Launcher.Services;
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

        public ICommand StartCleanupCommand { get; }
        public ICommand ConfirmCleanupCommand { get; }
        public ICommand CancelCleanupCommand { get; }

        private void StartCleanup() {
            _pendingCleanup = GameMappingManager.PreviewNameCleanup();
            CleanupExamples.Clear();
            CleanupStatus = string.Empty;

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
            int changed = GameMappingManager.CleanUpAllNames();
            IsConfirmingCleanup = false;
            CleanupExamples.Clear();
            CleanupStatus = changed == 1 ? "Renamed 1 game." : $"Renamed {changed} games.";
        }

        private void CancelCleanup() {
            IsConfirmingCleanup = false;
            CleanupExamples.Clear();
        }
        #endregion

        public ObservableCollection<DirectoryInfo> Roots => new([.. Preferences.Roots]);

        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand CloseCommand { get; }

        public ICommand AddRootCommand { get; }
        public ICommand RemoveRootCommand { get; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetCommand { get; }



        public PreferencesWindowVM(Action minimize, Action maximize, Action close) {
            Preferences = Preferences.Load();
            _steamGridDbApiKey = Preferences.SteamGridDbApiKey;
            _cleanNewGameNames = Preferences.CleanNewGameNames;
            _close = close;

            StartCleanupCommand = new RelayCommand(_ => StartCleanup());
            ConfirmCleanupCommand = new RelayCommand(_ => ConfirmCleanup());
            CancelCleanupCommand = new RelayCommand(_ => CancelCleanup());

            MinimizeCommand = new RelayCommand(_ => minimize());
            MaximizeCommand = new RelayCommand(_ => maximize());
            CloseCommand = new RelayCommand(_ => close());

            AddRootCommand = new RelayCommand(_ => AddRoot());
            RemoveRootCommand = new RelayCommand(_ => RemovePrimaryFolder());

            SaveCommand = new RelayCommand(_ => SaveChanges());
            CancelCommand = new RelayCommand(_ => CancelChanges());
            ResetCommand = new RelayCommand(_ => ResetChanges());
        }

        private void AddRoot() {
            var dlg = new OpenFolderDialog {
                Title = "Select a root folder",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
                ValidateNames = true,
                Multiselect = false,
            };

            if (dlg.ShowDialog() == true) {
                var fullPath = new DirectoryInfo(dlg.FolderName);

                // Preferences.Roots builds a fresh throwaway set on every read, so adding to it did nothing.
                // AddRoot writes to the real underlying set (which also ignores exact duplicates).
                if (!Preferences.Roots.Any(r => r.FullName.Equals(fullPath.FullName, StringComparison.OrdinalIgnoreCase))) {
                    Preferences.AddRoot(fullPath);
                    OnPropertyChanged(nameof(Roots));
                }
                else {
                    Debug.WriteLine("Root folder already exists in list.");
                }
            }
        }

        private void RemovePrimaryFolder() {

        }

        private void SaveChanges() {
            Preferences.SteamGridDbApiKey = (SteamGridDbApiKey ?? string.Empty).Trim();
            Preferences.CleanNewGameNames = CleanNewGameNames;
            Preferences.Save();
            _close();
        }

        private void CancelChanges() {
            _close();
        }

        private void ResetChanges() {

        }
    }
}
