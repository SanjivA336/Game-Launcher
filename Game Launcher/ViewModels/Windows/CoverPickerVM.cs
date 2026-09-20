using Game_Launcher.Models;
using Game_Launcher.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Game_Launcher.ViewModels.Windows {
    /// <summary> One cover thumbnail in the results grid. </summary>
    internal class CoverChoiceVM {
        public CoverOption Option { get; }
        public string ThumbUrl => Option.ThumbUrl;
        public string Label => $"{Option.Width}×{Option.Height}" + (string.IsNullOrEmpty(Option.Style) ? "" : $" · {Option.Style}");
        public string Author => Option.Author;

        public CoverChoiceVM(CoverOption option) {
            Option = option;
        }

        // Screen readers read a list item's name from ToString()
        public override string ToString() => $"Cover {Label}" + (string.IsNullOrEmpty(Author) ? "" : $" by {Author}");
    }

    /// <summary>
    /// The "change cover" window: either find a cover on SteamGridDB (search, pick a game, pick a cover) or use an image from your computer.
    /// Applying saves the cover straight away as a custom cover, so automatic lookups never replace it.
    /// </summary>
    internal class CoverPickerVM : BaseVM {
        private readonly GameMapping _game;
        private readonly Action<bool> _close;
        private readonly SteamGridDbClient? _client;
        private CancellationTokenSource? _cts; // cancels a search/load still in flight when a newer one starts or the window closes

        public string GameName => _game.Name;

        /// <summary> False when no API key is set: the SteamGridDB half can't work, but uploading still can. </summary>
        public bool HasApiKey => _client is not null;
        public bool NoApiKey => !HasApiKey;

        #region Mode (SteamGridDB / Upload)
        private bool _isSteamGridDbMode = true;
        public bool IsSteamGridDbMode {
            get => _isSteamGridDbMode;
            set {
                if (_isSteamGridDbMode != value) {
                    _isSteamGridDbMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsUploadMode));
                    StatusText = string.Empty;
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary> The opposite of IsSteamGridDbMode, so the second radio button can bind to it without a converter. </summary>
        public bool IsUploadMode {
            get => !_isSteamGridDbMode;
            set {
                if (value) {
                    IsSteamGridDbMode = false;
                }
            }
        }
        #endregion

        #region SteamGridDB search
        private string _searchText;
        public string SearchText {
            get => _searchText;
            set {
                if (_searchText != value) {
                    _searchText = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<GameSearchResult> GameResults { get; } = new();

        private GameSearchResult? _selectedGameResult;
        public GameSearchResult? SelectedGameResult {
            get => _selectedGameResult;
            set {
                if (_selectedGameResult != value) {
                    _selectedGameResult = value;
                    OnPropertyChanged();
                    if (value is not null) {
                        _ = LoadCoversAsync(value, page: 0);
                    }
                }
            }
        }

        public ObservableCollection<CoverChoiceVM> Covers { get; } = new();

        private CoverChoiceVM? _selectedCover;
        public CoverChoiceVM? SelectedCover {
            get => _selectedCover;
            set {
                if (_selectedCover != value) {
                    _selectedCover = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested(); // re-check whether "Use this cover" can be clicked
                }
            }
        }

        private CoverPage? _lastPage;
        public bool CanLoadMore => _lastPage?.HasMore == true;

        public ICommand SearchCommand { get; }
        public ICommand LoadMoreCommand { get; }
        #endregion

        #region Upload
        private string? _uploadPath;

        private Brush? _uploadPreview;
        public Brush? UploadPreview {
            get => _uploadPreview;
            private set {
                _uploadPreview = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUploadPreview));
            }
        }

        public bool HasUploadPreview => _uploadPreview is not null;

        private string _uploadFileText = "No image chosen yet";
        public string UploadFileText {
            get => _uploadFileText;
            private set {
                _uploadFileText = value;
                OnPropertyChanged();
            }
        }

        public ICommand ChooseFileCommand { get; }
        #endregion

        private string _statusText = string.Empty;
        public string StatusText {
            get => _statusText;
            private set {
                if (_statusText != value) {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy {
            get => _isBusy;
            private set {
                if (_isBusy != value) {
                    _isBusy = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary> True once a cover was applied (the window reports this so the options page can refresh). </summary>
        public bool Applied { get; private set; }

        public bool CanApply => !IsBusy && (IsSteamGridDbMode ? SelectedCover is not null : _uploadPath is not null);

        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }

        public CoverPickerVM(GameMapping game, Action<bool> close) {
            _game = game;
            _close = close;
            _client = CoverService.CreateClient();
            _searchText = game.Name;

            SearchCommand = new RelayCommand(_ => _ = SearchAsync(), _ => HasApiKey && !string.IsNullOrWhiteSpace(SearchText));
            LoadMoreCommand = new RelayCommand(_ => _ = LoadMoreAsync(), _ => CanLoadMore && !IsBusy);
            ChooseFileCommand = new RelayCommand(_ => ChooseFile());
            ApplyCommand = new RelayCommand(_ => _ = ApplyAsync(), _ => CanApply);
            CancelCommand = new RelayCommand(_ => _close(false));

            // Start with the game's own name already searched, so results are on screen as the window opens
            if (HasApiKey) {
                _ = SearchAsync();
            }
        }

        /// <summary> Stops anything still running (called when the window closes). </summary>
        public void Cancel() {
            _cts?.Cancel();
        }

        private CancellationToken NewOperation() {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        private async Task SearchAsync() {
            if (_client is null) {
                return;
            }

            var ct = NewOperation();
            IsBusy = true;
            StatusText = "Searching…";
            GameResults.Clear();
            Covers.Clear();
            _lastPage = null;
            SelectedCover = null;
            _selectedGameResult = null;
            OnPropertyChanged(nameof(SelectedGameResult));
            OnPropertyChanged(nameof(CanLoadMore));

            try {
                var search = await _client.SearchGamesAsync(SearchText, ct);
                if (search.Outcome != CoverOutcome.Found) {
                    StatusText = Describe(search.Outcome, search.Detail, "No games found. Try different words.");
                    return;
                }

                foreach (var game in search.Value!) {
                    GameResults.Add(game);
                }

                StatusText = string.Empty;

                // Select the best match for you: a title equal to what you typed, otherwise the site's first result
                string wanted = SteamGridDbClient.Squash(SearchText);
                SelectedGameResult = GameResults.FirstOrDefault(g => SteamGridDbClient.Squash(g.Name) == wanted) ?? GameResults[0];
            }
            catch (OperationCanceledException) {
                // A newer search replaced this one; it will report its own status
            }
            finally {
                if (!ct.IsCancellationRequested) {
                    IsBusy = false;
                }
            }
        }

        private async Task LoadCoversAsync(GameSearchResult game, int page) {
            if (_client is null) {
                return;
            }

            var ct = NewOperation();
            IsBusy = true;
            StatusText = "Loading covers…";
            if (page == 0) {
                Covers.Clear();
                SelectedCover = null;
                _lastPage = null;
            }
            OnPropertyChanged(nameof(CanLoadMore));

            try {
                var result = await _client.GetCoversAsync(game.Id, page, ct);
                if (result.Outcome != CoverOutcome.Found) {
                    StatusText = Describe(result.Outcome, result.Detail, $"No portrait covers for \"{game.Name}\". Pick another game in the list.");
                    return;
                }

                foreach (var cover in result.Value!.Covers) {
                    Covers.Add(new CoverChoiceVM(cover));
                }

                _lastPage = result.Value;
                StatusText = string.Empty;
                OnPropertyChanged(nameof(CanLoadMore));
            }
            catch (OperationCanceledException) {
                // Replaced by a newer request
            }
            finally {
                if (!ct.IsCancellationRequested) {
                    IsBusy = false;
                }
            }
        }

        private Task LoadMoreAsync() {
            return _selectedGameResult is null || _lastPage is null
                ? Task.CompletedTask
                : LoadCoversAsync(_selectedGameResult, _lastPage.Page + 1);
        }

        private static string Describe(CoverOutcome outcome, string? detail, string notFoundText) {
            string reason = string.IsNullOrEmpty(detail) ? string.Empty : $" ({detail})";
            return outcome switch {
                CoverOutcome.NotFound => notFoundText,
                CoverOutcome.InvalidKey => $"SteamGridDB rejected your API key{reason}. Check it in Settings.",
                _ => $"Couldn't reach SteamGridDB{reason}.",
            };
        }

        private void ChooseFile() {
            var dialog = new OpenFileDialog {
                Title = "Choose a cover image",
                Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files (*.*)|*.*",
            };

            if (dialog.ShowDialog() != true) {
                return;
            }

            try {
                var preview = new BitmapImage();
                preview.BeginInit();
                preview.CacheOption = BitmapCacheOption.OnLoad; // load now, so the chosen file isn't left locked
                preview.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                preview.DecodePixelWidth = 360;
                preview.UriSource = new Uri(dialog.FileName);
                preview.EndInit();
                preview.Freeze();

                var brush = new ImageBrush(preview) { Stretch = Stretch.UniformToFill };
                brush.Freeze();

                _uploadPath = dialog.FileName;
                UploadPreview = brush;
                UploadFileText = Path.GetFileName(dialog.FileName);
                StatusText = string.Empty;
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or IOException or ArgumentException or UriFormatException) {
                _uploadPath = null;
                UploadPreview = null;
                UploadFileText = "No image chosen yet";
                StatusText = "That file couldn't be read as an image. Try a PNG, JPG, BMP, GIF or TIFF.";
            }
        }

        private async Task ApplyAsync() {
            var ct = NewOperation();
            IsBusy = true;

            try {
                string fileName;
                string? sourceTitle;

                if (IsSteamGridDbMode) {
                    var cover = SelectedCover!.Option;
                    StatusText = "Downloading the cover…";

                    var image = await _client!.DownloadAsync(cover.Url, ct);
                    if (image.Outcome != CoverOutcome.Found) {
                        StatusText = Describe(image.Outcome, image.Detail, "That cover is no longer available.");
                        return;
                    }

                    fileName = await CoverStore.SaveAsync(_game, image.Value!, cover.Extension, custom: true, ct);
                    sourceTitle = SelectedGameResult?.Name;
                }
                else {
                    StatusText = "Saving your image…";
                    fileName = await CoverStore.ImportAsync(_game, _uploadPath!, ct);
                    sourceTitle = null;
                }

                if (GameMappingManager.SetCustomCover(_game.DirPathRaw, fileName, sourceTitle) is null) {
                    StatusText = "This game is no longer in the library.";
                    return;
                }

                Applied = true;
                _close(true);
            }
            catch (InvalidOperationException ex) {
                StatusText = ex.Message;
            }
            catch (OperationCanceledException) {
                // Window closed or replaced by a newer operation
            }
            finally {
                if (!ct.IsCancellationRequested) {
                    IsBusy = false;
                }
            }
        }
    }
}
