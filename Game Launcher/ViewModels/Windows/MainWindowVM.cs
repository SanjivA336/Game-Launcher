using Game_Launcher.Views.Windows;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Windows {
    public class MainWindowVM : BaseVM {

        // Commands for window actions
        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand CloseCommand { get; }

        public ICommand OpenPreferencesCommand { get; }
        public ICommand ShowLibraryCommand { get; }

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

        public MainWindowVM(Action minimize, Action maximize, Action close, Action showLibrary, Action preferencesClosed) {
            _preferencesClosed = preferencesClosed;

            MinimizeCommand = new RelayCommand(_ => minimize());
            MaximizeCommand = new RelayCommand(_ => maximize());
            CloseCommand = new RelayCommand(_ => close());
            ShowLibraryCommand = new RelayCommand(_ => showLibrary());
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);

            OpenPreferencesCommand = new RelayCommand(_ => OpenPreferences());
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