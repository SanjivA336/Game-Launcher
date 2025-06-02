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


        public MainWindowVM(Action minimize, Action maximize, Action close) {
            MinimizeCommand = new RelayCommand(_ => minimize());
            MaximizeCommand = new RelayCommand(_ => maximize());
            CloseCommand = new RelayCommand(_ => close());
        }
    }
}
