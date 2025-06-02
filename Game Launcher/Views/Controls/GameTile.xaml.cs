using Game_Launcher.ViewModels.Controls;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Game_Launcher.Views {
    public partial class GameTile : UserControl {
        public GameTile() {
            InitializeComponent();

            this.DataContextChanged += GameTile_DataContextChanged;
        }

        private void GameTile_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.OldValue is GameTileVM vm) {
                vm.OptionsRequested -= GameTileVM_OptionsRequested;
            }
            if (e.NewValue is GameTileVM newVm) {
                newVm.OptionsRequested += GameTileVM_OptionsRequested;
            }
        }

        private void GameTileVM_OptionsRequested(object? sender, EventArgs e) {
            NavigationService? navService = NavigationService.GetNavigationService(this);
            if (DataContext is GameTileVM vm && navService is not null) {
                navService.Navigate(new Views.Pages.GameOptions(vm.Game));
            }
            else {
                Debug.WriteLine("NavigationService is null or DataContext is not a GameTileVM.");
            }
        }
    }
}
