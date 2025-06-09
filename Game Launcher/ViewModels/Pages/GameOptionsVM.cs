using Game_Launcher.Models;
using Game_Launcher.Services;
using Microsoft.Win32;
using System.CodeDom.Compiler;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Pages {
    internal class GameOptionsVM : BaseVM {

        private GameMapping _game = null!;
        public GameMapping Game {
            get => _game;
            set {
                if (_game != value) {
                    _game = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Executables));
                    OnPropertyChanged(nameof(SelectedExecutable));
                }
            }
        }

        public bool IsInstalled => Game.HasTag("Installed");
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

        public ObservableCollection<FileInfo> Executables => new([.. Game.Executables]);
        public FileInfo? SelectedExecutable {
            get => Game.PrimaryExecutableIndex < Game.Executables.Count
                ? Game.Executables[Game.PrimaryExecutableIndex]
                : null;
            set {
                if (value != null && Game.Executables.Contains(value)) {
                    Game.PrimaryExecutableIndex = Game.Executables.IndexOf(value);
                    OnPropertyChanged(nameof(SelectedExecutable));
                    OnPropertyChanged(nameof(Game));
                }
            }
        }

        public ObservableCollection<string> FilteredTags { get; } = new();

        public ICommand OpenFolderCommand { get; }

        public ICommand AddExecutableCommand { get; }
        public ICommand RemoveExecutableCommand { get; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetCommand { get; }



        public GameOptionsVM(GameMapping game) {
            Game = game;

            OpenFolderCommand = new RelayCommand(_ => OpenFolder());

            AddExecutableCommand = new RelayCommand(_ => AddExecutable());
            RemoveExecutableCommand = new RelayCommand(_ => RemovePrimaryExecutable());

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

                if (!Game.ExecutablesRaw.Contains(relativePath)) {
                    Game.ExecutablesRaw.Add(relativePath);
                    OnPropertyChanged(nameof(Executables));
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

            Game.ExecutablesRaw.RemoveAt(Game.PrimaryExecutableIndex);
            Game.PrimaryExecutableIndex = 0;

            OnPropertyChanged(nameof(Executables));
            OnPropertyChanged(nameof(SelectedExecutable));
        }

        private void SaveChanges() {
            if(string.IsNullOrWhiteSpace(Game.Name)) {
                Debug.WriteLine("Game name cannot be empty.");
                Game.Name = GameMappingManager.GetMapping(Game.DirPathRaw, out string? err)?.Name ?? GameMapping.UNKNOWN_PLACEHOLDER;
                Debug.WriteLine($"Setting game name to default: {Game.Name}");
            }


            if (!GameMappingManager.UpdateMapping(Game, out string? error)) {
                Debug.WriteLine($"Error saving game mapping: {error}");
            }

            Debug.WriteLine($"Changes saved: {Game}");

        }

        private void CancelChanges() {

            var temp = GameMappingManager.GetMapping(Game.DirPathRaw, out string? error);
            if (temp is not null) {
                Game = temp;
            }
            else {
                Debug.WriteLine($"Error retrieving game mapping for cancellation: {error}");
            }

            Debug.WriteLine($"Changes cancelled: {Game}");
        }

        private void ResetChanges() {
            var errors = new List<string>();
            var temp = GameMappingManager.ScanSingleGame(Game.DirPath!, errors);

            // Could not find or scan the mapping at all
            if (temp == null) {
                Debug.WriteLine($"Error resetting game mapping: {string.Join(", ", errors)}");
                return;
            }

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
