using Microsoft.Win32;

namespace Game_Launcher.Helpers {
    /// <summary> Shows Windows' file picker, filtered to .exe files. Kept in one place so view models can be given a fake picker in tests. </summary>
    public static class ExecutablePicker {
        /// <returns> The chosen .exe's full path, or null if the user cancelled.</returns>
        public static string? Pick(string title) {
            var dlg = new OpenFileDialog { Title = title, Filter = "Programs (*.exe)|*.exe", CheckFileExists = true };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }
}
