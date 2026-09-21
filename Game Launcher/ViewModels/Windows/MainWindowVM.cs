using Game_Launcher.Models;
using Game_Launcher.Services;
using Game_Launcher.Views.Windows;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Windows {
    public class MainWindowVM : BaseVM {

        // Commands for window actions
        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand CloseCommand { get; }

        public ICommand OpenPreferencesCommand { get; }

        /// <summary> Clicking the Nexus logo/name in the sidebar: goes to the Home page. </summary>
        public ICommand GoHomeCommand { get; }

        /// <summary> The sidebar entries (Library, Apps, Deals...), built from the page list. </summary>
        public ObservableCollection<NavItemVM> NavItems { get; } = new();

        private bool _isSidebarCollapsed;
        /// <summary> True while the sidebar is shrunk to a slim strip of icons. (Only remembered until Nexus closes.) </summary>
        public bool IsSidebarCollapsed {
            get => _isSidebarCollapsed;
            set {
                if (_isSidebarCollapsed != value) {
                    _isSidebarCollapsed = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ToggleSidebarCommand { get; }

        private readonly Action _preferencesClosed;

        public MainWindowVM(Action minimize, Action maximize, Action close, IEnumerable<NavPage> pages,
                            Action<string> navigate, Action goHome, Action preferencesClosed) {
            _preferencesClosed = preferencesClosed;

            MinimizeCommand = new RelayCommand(_ => minimize());
            MaximizeCommand = new RelayCommand(_ => maximize());
            CloseCommand = new RelayCommand(_ => close());
            GoHomeCommand = new RelayCommand(_ => goHome());
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);

            OpenPreferencesCommand = new RelayCommand(_ => OpenPreferences());

            foreach (var page in pages.Where(p => p.ShowInSidebar)) {
                NavItems.Add(new NavItemVM(page.Id, page.Title, page.Glyph, navigate));
            }
        }

        /// <summary> Marks the sidebar entry for the given page as the selected one (null = none, e.g. on Home). </summary>
        public void Select(string? pageId) {
            foreach (var item in NavItems) {
                item.IsSelected = item.Id == pageId;
            }
        }

        /// <summary> Shows or hides optional sidebar entries according to the saved settings (e.g. the Deals switch). </summary>
        public void ApplyPreferences(IEnumerable<NavPage> pages, Preferences prefs) {
            foreach (var page in pages) {
                var item = NavItems.FirstOrDefault(i => i.Id == page.Id);
                if (item is not null) {
                    item.IsVisible = page.IsEnabled?.Invoke(prefs) ?? true;
                }
            }
        }

        private void OpenPreferences() {
            var preferencesWindow = new PreferencesWindow();
            preferencesWindow.Owner = App.Current.MainWindow;
            preferencesWindow.ShowDialog();

            // ShowDialog waits until the window closes; settings (like the cover API key) may have changed
            _preferencesClosed();
        }
    }
}
