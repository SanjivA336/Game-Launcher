using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Game_Launcher.Models;

namespace Game_Launcher.Views.Pages {
    public partial class Library : Page {
        private List<GameMapping> _mappings;

        public Library(List<GameMapping> mappings) {
            InitializeComponent();
            _mappings = mappings;
            this.SizeChanged += Library_SizeChanged;
            DisplayGames();
        }

        private void Library_SizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateTileGridLayout();
        }

        private void DisplayGames() {
            GameTileGrid.Children.Clear();
            foreach (var mapping in _mappings) {
                var tile = new GameTile();
                tile.DataContext = mapping;
                tile.OptionsRequested += (s, e) => { this.NavigationService.Navigate(new GameOptions(mapping)); };
                GameTileGrid.Children.Add(tile);
            }
            UpdateTileGridLayout();
        }

        private void UpdateTileGridLayout() {
            double gridWidth = GameTileGrid.ActualWidth;
            const double tileRatio = 1.25; // Height / Width
            const double maxTileWidth = 240;
            const double minTileWidth = 200;
            const double tileMargin = 20;

            if (gridWidth == 0)
                return;

            int columns = Math.Max(1, (int)(gridWidth / minTileWidth));
            int rows = Math.Max(1, (int)System.Math.Ceiling((double)GameTileGrid.Children.Count / columns));

            GameTileGrid.Columns = columns;
            GameTileGrid.Rows = rows;

            double totalMarginSpace = (columns + 1) * tileMargin;
            double availableWidth = gridWidth - totalMarginSpace;
            double tileWidth = availableWidth / columns;
            tileWidth = System.Math.Max(minTileWidth, System.Math.Min(tileWidth, maxTileWidth));
            double tileHeight = tileWidth * tileRatio;

            foreach (UIElement child in GameTileGrid.Children) {
                if (child is FrameworkElement fe) {
                    fe.Width = tileWidth;
                    fe.Height = tileHeight;
                    fe.Margin = new Thickness(tileMargin / 2);
                }
            }
        }
    }
}
