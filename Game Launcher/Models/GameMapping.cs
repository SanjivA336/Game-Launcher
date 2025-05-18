using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace Game_Launcher.Models {

    public class GameMapping {
        public string DirPathString { get; set; } = string.Empty;
        [JsonIgnore]
        public DirectoryInfo DirPath => new DirectoryInfo(DirPathString);

        public List<string> ExecutablesString { get; set; } = new List<string> { };
        [JsonIgnore]
        public List<FileInfo> Executables => ExecutablesString.Select(path => new FileInfo(path)).ToList();

        public int PrimaryExecutable { get; set; }
        public string Name { get; set; } = "Unkown";
        public bool IsVisible { get; set; }
        [JsonIgnore]
        public bool IsInstalled { get; set; }

        public string? CoverImagePath = null;

        public GameMapping() {

        }

        public GameMapping(string rawPath, List<FileInfo> executables) {
            DirPathString = rawPath;
            ExecutablesString = executables.Select(f => f.Name).ToList(); ;
            PrimaryExecutable = 0;
            Name = DirPath.Name;
            IsVisible = true;
            IsInstalled = DirPath.Exists;
        }

        public override string ToString() {
            return $@"{Name} {(IsInstalled ? "(✓)" : "(x)")} {(IsVisible ? "(◉)" : "(○)")}: {DirPath.FullName}\{Executables[PrimaryExecutable].Name}";
        }

        public string GetExecutablePath() {
            return Path.Combine(DirPath.FullName, Executables[PrimaryExecutable].Name);
        }

        public bool LaunchExecutable(out string? error) {
            error = null;

            // Check if the game is installed
            if (!IsInstalled) {
                error = $"{Name} is not curretnly installed. If this is an error, try refreshing your library.";
                return false;
            }

            // Get the executable path and check that it exists
            string executablePath = GetExecutablePath();
            if (!File.Exists(executablePath)) {
                error = "Executable not found: " + executablePath;
                return false;
            }

            // Attempt to launch
            try {
                Process.Start(new ProcessStartInfo{FileName = executablePath, WorkingDirectory = DirPath.FullName, UseShellExecute = true});
                return true;
            }
            catch (Exception ex) {
                error = $"Error occurred during launch process: {ex.Message}";
                return false;
            }
        }
    }
}
