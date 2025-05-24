using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;

namespace Game_Launcher.Models {
    public class GameMapping {

        public string _dirPath { get; set; } = string.Empty;
        [JsonIgnore]
        public DirectoryInfo DirPath => new DirectoryInfo(_dirPath);

        public List<string> _executables { get; set; } = new List<string> { };
        [JsonIgnore]
        public List<FileInfo> Executables => _executables.Select(path => new FileInfo(path)).ToList();

        public int PrimaryExecutable { get; set; } = 0;
        public string Name { get; set; } = "Unknown";
        public HashSet<string> Tags { get; set; } = [];

        public GameMapping() { }

        /// <summary> Constructor for GameMapping. </summary>
        /// <param name="path"> The raw path to the game directory.</param>
        /// <param name="executables"> The list of executable files for the game.</param>
        public GameMapping(string path, List<FileInfo> executables) {
            _dirPath = path;
            _executables = executables.Select(f => f.Name).ToList();
            Name = DirPath.Name;
        }

        /// <summary> Returns a string representation of the GameMapping object. </summary>
        /// <returns> A string that represents the GameMapping object. </returns>
        public override string ToString() {
            return $@"{Name} {(Tags.Contains("Installed") ? "(✓)" : "(x)")} {(Tags.Contains("Hidden") ? "(○)" : "(◉)")}: {DirPath.FullName}\{Executables[PrimaryExecutable].Name}";
        }

        /// <summary> Gets the path to the primary executable file.</summary>
        /// <returns> The full path to the primary executable file.</returns>
        public string GetExecutablePath() {
            return Path.Combine(DirPath.FullName, Executables[PrimaryExecutable].Name);
        }

        /// <summary> Launches the primary executable file for the game. </summary>
        /// <param name="error"> The error message if the launch fails.</param>
        /// <returns> True if the launch was successful, false otherwise.</returns>
        public bool LaunchExecutable(out string? error) {
            error = null;

            // Check if the game is installed
            if (!Tags.Contains("Installed")) {
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

        #region Tag Management
        /// <summary> Adds a tag to the game mapping. </summary>
        /// <param name="tag"> The tag to add.</param>
        /// <returns> True if the tag was added, false if it already exists.</returns>
        public bool AddTag(string tag) {
            if (Tags.Contains(tag)) {
                return false;
            }
            Tags.Add(tag);
            return true;
        }

        /// <summary> Removes a tag from the game mapping. </summary>
        /// <param name="tag"> The tag to remove.</param>
        /// <returns> True if the tag was removed, false if it did not exist.</returns>
        public bool RemoveTag(string tag) {
            if (!Tags.Contains(tag)) {
                return false;
            }
            Tags.Remove(tag);
            return true;
        }

        /// <summary> Clears all tags. </summary>
        /// <returns> The previous collection of tags.</returns>
        public List<string> ClearTags() {
            var prev = Tags.ToList();
            Tags.Clear();
            return prev;
        }

        /// <summary> Sets the tags to a new collection. </summary>
        /// <param name="tags"> The new collection of tags.</param>
        /// <returns> The previous collection of tags.</returns>
        public List<string> SetTags(ICollection<string> tags) {
            var prev = Tags.ToList();
            Tags = new HashSet<string>(tags);
            return prev;
        }

        /// <summary> Checks if the game mapping has a specific tag. </summary>
        /// <param name="tag"> The tag to check for.</param>
        /// <returns> True if the tag exists, false otherwise.</returns>
        public bool HasTag(string tag) {
            return Tags.Contains(tag);
        }
        #endregion
    }
}
