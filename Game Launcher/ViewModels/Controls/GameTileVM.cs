using Game_Launcher.Helpers;
using Game_Launcher.Models;
using Game_Launcher.Services;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;

namespace Game_Launcher.ViewModels.Controls {
    internal class GameTileVM : BaseVM {
        private const int MaxVisibleTags = 3;

        public GameMapping Game { get; }

        public bool IsInstalled => Game.HasTag("Installed");

        private Brush _coverBrush = null!;
        public Brush CoverBrush {
            get => _coverBrush;
            private set {
                _coverBrush = value;
                OnPropertyChanged();
            }
        }

        private bool _hasCover;
        /// <summary> True when a real downloaded cover is showing (the placeholder's big letter is hidden then). </summary>
        public bool HasCover {
            get => _hasCover;
            private set {
                if (_hasCover != value) {
                    _hasCover = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary> Re-reads the game's cover; called when a cover finishes downloading while the library is open. </summary>
        public void RefreshCover() {
            CoverBrush = CoverArt.CreateCoverBrush(Game.DirPathRaw, Game.CoverPath, out bool hasImage);
            HasCover = hasImage;
        }

        /// <summary> The tag chips drawn on the card: up to three, then a "+N" chip for the rest. </summary>
        public IReadOnlyList<string> VisibleTags { get; }

        public ICommand LaunchCommand { get; }
        public ICommand OptionsCommand { get; }

        public event EventHandler? OptionsRequested;

        public GameTileVM(GameMapping game) {
            Game = game;
            RefreshCover();
            VisibleTags = BuildVisibleTags(game);
            LaunchCommand = new RelayCommand(_ => LaunchGame());
            OptionsCommand = new RelayCommand(_ => OptionsRequested?.Invoke(this, EventArgs.Empty));
        }

        private static IReadOnlyList<string> BuildVisibleTags(GameMapping game) {
            var tags = TagCatalog.AssignedTagsOf(game).ToList();
            if (tags.Count <= MaxVisibleTags) {
                return tags;
            }

            var shown = tags.Take(MaxVisibleTags).ToList();
            shown.Add($"+{tags.Count - MaxVisibleTags}");
            return shown;
        }

        private void LaunchGame() {
            if (!Game.LaunchExecutable(out string? error)) {
                Debug.WriteLine($"Error launching game: {error}");
                return;
            }

            Game.LastPlayed = DateTime.Now;
            Game.LaunchCount++;
            if (!GameMappingManager.RecordLaunch(Game, out string? recordError)) {
                Debug.WriteLine($"Error recording launch: {recordError}");
            }
        }
    }
}
