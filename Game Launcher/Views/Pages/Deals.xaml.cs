using Game_Launcher.ViewModels.Pages;
using System.Windows.Controls;

namespace Game_Launcher.Views.Pages {
    public partial class Deals : Page {

        private readonly DealsVM _vm = new();

        public Deals() {
            InitializeComponent();
            DataContext = _vm;

            // Deals are only fetched when the page is shown (and reused for a while, so switching back and forth is instant)
            Loaded += async (_, _) => await _vm.LoadAsync(force: false);

            // Going to another page of deals starts at the top again
            _vm.PageChanged += (_, _) => CardsScroll.ScrollToTop();
        }
    }
}
