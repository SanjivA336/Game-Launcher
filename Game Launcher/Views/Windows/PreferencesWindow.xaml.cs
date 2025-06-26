using Game_Launcher.ViewModels.Windows;
using System.Windows;
using System.Windows.Input;

namespace Game_Launcher.Views.Windows {
    /// <summary>
    /// Interaction logic for PreferencesWindow.xaml
    /// </summary>
    public partial class PreferencesWindow : Window {
        public PreferencesWindow() {
            InitializeComponent();

            TitleBar.MouseDown += TitleBar_MouseDown;

            DataContext = new PreferencesWindowVM(
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
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) {
                DragMove();
            }
        }
    }
}
