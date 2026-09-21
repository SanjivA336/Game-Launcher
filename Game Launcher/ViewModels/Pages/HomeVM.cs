using Game_Launcher.Models;
using Game_Launcher.Services;
using Game_Launcher.ViewModels.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Pages {

    /// <summary> The last card of "Continue playing": a button that goes to the whole library. </summary>
    internal class SeeAllCardVM {
        public string Title => "See all your games";
        public string Subtitle { get; }
        public ICommand Command { get; }

        public SeeAllCardVM(int gameCount, Action open) {
            Subtitle = gameCount == 1 ? "1 game" : $"{gameCount} games";
            Command = new RelayCommand(_ => open());
        }
    }

    /// <summary> The start page: your recently played games, and a "pick something for me" section with its own filters. </summary>
    internal class HomeVM : BaseVM {

        private readonly Action _showLibrary;
        private readonly Random _random = new();
        private List<GameMapping> _allGames = new();

        #region Continue playing
        private List<GameMapping> _recent = new();
        private int _columns = 5;

        /// <summary> The recent games that fit on one row, followed by the "See all" card. </summary>
        public ObservableCollection<object> Items { get; } = new();

        /// <summary>
        /// How many cards fit across the page. One of them is always the "See all your games" card, so one fewer recent game is shown.
        /// Called by the page whenever its width changes.
        /// </summary>
        public void SetColumns(int columns) {
            columns = Math.Max(columns, 1);
            if (columns != _columns) {
                _columns = columns;
                RebuildItems();
            }
        }

        private void RebuildItems() {
            Items.Clear();
            foreach (var game in _recent.Take(Math.Max(_columns - 1, 0))) {
                Items.Add(new GameTileVM(game));
            }
            Items.Add(new SeeAllCardVM(_allGames.Count(g => !g.HasTag(TagCatalog.Hidden)), _showLibrary));
        }

        /// <summary> Installed, visible games that have been launched at least once, most recent first. </summary>
        public static IReadOnlyList<GameMapping> RecentGames(IEnumerable<GameMapping> games, int count) {
            return games
                .Where(g => g.LastPlayed.HasValue && !g.HasTag(TagCatalog.Hidden) && g.HasTag(TagCatalog.Installed))
                .OrderByDescending(g => g.LastPlayed)
                .Take(count)
                .ToList();
        }
        #endregion

        private string _subtitle = string.Empty;
        public string Subtitle {
            get => _subtitle;
            private set { _subtitle = value; OnPropertyChanged(); }
        }

        #region Pick something for me
        // These filters belong to the picker only: they never change what the Library page shows.
        public IReadOnlyList<TagGroupVM> FilterGroups { get; }
        private readonly List<TagChipVM> _filterChips;

        private bool _matchAll;
        /// <summary> True = a game needs ALL the selected tags. False = ANY one of them is enough. </summary>
        public bool MatchAll {
            get => _matchAll;
            set {
                if (_matchAll != value) {
                    _matchAll = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MatchAny));
                    UpdatePoolText();
                }
            }
        }
        public bool MatchAny {
            get => !_matchAll;
            set { if (value) MatchAll = false; }
        }

        public int ActiveFilterCount => _filterChips.Count(c => c.IsSelected);
        public ICommand ClearFiltersCommand { get; }

        private PickWeighting _weighting = PickWeighting.None;
        public PickWeighting Weighting => _weighting;

        // One bool per option so each radio button can bind to it
        public bool FavorMore { get => _weighting == PickWeighting.FavorMorePlayed; set { if (value) SetWeighting(PickWeighting.FavorMorePlayed); } }
        public bool FavorNone { get => _weighting == PickWeighting.None; set { if (value) SetWeighting(PickWeighting.None); } }
        public bool FavorLess { get => _weighting == PickWeighting.FavorLessPlayed; set { if (value) SetWeighting(PickWeighting.FavorLessPlayed); } }

        private void SetWeighting(PickWeighting weighting) {
            if (_weighting == weighting) return;
            _weighting = weighting;
            OnPropertyChanged(nameof(FavorMore));
            OnPropertyChanged(nameof(FavorNone));
            OnPropertyChanged(nameof(FavorLess));
            OnPropertyChanged(nameof(WeightingHint));
        }

        /// <summary> One line explaining what the chosen weighting does. </summary>
        public string WeightingHint => _weighting switch {
            PickWeighting.FavorMorePlayed => "Games you launch a lot come up more often.",
            PickWeighting.FavorLessPlayed => "Games you rarely launch (or never have) come up more often.",
            _ => "Every game is equally likely.",
        };

        private string _poolText = string.Empty;
        /// <summary> "12 installed games match", so you can see what the filters leave before picking. </summary>
        public string PoolText {
            get => _poolText;
            private set { _poolText = value; OnPropertyChanged(); }
        }

        private GameTileVM? _picked;
        public GameTileVM? Picked {
            get => _picked;
            private set {
                _picked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPicked));
            }
        }
        public bool HasPicked => _picked is not null;

        private string _pickMessage = string.Empty;
        /// <summary> Shown when there is nothing to pick from. </summary>
        public string PickMessage {
            get => _pickMessage;
            private set { _pickMessage = value; OnPropertyChanged(); }
        }

        public ICommand PickCommand { get; }

        private List<string> SelectedTags => _filterChips.Where(c => c.IsSelected).Select(c => c.Name).ToList();

        private void UpdatePoolText() {
            int count = GamePicker.Candidates(_allGames, SelectedTags, _matchAll).Count;
            PoolText = count == 1 ? "1 installed game matches" : $"{count} installed games match";
        }

        private void OnFilterChanged(TagChipVM chip) {
            OnPropertyChanged(nameof(ActiveFilterCount));
            UpdatePoolText();
        }

        private void ClearFilters() {
            foreach (var chip in _filterChips) {
                chip.SetSelectedSilently(false);
            }
            OnPropertyChanged(nameof(ActiveFilterCount));
            UpdatePoolText();
        }

        private void Pick() {
            var candidates = GamePicker.Candidates(_allGames, SelectedTags, _matchAll);
            var game = GamePicker.Choose(candidates, _weighting, _random, Picked?.Game.DirPathRaw);

            if (game is null) {
                Picked = null;
                PickMessage = ActiveFilterCount == 0
                    ? "There are no installed games to pick from yet."
                    : "None of your installed games match these filters.";
                return;
            }

            PickMessage = string.Empty;
            Picked = new GameTileVM(game);
        }
        #endregion

        public HomeVM(Action showLibrary) {
            _showLibrary = showLibrary;
            PickCommand = new RelayCommand(_ => Pick());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            // Everything a game can be filtered by, except the status tags that make no sense for a pick (it only ever picks
            // installed, visible games)
            var pickable = TagCatalog.All.Where(t => t.Name is not (TagCatalog.Installed or TagCatalog.NotInstalled or TagCatalog.Hidden));
            FilterGroups = pickable
                .GroupBy(t => t.Group)
                .Select(g => new TagGroupVM(g.Key, g.Select(t => new TagChipVM(t, false, OnFilterChanged)).ToList()))
                .ToList();
            _filterChips = FilterGroups.SelectMany(g => g.Tags).ToList();
        }

        /// <summary> Reloads the page's data; runs every time Home is shown, so a game you just launched appears right away. </summary>
        public void Load() {
            _allGames = GameMappingManager.LoadMappings();
            var visible = _allGames.Where(g => !g.HasTag(TagCatalog.Hidden)).ToList();

            _recent = RecentGames(visible, 30).ToList();
            RebuildItems();

            int installed = visible.Count(g => g.HasTag(TagCatalog.Installed));
            Subtitle = visible.Count == 0
                ? "Add a folder in Settings to fill your library."
                : $"{visible.Count} games in your library, {installed} installed";

            UpdatePoolText();
        }
    }
}
