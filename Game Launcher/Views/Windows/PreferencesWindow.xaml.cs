using Game_Launcher.Helpers;
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

            SourceInitialized += (_, _) => WindowStyler.Apply(this);
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

        private PreferencesWindowVM VM => (PreferencesWindowVM)DataContext;

        // Dragging from Explorer: only accept files/folders, and show the "copy" cursor so it's clear a drop is welcome
        private void Folders_DragOver(object sender, DragEventArgs e) {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Roots_Drop(object sender, DragEventArgs e) {
            foreach (string path in DroppedPaths(e)) {
                VM.Sources.AddRootPath(path);
            }
        }

        private void Excludes_Drop(object sender, DragEventArgs e) {
            foreach (string path in DroppedPaths(e)) {
                if (System.IO.Directory.Exists(path)) {
                    VM.Sources.AddExcludePath(path);
                }
            }
        }

        private static string[] DroppedPaths(DragEventArgs e) {
            return e.Data.GetData(DataFormats.FileDrop) as string[] ?? [];
        }

        // Pressing Enter in the "Add a word" box adds the word, like pressing the Add button
        private void Keyword_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter && VM.Sources.AddKeywordCommand.CanExecute(null)) {
                VM.Sources.AddKeywordCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) {
                DragMove();
            }
        }
    }
}
