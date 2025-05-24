using Game_Launcher.Models;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Game_Launcher.Views {
    public partial class GameTile : UserControl {

        public event EventHandler? OptionsRequested;

        public GameTile() {
            InitializeComponent();
        }

        private void Launch_Click(object sender, RoutedEventArgs e) {
            var game = DataContext as GameMapping;

            if (game == null) {
                Debug.WriteLine("This game does not exist.");
                return;
            }

            if(!game.LaunchExecutable(out string? error)) {
                Debug.WriteLine(error);
            }

            Keyboard.ClearFocus();
        }

        private void Options_Click(object sender, RoutedEventArgs e) {
            OptionsRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
