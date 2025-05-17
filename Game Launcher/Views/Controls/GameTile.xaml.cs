using Game_Launcher.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO;

namespace Game_Launcher.Views {
    public partial class GameTile : UserControl {

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
    }
}
