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

            // Listen to the game area itself (not the whole page): opening the filter panel shrinks the
            // game area without resizing the page, and the tile sizes must follow.
            GameTileGrid.SizeChanged += Library_SizeChanged;
            this.Loaded += Library_Loaded;

            DataContext = new LibraryVM();
        }

        // Runs every time the page is shown, including when coming back from a game's options page.
        // Reloads from disk (so edits show up) but keeps the current search, filters and sort applied.
        private void Library_Loaded(object sender, RoutedEventArgs e) {
            VM.LoadGames();
            VM.StartCoverFetch(); // picks up new games, renamed games and a newly entered API key
        }

        /// <summary> Called when Settings closes: names may have been cleaned up and an API key may have been added. </summary>
        public void RefreshAfterSettings() {
            VM.LoadGames();
            VM.StartCoverFetch();
        }

        /// <summary> Puts the page back to its starting state: search cleared and scrolled to the top. </summary>
        public void ShowHome() {
            VM.SearchText = string.Empty;
            GameTileGrid.ScrollToTop();
        }

        private void Library_SizeChanged(object sender, SizeChangedEventArgs e) {
            UniformGrid? grid = FindVisualChild<UniformGrid>(GameTileGrid);
            if (grid is null)
                return;

            double gridWidth = grid.ActualWidth;
            // Covers are portrait (2:3), so the picture area is 1.5x as tall as the tile is wide; the name area below adds a fixed height
            const double coverRatio = 1.5;
            const double footerHeight = 62;
            const double maxTileWidth = 200;
            const double minTileWidth = 160;
            const double tileMargin = 10;

            if (gridWidth == 0)
                return;

            // Every tile has a margin on BOTH sides, so each grid cell must fit the tile plus 2 margins
            // (the old math reserved only one margin per column, which made the last column get clipped)
            int columns = Math.Max(1, (int)(gridWidth / (minTileWidth + 2 * tileMargin)));
            double cellWidth = gridWidth / columns;
            double tileWidth = cellWidth - 2 * tileMargin;
            tileWidth = Math.Max(minTileWidth, Math.Min(tileWidth, maxTileWidth));
            double tileHeight = tileWidth * coverRatio + footerHeight;

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