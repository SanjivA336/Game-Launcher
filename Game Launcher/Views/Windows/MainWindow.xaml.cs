using Game_Launcher.Helpers;
using Game_Launcher.ViewModels.Windows;
using System.Windows;
using System.Windows.Input;

namespace Game_Launcher {
    public partial class MainWindow : Window {

        // One shared Library page for the whole session, so returning "home" doesn't rebuild it (and rescan your drives)
        private readonly Views.Pages.Library _library = new();

        public MainWindow() {
            InitializeComponent();

            SourceInitialized += (_, _) => WindowStyler.Apply(this);
            StateChanged += MainWindow_StateChanged;
            TitleBar.MouseDown += TitleBar_MouseDown;

            DataContext = new MainWindowVM(
                minimize: () => WindowState = WindowState.Minimized,
                maximize: ToggleMaximize,
                close: () => Close(),
                showLibrary: ShowLibrary,
                preferencesClosed: () => _library.RefreshAfterSettings()
            );

            PageHost.Navigate(_library);
        }

        private void ShowLibrary() {
            _library.ShowHome();

            // Navigating to the page that's already showing would do nothing, so only navigate when we're elsewhere
            if (PageHost.Content != _library) {
                PageHost.Navigate(_library);
            }
        }

        private void ToggleMaximize() {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e) {
            bool maximized = WindowState == WindowState.Maximized;

            // Swap the icon between "maximize" and "restore"
            MaximizeButton.Content = maximized ? "" : "";

            // A maximized borderless window extends past the screen by the width of its invisible resize border,
            // which would push the title bar buttons off-screen. Pull the content back in by that amount.
            RootGrid.Margin = maximized ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left) : new Thickness(0);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed) {
                return;
            }

            if (e.ClickCount == 2) {
                ToggleMaximize();
            }
            else {
                DragMove();
            }
        }
    }
}
