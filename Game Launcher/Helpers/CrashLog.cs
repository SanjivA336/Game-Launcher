using System.IO;

namespace Game_Launcher.Helpers {
    /// <summary>
    /// A note of what went wrong when something unexpected happens, kept in Nexus's own data folder (crash.log) so a problem
    /// can be described or reported instead of the app just disappearing. Writing it can never cause a second failure.
    /// </summary>
    public static class CrashLog {
        private const long MaxBytes = 256 * 1024; // when it grows past this, start over: it is only the recent problems that matter

        public static string FilePath => Path.Combine(AppPaths.DataDirectory, "crash.log");

        /// <summary> Adds an entry (time, then the full error with where it happened). Never throws. </summary>
        public static void Write(Exception? error) {
            try {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes) {
                    file.Delete();
                }
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {error?.ToString() ?? "Unknown error"}{Environment.NewLine}{Environment.NewLine}");
            }
            catch {
                // the log is a convenience: if it can't be written (full disk, locked file) there is nothing more to do
            }
        }
    }
}
