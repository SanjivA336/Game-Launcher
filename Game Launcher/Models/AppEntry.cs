using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;

namespace Game_Launcher.Models {
    /// <summary> A launcher or other program shown on the Apps page (Steam, Discord, ...). It is just a saved shortcut. </summary>
    public class AppEntry {
        /// <summary> Stable identity, so renaming or re-pointing an app never confuses it with another. </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        /// <summary> Which of Nexus's known launchers this links (e.g. "steam"); null for a custom app the user added. </summary>
        public string? KnownId { get; set; }

        /// <summary> The program to start. </summary>
        public string ExePath { get; set; } = string.Empty;

        /// <summary> Extra command-line arguments (filled in when the app came from a shortcut that had some). </summary>
        public string Arguments { get; set; } = string.Empty;

        [JsonIgnore]
        public bool Exists => File.Exists(ExePath);

        /// <summary> Starts the program. Returns false with a reason instead of throwing, like GameMapping.LaunchExecutable. </summary>
        public bool Launch(out string? error) {
            error = null;
            if (!Exists) {
                error = $"{ExePath} was not found. Is the drive connected, or was it uninstalled?";
                return false;
            }

            try {
                // UseShellExecute lets Windows show its own "Run as administrator" prompt for launchers that need it
                Process.Start(new ProcessStartInfo {
                    FileName = ExePath,
                    Arguments = Arguments,
                    WorkingDirectory = Path.GetDirectoryName(ExePath) ?? string.Empty,
                    UseShellExecute = true,
                });
                return true;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) {
                error = ex.Message; // includes "The operation was canceled by the user" when the admin prompt is declined
                return false;
            }
        }
    }

    /// <summary> What apps.json holds. </summary>
    public class AppsData {
        public List<AppEntry> Apps { get; set; } = new();
    }
}
