using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Game_Launcher.Models {
    public class GameMapping : INotifyPropertyChanged {

        public event PropertyChangedEventHandler? PropertyChanged;

        public const string UNKNOWN_PLACEHOLDER = "Unknown";

        // Private backing fields
        private string _dirPath = string.Empty;
        private List<string> _executables = new List<string>();
        private int _primaryExecutableIndex = 0;
        private string _name = UNKNOWN_PLACEHOLDER;
        private HashSet<string> _tags = new HashSet<string>();

        #region Properties
        public string DirPathRaw {
            get => _dirPath;
            set {
                if (_dirPath != value) {
                    _dirPath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DirPath));
                }
            }
        }
        [JsonIgnore]
        public DirectoryInfo? DirPath {
            get => string.IsNullOrEmpty(_dirPath) ? null : new DirectoryInfo(_dirPath);
            set {
                if (value != null && _dirPath != value.FullName) {
                    _dirPath = value.FullName;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DirPathRaw));
                }
            }
        }

        public List<string> ExecutablesRaw {
            get => _executables;
            set {
                if (_executables != value) {
                    _executables = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Executables)); // Computed property depends on this
                }
            }
        }
        [JsonIgnore]
        public List<FileInfo> Executables {
            get => _executables.Select(relPath => new FileInfo(Path.Combine(DirPath?.FullName ?? UNKNOWN_PLACEHOLDER, relPath))).ToList();
            set {
                if (value != null) {
                    var newExecutables = value
                        .Select(f => Path.GetRelativePath(DirPath?.FullName ?? string.Empty, f.FullName))
                        .ToList();

                    if (!_executables.SequenceEqual(newExecutables)) {
                        _executables = newExecutables;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(ExecutablesRaw));
                    }
                }
            }
        }

        public int PrimaryExecutableIndex {
            get => _primaryExecutableIndex;
            set {
                if (_primaryExecutableIndex != value) {
                    _primaryExecutableIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PrimaryExecutable));
                }
            }
        }
        [JsonIgnore]
        public FileInfo? PrimaryExecutable {
            get {
                var relPath = _executables[_primaryExecutableIndex];
                var fullPath = Path.Combine(DirPath?.FullName ?? UNKNOWN_PLACEHOLDER, relPath);
                return new FileInfo(fullPath);
            }
            set {
                if (value != null) {
                    int idx = Executables.FindIndex(f => f.FullName == value.FullName);
                    if (idx >= 0 && idx != _primaryExecutableIndex) {
                        _primaryExecutableIndex = idx;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(PrimaryExecutableIndex));
                    }
                    else {
                        Debug.WriteLine($"Warning: Attempted to set primary executable to {value.FullName}, but it is not in the list of executables or index is unchanged.");
                    }
                }
            }
        }

        public string Name {
            get => _name;
            set {
                if (_name != value) {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public HashSet<string> Tags {
            get => _tags;
            set {
                if (_tags != value) {
                    _tags = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        #region Constructors
        public GameMapping() { }

        /// <summary> Constructor for GameMapping. </summary>
        /// <param name="path"> The raw path to the game directory.</param>
        /// <param name="executables"> The list of executable files for the game.</param>
        public GameMapping(string path, List<FileInfo> executables) {
            _dirPath = path;
            _executables = executables.Select(f => Path.GetRelativePath(path, f.FullName)).ToList();
            Name = DirPath?.Name ?? UNKNOWN_PLACEHOLDER;
        }
        #endregion

        #region Main Methods

        /// <summary> Launches the primary executable file for the game. </summary>
        /// <param name="error"> The error message if the launch fails.</param>
        /// <returns> True if the launch was successful, false otherwise.</returns>
        public bool LaunchExecutable(out string? error) {
            error = null;

            // Check if the game is installed
            if (!Tags.Contains("Installed")) {
                error = $"{Name} is not currently installed. If this is an error, try refreshing your library.";
                return false;
            }

            // Get the executable path and check that it exists
            string? executablePath = PrimaryExecutable?.FullName ?? null;
            if (executablePath == null) {
                error = "Primary executable is not set or does not exist.";
                return false;
            }

            // Check if the executable file exists
            if (!File.Exists(executablePath)) {
                error = "Executable not found: " + executablePath;
                return false;
            }

            // Check if the directory exists
            if (DirPath == null || !DirPath.Exists) {
                error = "Game directory does not exist: " + DirPath?.FullName;
                return false;
            }

            // Attempt to launch
            try {
                Process.Start(new ProcessStartInfo{FileName = executablePath, WorkingDirectory = DirPath.FullName, UseShellExecute = true});
                Debug.WriteLine($"Successfully launched application: {executablePath}");
                return true;
            }
            catch (Exception ex) {
                error = $"Error occurred during launch process: {ex.Message}";
                return false;
            }

        }

        /// <summary> Opens the game directory in the file explorer. </summary>
        /// <param name="error"> The error message if the operation fails.</param>
        /// <returns> True if the directory was opened successfully, false otherwise.</returns>
        public bool OpenFolder(out string? error) {
            error = null;

            // Check if the directory exists
            if (DirPath == null || !DirPath.Exists) {
                error = "Game directory does not exist: " + DirPath?.FullName;
                return false;
            }

            // Attempt to open the directory
            try {
                Process.Start(new ProcessStartInfo { FileName = DirPath.FullName, UseShellExecute = true, Verb = "open" });
                return true;
            }
            catch (Exception ex) {
                error = $"Error opening game directory: {ex.Message}";
                return false;
            }
        }
        #endregion

        #region Overrides
        /// <summary> Raises the PropertyChanged event for a property. </summary>
        /// <param name="propertyName"> The name of the property that changed.</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary> Returns a string representation of the GameMapping object. </summary>
        /// <returns> A string that represents the GameMapping object. </returns>
        public override string ToString() {
            return $@"{Name} {(Tags.Contains("Installed") ? "(✓)" : "(x)")} {(Tags.Contains("Hidden") ? "(○)" : "(◉)")}: {DirPath?.FullName ?? GameMapping.UNKNOWN_PLACEHOLDER}\{Executables[PrimaryExecutableIndex].Name}";
        }
        #endregion

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

        /// <summary> Checks if the game mapping has any of the specified tags. </summary>
        /// <param name="tags"> The collection of tags to check for.</param>
        /// <returns> True if any tag exists, false otherwise.</returns>
        public bool HasAnyTag(ICollection<string> tags) {
            return tags.Any(tag => Tags.Contains(tag));
        }

        /// <summary> Checks if the game mapping has all of the specified tags. </summary>
        /// <param name="tags"> The collection of tags to check for.</param>
        /// <returns> True if all tags exist, false otherwise.</returns>
        public bool HasAllTags(ICollection<string> tags) {
            return tags.All(tag => Tags.Contains(tag));
        }
        #endregion
    }
}
