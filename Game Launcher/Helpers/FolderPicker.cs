using Microsoft.Win32;

namespace Game_Launcher.Helpers {
    /// <summary> Shows Windows' folder picker. Kept in one place so view models can be given a fake picker in tests. </summary>
    public static class FolderPicker {
        /// <returns> The chosen folder, or null if the user cancelled.</returns>
        public static string? Pick(string title) {
            var dlg = new OpenFolderDialog { Title = title, Multiselect = false };
            return dlg.ShowDialog() == true ? dlg.FolderName : null;
        }
    }
}
