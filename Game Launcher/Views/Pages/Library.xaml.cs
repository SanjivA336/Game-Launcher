using Game_Launcher.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Game_Launcher.Views.Pages {
    public partial class Library : Page {

        private LibraryVM VM => (LibraryVM)DataContext;

        public Library() {
            InitializeComponent();

            this.SizeChanged += Library_SizeChanged;
            this.Loaded += Library_Loaded;

            DataContext = new LibraryVM();
        }

        private void Library_Loaded(object sender, RoutedEventArgs e) {
            if (DataContext is LibraryVM vm) {
                vm.LoadGames();
            }
        }

        private void Library_SizeChanged(object sender, SizeChangedEventArgs e) {
            UniformGrid? grid = FindVisualChild<UniformGrid>(GameTileGrid);
            if (grid is null)
                return;

            double gridWidth = grid.ActualWidth;
            const double tileRatio = 1.25;
            const double maxTileWidth = 250;
            const double minTileWidth = 200;
            const double tileMargin = 10;

            if (gridWidth == 0)
                return;

            int columns = Math.Max(1, (int)(gridWidth / minTileWidth));
            double totalMarginSpace = (columns + 1) * tileMargin;
            double availableWidth = gridWidth - totalMarginSpace;
            double tileWidth = availableWidth / columns;
            tileWidth = Math.Max(minTileWidth, Math.Min(tileWidth, maxTileWidth));
            double tileHeight = tileWidth * tileRatio;

            VM.Columns = columns;
            VM.TileWidth = tileWidth;
            VM.TileHeight = tileHeight;

        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}