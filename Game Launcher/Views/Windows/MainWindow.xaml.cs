using Game_Launcher.Models;
using Game_Launcher.Services;
using Game_Launcher.ViewModels.Windows;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Game_Launcher {
    public partial class MainWindow : Window {

        public MainWindow() {
            InitializeComponent();

            TitleBar.MouseDown += TitleBar_MouseDown;

            DataContext = new MainWindowVM(
                minimize: () => WindowState = WindowState.Minimized,
                maximize: () => {
                    if (WindowState == WindowState.Maximized) {
                        WindowState = WindowState.Normal;
                    }
                    else {
                        WindowState = WindowState.Maximized;
                    }
                },
                close: () => Close()
            );

            PageHost.Navigate(new Views.Pages.Library());
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) {
                DragMove();
            }
        }
    }
}