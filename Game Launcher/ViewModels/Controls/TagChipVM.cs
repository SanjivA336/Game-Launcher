using Game_Launcher.Models;

namespace Game_Launcher.ViewModels.Controls {
    /// <summary>
    /// One clickable tag chip. The library's filter panel uses it ("is this tag part of the filter?") and so does the
    /// game options page ("does this game have this tag?"), so both share one small class.
    /// </summary>
    internal class TagChipVM : BaseVM {
        private readonly Action<TagChipVM>? _onSelectionChanged;

        public TagDefinition Definition { get; }
        public string Name => Definition.Name;
        public string Description => Definition.Description;

        private bool _isSelected;
        public bool IsSelected {
            get => _isSelected;
            set {
                if (_isSelected != value) {
                    _isSelected = value;
                    OnPropertyChanged();
                    _onSelectionChanged?.Invoke(this);
                }
            }
        }

        private int _count;
        /// <summary> How many games in the library have this tag (shown next to the name in the filter panel). </summary>
        public int Count {
            get => _count;
            set {
                if (_count != value) {
                    _count = value;
                    OnPropertyChanged();
                }
            }
        }

        public TagChipVM(TagDefinition definition, bool isSelected, Action<TagChipVM>? onSelectionChanged) {
            Definition = definition;
            _isSelected = isSelected;
            _onSelectionChanged = onSelectionChanged;
        }

        /// <summary> Changes the selection without triggering the change callback (used when clearing many chips at once). </summary>
        public void SetSelectedSilently(bool value) {
            if (_isSelected != value) {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    /// <summary> A titled row of tag chips, e.g. "Players": Solo, Co-op, Competitive. </summary>
    internal class TagGroupVM : BaseVM {
        public string Name { get; }
        public IReadOnlyList<TagChipVM> Tags { get; }

        private bool _isVisible = true;
        /// <summary> False hides the whole row (the options page hides "Completion" until a game is marked as a Campaign game). </summary>
        public bool IsVisible {
            get => _isVisible;
            set {
                if (_isVisible != value) {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public TagGroupVM(string name, IReadOnlyList<TagChipVM> tags) {
            Name = name;
            Tags = tags;
        }
    }
}
