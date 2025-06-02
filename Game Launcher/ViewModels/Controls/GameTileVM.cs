using Game_Launcher.Models;
using System.Diagnostics;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Controls {
    internal class GameTileVM : BaseVM {
        public GameMapping Game { get; }

        public ICommand LaunchCommand { get; }
        public ICommand OptionsCommand { get; }

        public event EventHandler? OptionsRequested;

        public GameTileVM(GameMapping game) {
            Game = game;
            LaunchCommand = new RelayCommand(_ => LaunchGame());
            OptionsCommand = new RelayCommand(_ => OptionsRequested?.Invoke(this, EventArgs.Empty));
        }

        private void LaunchGame() {
            if (!Game.LaunchExecutable(out string? error)) {
                Debug.WriteLine($"Error launching game: {error}");
            }
        }
    }
}
