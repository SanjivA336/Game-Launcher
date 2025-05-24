using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Game_Launcher.ViewModels {
    public class BaseVM : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
