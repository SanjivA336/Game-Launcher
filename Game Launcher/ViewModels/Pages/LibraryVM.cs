using Game_Launcher.Models;
using Game_Launcher.Services;
using Game_Launcher.ViewModels.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Pages {
    internal class LibraryVM : BaseVM {
        public ObservableCollection<GameTileVM> GamesTiles { get; } = new();

        private int _columns;
        public int Columns {
            get => _columns;
            set { _columns = value; OnPropertyChanged(); }
        }

        private double _tileWidth;
        public double TileWidth {
            get => _tileWidth;
            set { _tileWidth = value; OnPropertyChanged(); }
        }

        private double _tileHeight;
        public double TileHeight {
            get => _tileHeight;
            set { _tileHeight = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }

        public LibraryVM() {
            RefreshCommand = new RelayCommand(_ => LoadGames());
            LoadGames();
        }

        private void LoadGames() {
            GamesTiles.Clear();
            GameMappingManager.ScanGames(out string[] errors);
            var mappings = GameMappingManager.LoadMappings();
            foreach (var mapping in mappings) {
                GamesTiles.Add(new GameTileVM(mapping));
            }

            // Handle errors, e.g., show a message box or log them
            if (errors.Length > 0) {
                foreach (var error in errors) {
                    System.Diagnostics.Debug.WriteLine($"Error loading game mappings: {error}");
                }
            }
        }
    }
}
