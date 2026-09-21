using Game_Launcher.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;

namespace Game_Launcher.Views.Pages {
    public partial class Home : Page {

        // Each card in "Continue playing" is 160 wide with a 20 gap after it
        private const double CardWidth = 160;
        private const double CardGap = 20;

        private readonly HomeVM _vm;

        /// <param name="showLibrary"> Called by the "See all your games" card.</param>
        public Home(Action showLibrary) {
            InitializeComponent();

            _vm = new HomeVM(showLibrary);
            DataContext = _vm;

            // Runs every time Home is shown, so games you just launched appear in "Continue playing"
            Loaded += (_, _) => _vm.Load();

            // As the window is resized, show as many recent games as fit (the last slot is always the "See all" card)
            RecentHost.SizeChanged += (_, e) => _vm.SetColumns(ColumnsFor(e.NewSize.Width));
        }

        /// <summary> How many cards fit in a row of this width: n cards take n * (card + gap) minus the gap after the last one. </summary>
        private static int ColumnsFor(double width) => Math.Max(1, (int)((width + CardGap) / (CardWidth + CardGap)));
    }
}
