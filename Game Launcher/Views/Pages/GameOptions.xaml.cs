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

namespace Game_Launcher.Views.Pages
{
    public partial class GameOptions : Page
    {
        private GameMapping mapping;

        public GameOptions(GameMapping map) {
            InitializeComponent();
            mapping = map;
            this.DataContext = mapping;
        }

        private void ChangeImage_Click(object sender, RoutedEventArgs e) {
            var dialog = new Microsoft.Win32.OpenFileDialog{ Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" };
            if (dialog.ShowDialog() == true) {
                string imageDir = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "Images");
                Directory.CreateDirectory(imageDir);

                string ext = System.IO.Path.GetExtension(dialog.FileName);
                string uniqueFileName = $"{Guid.NewGuid()}{ext}";
                string destPath = System.IO.Path.Combine(imageDir, uniqueFileName);

                System.IO.File.Copy(dialog.FileName, destPath, overwrite: true);

                if (this.DataContext is GameMapping game) {
                    game.CoverImagePath = destPath;
                }
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e) {
            
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {

        }

        private void Reset_Click(object sender, RoutedEventArgs e) {

        }
    }
}
