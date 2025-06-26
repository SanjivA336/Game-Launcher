using Game_Launcher.Views.Windows;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Windows {
    public class MainWindowVM : BaseVM {

        // Commands for window actions
        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand CloseCommand { get; }

        public ICommand OpenPreferencesCommand { get; }

        public MainWindowVM(Action minimize, Action maximize, Action close) {
            MinimizeCommand = new RelayCommand(_ => minimize());
            MaximizeCommand = new RelayCommand(_ => maximize());
            CloseCommand = new RelayCommand(_ => close());

            OpenPreferencesCommand = new RelayCommand(_ => OpenPreferences());
        }

        private void OpenPreferences() {
            var preferencesWindow = new PreferencesWindow();
            preferencesWindow.Owner = App.Current.MainWindow;
            preferencesWindow.ShowDialog();


        }
    }
}