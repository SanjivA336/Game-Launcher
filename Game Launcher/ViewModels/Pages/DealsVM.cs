using System.Net.Http;
using Game_Launcher.Helpers;
using Game_Launcher.Models;
using Game_Launcher.Services;
using Game_Launcher.Services.Deals;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace Game_Launcher.ViewModels.Pages {

    /// <summary> One deal card: a game and the offer shown for it. </summary>
    internal class DealCardVM : BaseVM {
        private static readonly Brush Placeholder = Freeze(new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)));

        public DealGroup Group { get; }
        public Deal Deal => Group.Shown;
        public bool InLibrary { get; }

        public string Title => Group.Title;
        public string ProviderName => Deal.ProviderName;
        public string Note => Deal.Note ?? string.Empty;
        public bool IsFree => Deal.IsFree;
        public bool HasDiscount => !Deal.IsFree && Deal.DiscountPercent is > 0;
        public bool HasOriginalPrice => Deal.OriginalPriceText.Length > 0;
        public string OriginalPriceText => Deal.OriginalPriceText;
        public string CurrentPriceText => Deal.CurrentPriceText;
        public string DiscountText => Deal.DiscountText;

        /// <summary> "Ends 09/24", or "No Date Found". </summary>
        public string EndsText => Deal.EndsDateText;

        // The page colours these with theme brushes (see Deals.xaml), so the view model only says which tier applies
        public EndsUrgency Urgency { get; }
        public DealGrade Grade => Deal.Grade;

        /// <summary> "Also on GOG, Fanatical" when the same game is offered by other stores. </summary>
        public string AlsoOnText { get; }
        public bool HasAlsoOn => AlsoOnText.Length > 0;

        private Brush _coverBrush = Placeholder;
        /// <summary> The game's picture once it has loaded (a plain dark placeholder until then, or if it never loads). </summary>
        public Brush CoverBrush {
            get => _coverBrush;
            private set { _coverBrush = value; OnPropertyChanged(); }
        }

        private ImageSource? _logo;
        public ImageSource? Logo {
            get => _logo;
            private set { _logo = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLogo)); }
        }
        public bool HasLogo => _logo is not null;

        /// <summary> Stand-in for a missing logo: the store's first letter on a colour picked from its name. </summary>
        public string Initial { get; }
        public Brush BadgeBrush { get; }

        public ICommand OpenCommand { get; }

        public DealCardVM(DealGroup group, bool inLibrary, DateTimeOffset now, Action<DealCardVM> open) {
            Group = group;
            InLibrary = inLibrary;
            Urgency = group.Shown.Urgency(now);

            var others = group.Others.Select(o => o.ProviderName).Distinct().ToList();
            AlsoOnText = others.Count == 0 ? string.Empty : "Also on " + string.Join(", ", others);

            Initial = Deal.ProviderName.Length > 0 ? Deal.ProviderName[..1].ToUpperInvariant() : "?";

            // Same name -> same colour, so a store always looks the same
            int hue = Deal.ProviderName.Aggregate(17, (h, c) => unchecked(h * 31 + c)) & 0x7FFFFFFF;
            BadgeBrush = Freeze(new SolidColorBrush(FromHsv(hue % 360, 0.45, 0.65)));

            OpenCommand = new RelayCommand(_ => open(this));
        }

        public void SetImage(ImageSource image) {
            CoverBrush = Freeze(new ImageBrush(image) { Stretch = Stretch.UniformToFill });
        }

        public void SetLogo(ImageSource? logo) => Logo = logo;

        private static T Freeze<T>(T freezable) where T : System.Windows.Freezable {
            freezable.Freeze();
            return freezable;
        }

        private static Color FromHsv(double hue, double saturation, double value) {
            double c = value * saturation, x = c * (1 - Math.Abs(hue / 60 % 2 - 1)), m = value - c;
            (double r, double g, double b) = hue switch {
                < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x),
                < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x),
            };
            return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }
    }

    /// <summary> A store in the filter panel: a chip that can be switched on. </summary>
    internal class ProviderChipVM : BaseVM {
        private readonly Action _changed;

        public string Name { get; }
        public int Games { get; }

        private bool _isSelected;
        public bool IsSelected {
            get => _isSelected;
            set {
                if (_isSelected != value) {
                    _isSelected = value;
                    OnPropertyChanged();
                    _changed();
                }
            }
        }

        public ProviderChipVM(string name, int games, bool isSelected, Action changed) {
            Name = name;
            Games = games;
            _isSelected = isSelected;
            _changed = changed;
        }

        public void SetSelectedSilently(bool value) {
            if (_isSelected != value) {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    /// <summary> One button in the page bar. A page number of 0 is a "..." gap. </summary>
    internal class PageButtonVM : BaseVM {
        public int Page { get; }
        public bool IsGap => Page == 0;
        public bool IsCurrent { get; }
        public string Label => IsGap ? "…" : Page.ToString();
        public ICommand GoCommand { get; }

        public PageButtonVM(int page, bool isCurrent, Action<int> go) {
            Page = page;
            IsCurrent = isCurrent;
            GoCommand = new RelayCommand(_ => go(page), _ => !IsGap && !IsCurrent);
        }
    }

    /// <summary> The Deals page: free games and discounts from several sites. Goes online only when the page is opened. </summary>
    internal class DealsVM : BaseVM {

        private static readonly HttpClient Http = DealsService.CreateHttpClient();
        private static readonly SemaphoreSlim ImageSlots = new(4); // a few pictures at a time, so opening the page stays light
        private const int MaxCachedImages = 80;

        private readonly DealsService _service;
        private readonly Func<IReadOnlyList<string>> _libraryNames;
        private readonly Func<string, bool> _openLink;

        private IReadOnlyList<Deal> _deals = [];
        private IReadOnlyList<string> _owned = [];
        private List<DealGroup> _results = new();
        private CancellationTokenSource _imageLoads = new();
        private readonly Dictionary<string, ImageSource> _imageCache = new(); // pictures already fetched this session (memory only)
        private bool _rebuilding;

        /// <summary> The cards on the current page. </summary>
        public ObservableCollection<DealCardVM> Cards { get; } = new();

        /// <summary> Raised when the page changes so the view can scroll back to the top. </summary>
        public event EventHandler? PageChanged;

        #region Search
        private string _searchText = string.Empty;
        public string SearchText {
            get => _searchText;
            set {
                if (_searchText != value) {
                    _searchText = value;
                    OnPropertyChanged();
                    Refilter();
                }
            }
        }
        #endregion

        #region Sorting
        public IReadOnlyList<DealSortOption> SortOptions => DealsQuery.SortOptions;

        private DealSortOption _selectedSort = DealsQuery.SortOptions[0];
        public DealSortOption SelectedSort {
            get => _selectedSort;
            set {
                if (value is null || _selectedSort == value) return;
                _selectedSort = value;
                _isDescending = value.DefaultDescending; // each sort starts in its natural direction
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDescending));
                OnPropertyChanged(nameof(SortDirectionLabel));
                Refilter();
            }
        }

        private bool _isDescending = true;
        public bool IsDescending => _isDescending;
        public string SortDirectionLabel => SelectedSort.DirectionLabel(_isDescending);
        public ICommand ToggleSortDirectionCommand { get; }
        #endregion

        #region Price filter (All / Free / Paid)
        private PriceFilter _price = PriceFilter.All;
        public bool IsPriceAll { get => _price == PriceFilter.All; set { if (value) SetPrice(PriceFilter.All); } }
        public bool IsPriceFree { get => _price == PriceFilter.FreeOnly; set { if (value) SetPrice(PriceFilter.FreeOnly); } }
        public bool IsPricePaid { get => _price == PriceFilter.PaidOnly; set { if (value) SetPrice(PriceFilter.PaidOnly); } }

        private void SetPrice(PriceFilter price) {
            if (_price == price) return;
            _price = price;
            OnPropertyChanged(nameof(IsPriceAll));
            OnPropertyChanged(nameof(IsPriceFree));
            OnPropertyChanged(nameof(IsPricePaid));
            Refilter();
        }
        #endregion

        #region Filter panel: stores, hide owned
        private bool _isFilterPanelOpen;
        public bool IsFilterPanelOpen {
            get => _isFilterPanelOpen;
            set { if (_isFilterPanelOpen != value) { _isFilterPanelOpen = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<ProviderChipVM> ProviderChips { get; } = new();
        private readonly HashSet<string> _selectedProviders = new(StringComparer.OrdinalIgnoreCase);
        public bool HasProviders => ProviderChips.Count > 0;

        private bool _matchAllProviders = true;
        /// <summary> True (the default) = a game must be on offer at EVERY selected store (AND). False = any one of them (OR). </summary>
        public bool MatchAllProviders {
            get => _matchAllProviders;
            set {
                if (_matchAllProviders != value) {
                    _matchAllProviders = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MatchAnyProvider));
                    OnPropertyChanged(nameof(ProviderModeHint));
                    Refilter();
                }
            }
        }
        public bool MatchAnyProvider {
            get => !_matchAllProviders;
            set { if (value) MatchAllProviders = false; }
        }
        public string ProviderModeHint => _matchAllProviders
            ? "Only games on offer at every selected store (useful for comparing prices)."
            : "Games on offer at any of the selected stores.";

        private bool _hideOwned;
        /// <summary> Leave out games that are already in your library. </summary>
        public bool HideOwned {
            get => _hideOwned;
            set { if (_hideOwned != value) { _hideOwned = value; OnPropertyChanged(); Refilter(); } }
        }

        /// <summary> How many filters are switched on (shown on the Filters button). The price toggle counts too when it isn't "All". </summary>
        public int ActiveFilterCount => _selectedProviders.Count + (_price != PriceFilter.All ? 1 : 0) + (_hideOwned ? 1 : 0);

        public ICommand ToggleFilterPanelCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        #endregion

        #region Pagination
        public IReadOnlyList<int> PageSizes { get; } = [12, 24, 48];

        private int _pageSize = 24;
        public int PageSize {
            get => _pageSize;
            set {
                if (_pageSize != value && value > 0) {
                    _pageSize = value;
                    OnPropertyChanged();
                    _page = 1;
                    ShowPage();
                }
            }
        }

        private int _page = 1;
        public int Page => _page;

        private int _totalPages = 1;
        public int TotalPages => _totalPages;

        public ObservableCollection<PageButtonVM> PageButtons { get; } = new();
        public bool HasPages => _totalPages > 1;

        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }

        /// <summary> "Showing 1-24 of 53 deals". </summary>
        public string ResultsText { get; private set; } = string.Empty;

        public void GoToPage(int page) {
            if (page == _page) return;
            _page = page;
            ShowPage();
            PageChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region State shown by the page
        private bool _isLoading;
        public bool IsLoading {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowEmpty)); }
        }

        private string _statusText = string.Empty;
        /// <summary> "Updated 3 minutes ago", or "Loading deals…". </summary>
        public string StatusText {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        private string _note = string.Empty;
        /// <summary> A short warning when some (but not all) sources couldn't be reached, or a link couldn't be opened. </summary>
        public string Note {
            get => _note;
            private set { _note = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNote)); }
        }
        public bool HasNote => _note.Length > 0;

        private bool _isOffline;
        /// <summary> True when no source could be reached at all. </summary>
        public bool IsOffline {
            get => _isOffline;
            private set { _isOffline = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowEmpty)); }
        }

        /// <summary> The sources answered but nothing is left to show (no deals at all, or the filters remove everything). </summary>
        public bool ShowEmpty => !_isLoading && !_isOffline && Cards.Count == 0;

        /// <summary> True when the empty list is caused by filters or search (so a "Clear filters" hint makes sense). </summary>
        public bool EmptyBecauseOfFilters => _deals.Count > 0 && (ActiveFilterCount > 0 || !string.IsNullOrWhiteSpace(_searchText));
        #endregion

        public ICommand RefreshCommand { get; }

        public DealsVM(DealsService? service = null, Func<IReadOnlyList<string>>? libraryNames = null, Func<string, bool>? openLink = null) {
            _service = service ?? DealsService.Shared;
            _libraryNames = libraryNames ?? (() => GameMappingManager.LoadMappings().Select(m => m.Name).ToList());
            _openLink = openLink ?? DealLinks.Open;

            RefreshCommand = new RelayCommand(async _ => await LoadAsync(force: true), _ => !_isLoading);
            ToggleSortDirectionCommand = new RelayCommand(_ => {
                _isDescending = !_isDescending;
                OnPropertyChanged(nameof(IsDescending));
                OnPropertyChanged(nameof(SortDirectionLabel));
                Refilter();
            });
            ToggleFilterPanelCommand = new RelayCommand(_ => IsFilterPanelOpen = !IsFilterPanelOpen);
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            PreviousPageCommand = new RelayCommand(_ => GoToPage(_page - 1), _ => _page > 1);
            NextPageCommand = new RelayCommand(_ => GoToPage(_page + 1), _ => _page < _totalPages);
        }

        /// <summary> Fetches (or reuses the remembered answer for a while) and shows the deals. Called when the page opens. </summary>
        public async Task LoadAsync(bool force) {
            IsLoading = true;
            Note = string.Empty;
            StatusText = "Loading deals…";
            if (force) _imageCache.Clear();

            try {
                var result = await _service.LoadAsync(force);
                Show(result);
            }
            catch (Exception ex) {
                // LoadAsync isolates each source, so reaching here means something unexpected; show it instead of crashing
                IsOffline = true;
                StatusText = string.Empty;
                Note = $"Something went wrong loading deals: {ex.Message}";
            }
            finally {
                IsLoading = false;
            }
        }

        private void Show(DealsResult result) {
            IsOffline = result.AllFailed;
            _deals = result.Deals;
            _owned = _libraryNames();

            if (result.AllFailed) {
                StatusText = string.Empty;
            }
            else {
                StatusText = $"Updated {RelativeTime.Format(result.FetchedAt.LocalDateTime)}";
                if (result.Failed.Count > 0) {
                    Note = $"Couldn't reach {string.Join(", ", result.Failed)}, so those deals are missing. Refresh to try again.";
                }
            }

            RebuildProviderChips();
            Refilter();
        }

        // The store chips come from what was actually found; selections are kept for stores that are still there
        private void RebuildProviderChips() {
            _rebuilding = true;
            ProviderChips.Clear();
            var counts = DealsQuery.ProviderCounts(_deals);
            _selectedProviders.IntersectWith(counts.Select(c => c.Name));
            foreach (var (name, games) in counts) {
                ProviderChips.Add(new ProviderChipVM(name, games, _selectedProviders.Contains(name), () => OnProviderChanged(name)));
            }
            _rebuilding = false;
            OnPropertyChanged(nameof(HasProviders));
        }

        private void OnProviderChanged(string name) {
            if (_rebuilding) return;
            var chip = ProviderChips.First(c => c.Name == name);
            if (chip.IsSelected) _selectedProviders.Add(name); else _selectedProviders.Remove(name);
            Refilter();
        }

        private void ClearFilters() {
            _rebuilding = true;
            foreach (var chip in ProviderChips) chip.SetSelectedSilently(false);
            _selectedProviders.Clear();
            _matchAllProviders = true;
            _hideOwned = false;
            _searchText = string.Empty;
            _price = PriceFilter.All;
            _rebuilding = false;

            foreach (string property in new[] { nameof(MatchAllProviders), nameof(MatchAnyProvider), nameof(ProviderModeHint),
                                                nameof(HideOwned), nameof(SearchText), nameof(IsPriceAll), nameof(IsPriceFree), nameof(IsPricePaid) }) {
                OnPropertyChanged(property);
            }
            Refilter();
        }

        /// <summary> Re-runs the search, filters and sort over every deal, then shows page 1. </summary>
        private void Refilter() {
            if (_rebuilding) return;

            var query = new DealsQuery(_searchText, _selectedProviders, _matchAllProviders, _price, _hideOwned, _selectedSort.Key, _isDescending);
            _results = query.Apply(_deals, title => _owned.Any(name => Deal.SameGame(name, title)));
            _page = 1;
            OnPropertyChanged(nameof(ActiveFilterCount));
            ShowPage();
        }

        // Builds the cards for the current page and starts loading their pictures
        private void ShowPage() {
            _imageLoads.Cancel(); // pictures still loading for the previous page aren't needed any more
            _imageLoads = new CancellationTokenSource();

            var (items, page, totalPages) = Pager.Slice(_results, _page, _pageSize);
            _page = page;
            _totalPages = totalPages;

            var now = DateTimeOffset.UtcNow;
            Cards.Clear();
            foreach (var group in items) {
                Cards.Add(new DealCardVM(group, _owned.Any(name => Deal.SameGame(name, group.Title)), now, Open));
            }

            PageButtons.Clear();
            foreach (int number in Pager.ButtonNumbers(_page, _totalPages)) {
                PageButtons.Add(new PageButtonVM(number, number == _page, GoToPage));
            }

            int first = _results.Count == 0 ? 0 : (_page - 1) * _pageSize + 1;
            ResultsText = _results.Count == 0 ? string.Empty : $"Showing {first}-{first + items.Count - 1} of {_results.Count} deals";

            OnPropertyChanged(nameof(Page));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(HasPages));
            OnPropertyChanged(nameof(ResultsText));
            OnPropertyChanged(nameof(ShowEmpty));
            OnPropertyChanged(nameof(EmptyBecauseOfFilters));

            _ = LoadPicturesAsync(Cards.ToList(), _imageLoads.Token);
        }

        private void Open(DealCardVM card) {
            if (!_openLink(card.Deal.Url)) {
                Note = "That link couldn't be opened.";
            }
        }

        // Pictures and logos are fetched live and only kept in memory
        private async Task LoadPicturesAsync(IReadOnlyList<DealCardVM> cards, CancellationToken ct) {
            var logoTasks = cards.Select(c => c.Deal.ProviderHost).Where(h => h.Length > 0).Distinct()
                .ToDictionary(host => host, host => DealImages.LoadLogoAsync(Http, host));

            var work = cards.Select(async card => {
                try {
                    if (logoTasks.TryGetValue(card.Deal.ProviderHost, out var logo)) {
                        card.SetLogo(await logo);
                    }

                    string? url = card.Deal.ImageUrl;
                    if (url is not null && _imageCache.TryGetValue(url, out var cached)) {
                        card.SetImage(cached);
                        return;
                    }

                    await ImageSlots.WaitAsync(ct);
                    try {
                        var image = await DealImages.LoadAsync(Http, url, decodeWidth: 400, ct: ct);
                        if (image is not null && url is not null) {
                            if (_imageCache.Count >= MaxCachedImages) _imageCache.Clear();
                            _imageCache[url] = image;
                            if (!ct.IsCancellationRequested) card.SetImage(image);
                        }
                    }
                    finally {
                        ImageSlots.Release();
                    }
                }
                catch (OperationCanceledException) {
                    // the page changed while this card's picture was loading
                }
            });
            await Task.WhenAll(work);
        }
    }
}
