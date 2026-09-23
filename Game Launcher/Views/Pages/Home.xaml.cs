using Game_Launcher.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;

namespace Game_Launcher.Views.Pages {
    public partial class Home : Page {

        private readonly HomeVM _vm;

        /// <param name="showLibrary"> Called by the "See all your games" card.</param>
        public Home(Action showLibrary) {
            InitializeComponent();

            _vm = new HomeVM(showLibrary);
            DataContext = _vm;

            // Runs every time Home is shown, so games you just launched appear in "Continue playing"
            Loaded += (_, _) => _vm.Load();

            // As the window is resized, show as many recent games (and however wide a "See all" card) as fit
            RecentHost.SizeChanged += (_, e) => _vm.SetAvailableWidth(e.NewSize.Width);
        }
    }
}
