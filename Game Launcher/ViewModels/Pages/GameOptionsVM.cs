using Game_Launcher.Models;
using Game_Launcher.Services;
using System.CodeDom.Compiler;
using System.Diagnostics;
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
                }
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetCommand { get; }

        public GameOptionsVM(GameMapping game) {
            Game = game;

            SaveCommand = new RelayCommand(_ => SaveChanges());
            CancelCommand = new RelayCommand(_ => CancelChanges());
            ResetCommand = new RelayCommand(_ => ResetChanges());
        }

        private void SaveChanges() {
            if (!GameMappingManager.UpdateMapping(Game, out string? error)) {
                Debug.WriteLine($"Error saving game mapping: {error}");
            }
        }

        private void CancelChanges() {
            var temp = GameMappingManager.GetMapping(Game.DirPathRaw, out string? error);
            if (temp is not null) {
                Game = temp;
            }
            else {
                Debug.WriteLine($"Error retrieving game mapping for cancellation: {error}");
            }
        }

        private void ResetChanges() {
            var temp = GameMappingManager.ScanSingleGame(Game.DirPath, out string[] error);

            // Could not find or scan the mapping at all
            if (temp == null) {
                Debug.WriteLine($"Error resetting game mapping: {string.Join(", ", error)}");
                return;
            }

            Game = temp;

            // Successfully scanned a new mapping
            if (error.Length == 0) {
                Debug.WriteLine("Game mapping reset successfully.");
            }
            // Returned preexisting mapping, but with issues
            else {
                Debug.WriteLine($"Game mapping reset with issues: {string.Join(", ", error)}");
            }
        }
    }
}
