using System.Windows;
using System.Windows.Controls;

namespace Game_Launcher.Views {
    /// <summary> The Apps tab of Settings. Its data (an AppsVM) is supplied by the Settings window. </summary>
    public partial class AppsPanel : UserControl {

        public AppsPanel() {
            InitializeComponent();
        }

        private void Apps_DragOver(object sender, DragEventArgs e) {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Apps_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && DataContext is ViewModels.Pages.AppsVM vm) {
                foreach (string path in paths) {
                    vm.AddFromPath(path);
                }
            }
        }
    }
}
