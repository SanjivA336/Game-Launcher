using Game_Launcher.Models;
using System;
using System.Collections.Generic;
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
using IOPath = System.IO.Path;
using System.Diagnostics;

namespace Game_Launcher.Views.Pages
{
    public partial class GameOptions : Page
    {
        public GameOptions(GameMapping mapping) {
            InitializeComponent();

            this.DataContext = mapping;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e) {

        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {

        }

        private void Reset_Click(object sender, RoutedEventArgs e) {

        }
    }
}
