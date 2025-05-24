using Game_Launcher.Services;
using Game_Launcher.ViewModels.Windows;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Game_Launcher {
    public partial class MainWindow : Window {

        public MainWindow() {
            InitializeComponent();

            // Root paths to search for executables
            var roots = new List<DirectoryInfo> {
                new DirectoryInfo(@"D:\Games"),
                new DirectoryInfo(@"D:\Games\Amazon Games\Library"),
                new DirectoryInfo(@"D:\Games\Steam\steamapps\common"),
            };

            // Paths to ignore during the search
            var exludes = new List<DirectoryInfo> {
                new DirectoryInfo(@"D:\D:\Games\Amazon Games"),
                new DirectoryInfo(@"D:\Games\Steam"),
                new DirectoryInfo(@"D:\Games\UE_5.1"),
            };

            // Keywords to ignore in the file names
            var filters = new List<string> {
                "redist",
                "crash",
                "helper",
                "update",
                "unins",
                "setup",
                "bench",
                "anticheat",
                "worker",
                "agent",
                "service",
                "dotnet",
                "handler",
                "x86",
                "32",
                "trial",
                "downloader",
                "pbsvc",
                "readme",
                "prelaunch",
                "cracktro"
            };

            GameMappingManager.ScanGames(out string[] errors);

            // Show the library page by default
            PageHost.Navigate(new Views.Pages.Library());

            var VM = new MainWindowVM(
                minimize: () => WindowState = WindowState.Minimized,
                maximize: () =>
                {
                    if (WindowState == WindowState.Maximized) {
                        WindowState = WindowState.Normal;
                        // Optionally update ViewModel.IsMaximized
                    }
                    else {
                        WindowState = WindowState.Maximized;
                        // Optionally update ViewModel.IsMaximized
                    }
                },
                close: () => Close()
            );

            DataContext = VM;

            // Navigation: You may want to bind Frame's Content to viewModel.CurrentPage
            // Or keep this line if using code-behind navigation:
            PageHost.Navigate(new Views.Pages.Library());
        }

        // Drag-move logic can remain here, as it is UI-specific
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) {
                DragMove();
            }
        }
    }
}