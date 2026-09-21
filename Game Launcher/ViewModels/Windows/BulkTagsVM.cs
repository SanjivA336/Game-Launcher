using Game_Launcher.Models;
using Game_Launcher.Services;
using Game_Launcher.Services.Steam;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Windows {
    /// <summary>
    /// The Settings button that adds Steam's suggested tags to every game. It runs in the background with a progress line and a Cancel button,
    /// then lists which games were updated and which couldn't be found on Steam. It only ever ADDS tags.
    /// </summary>
    internal class BulkTagsVM : BaseVM {

        private static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(1500); // gentle on Steam: about 40 seconds for 27 games

        private readonly Func<IReadOnlyList<GameMapping>> _games;
        private readonly Func<GameMapping, CancellationToken, Task<TagSuggestion>> _suggest;
        private readonly TimeSpan _delay;
        private CancellationTokenSource? _cts;

        /// <summary> "Name: Solo, Action" for each game that got new tags. </summary>
        public ObservableCollection<string> Updated { get; } = new();
        public ObservableCollection<string> NotFound { get; } = new();
        /// <summary> Games that couldn't be checked because Steam couldn't be reached. </summary>
        public ObservableCollection<string> Failed { get; } = new();

        private bool _isRunning;
        public bool IsRunning {
            get => _isRunning;
            private set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStart)); }
        }
        public bool CanStart => !_isRunning;

        private string _progressText = string.Empty;
        /// <summary> "Looking up 12 of 27…", or how the run ended. </summary>
        public string ProgressText {
            get => _progressText;
            private set { _progressText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProgress)); }
        }
        public bool HasProgress => _progressText.Length > 0;

        private int _alreadyHad;
        public int AlreadyHadCount {
            get => _alreadyHad;
            private set { _alreadyHad = value; OnPropertyChanged(); OnPropertyChanged(nameof(AlreadyHadText)); }
        }
        public string AlreadyHadText => _alreadyHad == 1 ? "1 game already had these tags." : $"{_alreadyHad} games already had these tags.";

        private bool _hasResults;
        public bool HasResults {
            get => _hasResults;
            private set { _hasResults = value; OnPropertyChanged(); }
        }

        public string UpdatedTitle => $"Updated ({Updated.Count})";
        public string NotFoundTitle => $"Not found on Steam ({NotFound.Count})";
        public string FailedTitle => $"Couldn't check ({Failed.Count})";
        public bool HasUpdated => Updated.Count > 0;
        public bool HasNotFound => NotFound.Count > 0;
        public bool HasFailed => Failed.Count > 0;
        public bool HasAlreadyHad => AlreadyHadCount > 0;

        public ICommand RunCommand { get; }
        public ICommand CancelCommand { get; }

        /// <param name="games"> The games to look up (all of them, by default).</param>
        /// <param name="suggest"> Looks one game up on Steam. Replaceable in tests.</param>
        /// <param name="delay"> Pause between lookups. Tests pass zero.</param>
        public BulkTagsVM(Func<IReadOnlyList<GameMapping>>? games = null, Func<GameMapping, CancellationToken, Task<TagSuggestion>>? suggest = null, TimeSpan? delay = null) {
            _games = games ?? (() => GameMappingManager.LoadMappings());
            _suggest = suggest ?? ((g, ct) => SteamTagSuggester.Shared.SuggestAsync(g.Name, g.DirPathRaw, ct));
            _delay = delay ?? DefaultDelay;
            RunCommand = new RelayCommand(async _ => await RunAsync(), _ => CanStart);
            CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsRunning);
        }

        public async Task RunAsync() {
            var games = _games().OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (games.Count == 0) {
                ProgressText = "There are no games yet.";
                return;
            }

            ClearResults();
            IsRunning = true;
            _cts = new CancellationTokenSource();
            try {
                var result = await BulkTagSuggestions.RunAsync(games, _suggest,
                    (done, total) => ProgressText = done >= total ? "Done." : $"Looking up {done + 1} of {total}…", _delay, _cts.Token);

                foreach (var change in result.Updated) Updated.Add($"{change.Game}: {string.Join(", ", change.Added)}");
                foreach (string name in result.NotFound) NotFound.Add(name);
                foreach (string name in result.Failed) Failed.Add(name);
                AlreadyHadCount = result.AlreadyHad.Count;

                ProgressText = result.Failed.Count > 0 && result.Updated.Count == 0 && result.NotFound.Count == 0 && result.AlreadyHad.Count == 0
                    ? "Couldn't reach Steam."
                    : "Done.";
                HasResults = true;
                RaiseListChanged();
            }
            catch (OperationCanceledException) {
                ProgressText = "Cancelled. Tags already added were kept.";
            }
            finally {
                IsRunning = false;
                _cts.Dispose();
                _cts = null;
            }
        }

        private void ClearResults() {
            Updated.Clear();
            NotFound.Clear();
            Failed.Clear();
            AlreadyHadCount = 0;
            HasResults = false;
            ProgressText = string.Empty;
            RaiseListChanged();
        }

        private void RaiseListChanged() {
            foreach (string property in new[] { nameof(UpdatedTitle), nameof(NotFoundTitle), nameof(FailedTitle), nameof(HasUpdated), nameof(HasNotFound), nameof(HasFailed), nameof(HasAlreadyHad) }) {
                OnPropertyChanged(property);
            }
        }
    }
}
