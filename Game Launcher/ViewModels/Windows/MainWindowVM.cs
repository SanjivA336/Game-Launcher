using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Game_Launcher.ViewModels.Windows {
    public class MainWindowVM : BaseVM {

        // Commands for window actions
        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand CloseCommand { get; }

        // Property for navigation/content
        private object? _currentPage;
        public object? CurrentPage {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); }
        }

        // Window state tracking (optional, for maximize button icon)
        private bool _isMaximized;
        public bool IsMaximized {
            get => _isMaximized;
            set { _isMaximized = value; OnPropertyChanged(); }
        }

        public MainWindowVM(Action minimize, Action maximize, Action close) {
            MinimizeCommand = new RelayCommand(_ => minimize());
            MaximizeCommand = new RelayCommand(_ => maximize());
            CloseCommand = new RelayCommand(_ => close());

            // Set default page
            CurrentPage = new Views.Pages.Library(); // Or use a ViewModel for navigation
        }
    }
}
