using Game_Launcher.Models;
using Game_Launcher.ViewModels.Pages;
using Game_Launcher.Views.Windows;
using System.Windows;
using System.Windows.Controls;

namespace Game_Launcher.Views.Pages
{
    public partial class GameOptions : Page
    {
        public GameOptions(GameMapping mapping) {
            InitializeComponent();

            var vm = new GameOptionsVM(mapping);
            vm.CloseRequested += (_, _) => ReturnToLibrary();
            vm.ChangeCoverRequested += (_, _) => OpenCoverPicker(vm);
            DataContext = vm;
        }

        // The ViewModel only says "I'm done" / "open the picker"; opening windows and navigating are the page's job.
        private void ReturnToLibrary() {
            if (NavigationService is null) {
                return;
            }

            if (NavigationService.CanGoBack) {
                NavigationService.GoBack();
            }
            else {
                NavigationService.Navigate(new Library());
            }
        }

        private void OpenCoverPicker(GameOptionsVM vm) {
            var picker = new CoverPickerWindow(vm.Game) { Owner = Window.GetWindow(this) };
            picker.ShowDialog();

            // The picker saves the new cover by itself; show it here
            vm.RefreshCoverFromStore();
        }
    }
}
