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

        #region Search
        private string _searchText;
        public string SearchText {
            get => _searchText;
            set {
                if (_searchText != value) {
                    _searchText = value;
                    OnPropertyChanged();
                    LoadGames();
                }
            }
        }
        #endregion

        #region Sorting
        public IReadOnlyList<SortOption> SortOptions => LibraryQuery.SortOptions;

        private SortOption _selectedSort = LibraryQuery.SortOptions[0];
        public SortOption SelectedSort {
            get => _selectedSort;
            set {
                if (value is null || _selectedSort == value) {
                    return;
                }

                _selectedSort = value;
                _isDescending = value.DefaultDescending; // each sort starts in its natural direction (A to Z, newest first, ...)
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDescending));
                OnPropertyChanged(nameof(SortDirectionLabel));
                LoadGames();
            }
        }

        private bool _isDescending;
        public bool IsDescending => _isDescending;

        /// <summary> Tooltip text for the direction button, e.g. "A to Z" or "Newest first". </summary>
        public string SortDirectionLabel => SelectedSort.DirectionLabel(_isDescending);

        public ICommand ToggleSortDirectionCommand { get; }
        #endregion

        #region Tag filters
        public IReadOnlyList<TagGroupVM> FilterGroups { get; }
        private readonly List<TagChipVM> _filterChips;

        private bool _isFilterPanelOpen;
        public bool IsFilterPanelOpen {
            get => _isFilterPanelOpen;
            set {
                if (_isFilterPanelOpen != value) {
                    _isFilterPanelOpen = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _matchAll;
        /// <summary> True = a game needs ALL the selected tags (AND). False = ANY one of them is enough (OR). </summary>
        public bool MatchAll {
            get => _matchAll;
            set {
                if (_matchAll != value) {
                    _matchAll = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MatchAny));
                    LoadGames();
                }
            }
        }

        /// <summary> The opposite of MatchAll, so the "Any" radio button can bind to it directly without a converter. </summary>
        public bool MatchAny {
            get => !_matchAll;
            set {
                if (value) {
                    MatchAll = false;
                }
            }
        }

        public int ActiveFilterCount => _filterChips.Count(c => c.IsSelected);

        public ICommand ToggleFilterPanelCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        #endregion

        private string _countText = string.Empty;
        /// <summary> "27 games", or "5 of 27 games" while a search or filter is active. </summary>
        public string CountText {
            get => _countText;
            private set {
                if (_countText != value) {
                    _countText = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _emptyTitle = "No games to show";
        private string _emptyHint = "Try a different search or clear your filters.";
        /// <summary> What the empty grid says: a brand-new library needs a folder added, while an empty result just needs different filters. </summary>
        public string EmptyTitle {
            get => _emptyTitle;
            private set { if (_emptyTitle != value) { _emptyTitle = value; OnPropertyChanged(); } }
        }
        public string EmptyHint {
            get => _emptyHint;
            private set { if (_emptyHint != value) { _emptyHint = value; OnPropertyChanged(); } }
        }

        #region Cover art downloads
        private bool _coverDownloadRunning;

        private string _coverStatusText = string.Empty;
        /// <summary> Progress ("Downloading cover art… 3 of 12") or a problem message; empty when there is nothing to say. </summary>
        public string CoverStatusText {
            get => _coverStatusText;
            private set {
                if (_coverStatusText != value) {
                    _coverStatusText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary> Downloads covers for games that need one, in the background. Safe to call often: only one run happens at a time. </summary>
        public void StartCoverFetch() {
            if (_coverDownloadRunning) {
                return; // the running download re-checks for newly renamed games when it finishes a round
            }

            _ = RunCoverFetchAsync();
        }

        private async Task RunCoverFetchAsync() {
            _coverDownloadRunning = true;
            try {
                var result = await CoverService.DownloadMissingCoversAsync(status => CoverStatusText = status, OnCoverChanged);
                string reason = string.IsNullOrEmpty(result.Detail) ? string.Empty : $" ({result.Detail})";
                CoverStatusText = result.Status switch {
                    CoverRunStatus.InvalidKey => $"SteamGridDB rejected your API key{reason}. Check it in Settings.",
                    CoverRunStatus.Unavailable => $"Couldn't get covers from SteamGridDB{reason}. Nexus will try again next time.",
                    _ => string.Empty,
                };
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Cover download failed: {ex.Message}");
                CoverStatusText = string.Empty;
            }
            finally {
                _coverDownloadRunning = false;
            }
        }

        // A cover just finished downloading: update that game's tile (if it's on screen) so the cover appears immediately
        private void OnCoverChanged(GameMapping saved) {
            foreach (var tile in GamesTiles.Where(t => t.Game.DirPathRaw.Equals(saved.DirPathRaw, StringComparison.OrdinalIgnoreCase))) {
                tile.Game.CoverPath = saved.CoverPath;
                tile.Game.CoverMatchedName = saved.CoverMatchedName;
                tile.Game.CoverIsCustom = saved.CoverIsCustom;
                tile.Game.CoverLookupPending = saved.CoverLookupPending;
                tile.RefreshCover();
            }
        }
        #endregion

        public ICommand RefreshCommand { get; }

        public LibraryVM() {
            RefreshCommand = new RelayCommand(_ => Refresh());
            ToggleSortDirectionCommand = new RelayCommand(_ => ToggleSortDirection());
            ToggleFilterPanelCommand = new RelayCommand(_ => IsFilterPanelOpen = !IsFilterPanelOpen);
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            FilterGroups = TagCatalog.All
                .GroupBy(t => t.Group)
                .Select(g => new TagGroupVM(g.Key, g.Select(t => new TagChipVM(t, false, OnFilterChipChanged)).ToList()))
                .ToList();
            _filterChips = FilterGroups.SelectMany(g => g.Tags).ToList();

            _searchText = string.Empty;
            Refresh();
        }

        private void Refresh() {
            GameMappingManager.ScanGames();
            GameMappingManager.MigrateLegacyCoverNames();
            LoadGames();
            SearchText = string.Empty;
        }

        private void ToggleSortDirection() {
            _isDescending = !_isDescending;
            OnPropertyChanged(nameof(IsDescending));
            OnPropertyChanged(nameof(SortDirectionLabel));
            LoadGames();
        }

        private void OnFilterChipChanged(TagChipVM chip) {
            OnPropertyChanged(nameof(ActiveFilterCount));
            LoadGames();
        }

        private void ClearFilters() {
            foreach (var chip in _filterChips) {
                chip.SetSelectedSilently(false); // silently, so we reload once at the end instead of once per chip
            }

            OnPropertyChanged(nameof(ActiveFilterCount));
            LoadGames();
        }

        /// <summary> Rebuilds the tile list from disk using the current search, filters and sort. </summary>
        public void LoadGames() {
            var errors = new List<string>();
            var all = GameMappingManager.LoadMappings();
            UpdateTagCounts(all);

            var selectedTags = _filterChips.Where(c => c.IsSelected).Select(c => c.Name).ToList();
            var query = new LibraryQuery(SearchText, selectedTags, MatchAll, SelectedSort.Key, IsDescending);
            var games = query.Apply(all);

            GamesTiles.Clear();
            foreach (var mapping in games) {
                try {
                    GamesTiles.Add(new GameTileVM(mapping));
                }
                catch (Exception ex) {
                    errors.Add($"Error creating tile for {mapping.Name}: {ex.Message}");
                }
            }

            int libraryTotal = all.Count(g => !g.HasTag(TagCatalog.Hidden));
            bool filtering = !string.IsNullOrWhiteSpace(SearchText) || selectedTags.Count > 0;
            CountText = filtering ? $"{games.Count} of {libraryTotal} games" : $"{games.Count} games";

            bool libraryIsEmpty = all.Count == 0;
            EmptyTitle = libraryIsEmpty ? "No games yet" : "No games to show";
            EmptyHint = libraryIsEmpty ? "Add a folder in Settings and Nexus will find your games." : "Try a different search or clear your filters.";

            // Handle errors, e.g., show a message box or log them
            foreach (var error in errors) {
                System.Diagnostics.Debug.WriteLine($"Error loading game mappings: {error}");
            }
        }

        // Counts always describe the whole library (not the current filter), so you can see what a tag would give you before clicking it
        private void UpdateTagCounts(List<GameMapping> all) {
            var visible = all.Where(g => !g.HasTag(TagCatalog.Hidden)).ToList();
            foreach (var chip in _filterChips) {
                chip.Count = chip.Name == TagCatalog.Hidden
                    ? all.Count(g => g.HasTag(TagCatalog.Hidden))
                    : visible.Count(g => TagCatalog.Matches(g, chip.Name));
            }
        }
    }
}
