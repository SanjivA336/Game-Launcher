using System.Windows.Input;

namespace Game_Launcher.ViewModels.Windows {
    /// <summary> One entry in the sidebar. Holds the label, the icon and whether it's the page currently showing. </summary>
    public class NavItemVM : BaseVM {
        public string Id { get; }
        public string Title { get; }
        public string Glyph { get; }
        public ICommand Command { get; }

        private bool _isSelected;
        public bool IsSelected {
            get => _isSelected;
            set {
                if (_isSelected != value) {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isVisible = true;
        public bool IsVisible {
            get => _isVisible;
            set {
                if (_isVisible != value) {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public NavItemVM(string id, string title, string glyph, Action<string> navigate) {
            Id = id;
            Title = title;
            Glyph = glyph;
            Command = new RelayCommand(_ => navigate(id));
        }
    }
}
