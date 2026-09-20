using Game_Launcher.Helpers;
using Game_Launcher.Models;
using Game_Launcher.ViewModels.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Game_Launcher.Views.Windows {
    /// <summary> The "change cover" popup: find a cover on SteamGridDB or upload your own image. </summary>
    public partial class CoverPickerWindow : Window {
        private readonly CoverPickerVM _vm;

        /// <summary> True if a cover was applied (so the caller should refresh what it shows). </summary>
        public bool Applied => _vm.Applied;

        public CoverPickerWindow(GameMapping game) {
            InitializeComponent();

            SourceInitialized += (_, _) => WindowStyler.Apply(this);
            TitleBar.MouseDown += TitleBar_MouseDown;
            PreviewKeyDown += CoverPickerWindow_PreviewKeyDown;

            _vm = new CoverPickerVM(game, applied => {
                DialogResult = applied;
                Close();
            });
            DataContext = _vm;

            // Closing the window (X, Cancel, Alt+F4) stops any search or download still running
            Closed += (_, _) => _vm.Cancel();
        }

        // Enter in the search box runs the search, like a web search field
        private void CoverPickerWindow_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter && Keyboard.FocusedElement is TextBox && _vm.IsSteamGridDbMode && _vm.SearchCommand.CanExecute(null)) {
                _vm.SearchCommand.Execute(null);
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
