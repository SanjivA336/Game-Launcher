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

        private string _searchText;
        public string SearchText {
            get => _searchText;
            set {
                if (_searchText != value) {
                    _searchText = value;
                    OnPropertyChanged();
                    LoadGames(_searchText);
                }
            }
        }


        public ICommand RefreshCommand { get; }

        public LibraryVM() {
            RefreshCommand = new RelayCommand(_ => Refresh());
            _searchText = string.Empty;
            Refresh();
        }

        private void Refresh() {
            GameMappingManager.ScanGames();
            LoadGames();
            SearchText = string.Empty;
        }

        public void LoadGames(string? searchTerm = null, ICollection<string>? tags = null) {
            var errors = new List<string>();
            var mappings = GameMappingManager.SearchMappings(searchTerm, 25);

            if (tags != null && tags.Count > 0) {
                mappings = mappings.Where(m => m.Tags.Any(t => tags.Contains(t))).ToList();
            }

            GamesTiles.Clear();
            foreach (var mapping in mappings) {
                try {
                    GamesTiles.Add(new GameTileVM(mapping));
                }
                catch (Exception ex) {
                    errors.Add($"Error creating tile for {mapping.Name}: {ex.Message}");
                }
            }

            // Handle errors, e.g., show a message box or log them
            if (errors.Count > 0) {
                foreach (var error in errors) {
                    System.Diagnostics.Debug.WriteLine($"Error loading game mappings: {error}");
                }
            }
        }
    }
}
