using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game_Launcher.Models {
    public class Preferences {

        private static readonly string PreferencesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "preferences.json");

        private static readonly HashSet<string> DefaultIgnores = new HashSet<string> {
                "redist",
                "crash",
                "helper",
                "update",
                "unins",
                "setup",
                "bench",
                "anticheat",
                "worker",
                "agent",
                "service",
                "dotnet",
                "handler",
                "x86",
                "32",
                "trial",
                "downloader",
                "pbsvc",
                "readme",
                "prelaunch",
                "cracktro"
            };

        #region Temp Stuff (Values for later)
        /* Root paths to search for executables
        var roots = new List<DirectoryInfo> {
                new DirectoryInfo(@"D:\Games"),
                new DirectoryInfo(@"D:\Games\Amazon Games\Library"),
                new DirectoryInfo(@"D:\Games\Steam\steamapps\common"),
            };

        // Paths to ignore during the search
        var exludes = new List<DirectoryInfo> {
                new DirectoryInfo(@"D:\D:\Games\Amazon Games"),
                new DirectoryInfo(@"D:\Games\Steam"),
                new DirectoryInfo(@"D:\Games\UE_5.1"),
            };

        // Keywords to ignore in the file names
        var filters = new List<string> {
                "redist",
                "crash",
                "helper",
                "update",
                "unins",
                "setup",
                "bench",
                "anticheat",
                "worker",
                "agent",
                "service",
                "dotnet",
                "handler",
                "x86",
                "32",
                "trial",
                "downloader",
                "pbsvc",
                "readme",
                "prelaunch",
                "cracktro"
            };
        */
        #endregion

        public HashSet<string> _roots { get; set; } = [];
        [JsonIgnore]
        public HashSet<DirectoryInfo> Roots => _roots.Select(path => new DirectoryInfo(path)).ToHashSet();

        public HashSet<string> _excludes { get; set; } = [];
        [JsonIgnore]
        public HashSet<DirectoryInfo> Excludes => _excludes.Select(path => new DirectoryInfo(path)).ToHashSet();

        public HashSet<string> Ignores { get; set; } = [];

        #region JSON Serialization
        /// <summary> Loads the user preferences from a JSON file. </summary>
        /// <returns> A list of user preferences loaded from the file.</returns>
        public static Preferences Load() {
            if (!File.Exists(PreferencesPath))
                return new Preferences();

            string json = File.ReadAllText(PreferencesPath);
            return JsonSerializer.Deserialize<Preferences>(json) ?? new Preferences();
        }

        /// <summary> Saves the user preferences to a JSON file. </summary>
        public void Save() {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath) ?? string.Empty);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PreferencesPath, json);
        }
        #endregion

        #region Root Management
        /// <summary> Sets the roots for the game scanning process. </summary>
        /// <param name="roots"> The list of root directories to set (as DirectoryInfos).</param>
        public void SetRoots(ICollection<DirectoryInfo> roots) {
            _roots = roots.Select(d => d.FullName).ToHashSet();
        }

        /// <summary> Sets the roots for the game scanning process. Overload: Uses strings </summary>
        /// <param name="roots"> The list of root directories to set (as strings).</param>
        public void SetRoots(ICollection<string> roots) {
            _roots = roots.ToHashSet();
        }

        /// <summary> Adds a root for the game scanning process. </summary>
        /// <param name="root"> The root directory to add (as a DirectoryInfo).</param>
        public void AddRoot(DirectoryInfo root) {
            _roots.Add(root.FullName);
        }

        /// <summary> Adds a root for the game scanning process. Overload: Uses strings </summary>
        /// <param name="root"> The root directory to add (as a string).</param>
        public void AddRoot(string root) {
            _roots.Add(root);
        }

        /// <summary> Removes a root for the game scanning process. </summary>
        /// <param name="root"> The root directory to remove (as a DirectoryInfo).</param>
        public void RemoveRoot(DirectoryInfo root) {
            _roots.Remove(root.FullName);
        }

        /// <summary> Removes a root for the game scanning process. Overload: Uses strings </summary>
        /// <param name="root"> The root directory to remove (as a string).</param>
        public void RemoveRoot(string root) {
            _roots.Remove(root);
        }

        /// <summary> Clears the roots to empty. </summary>
        /// <returns> The previous roots before the clear.</returns>
        public HashSet<DirectoryInfo> ClearRoots() {
            var prevRoots = Roots.ToHashSet();
            _roots.Clear();
            return prevRoots;
        }
        #endregion

        #region Exclude Management
        /// <summary> Sets the excludes for the game scanning process. </summary>
        /// <param name="excludes"> The list of exclude directories to set (as DirectoryInfos).</param>
        public void SetExcludes(ICollection<DirectoryInfo> excludes) {
            _excludes = excludes.Select(d => d.FullName).ToHashSet();
        }

        /// <summary> Sets the excludes for the game scanning process. Overload: Uses strings </summary>
        /// <param name="excludes"> The list of exclude directories to set (as strings).</param>
        public void SetExcludes(ICollection<string> excludes) {
            _excludes = excludes.ToHashSet();
        }

        /// <summary> Adds an exclude for the game scanning process. </summary>
        /// <param name="exclude"> The exclude directory to add (as a DirectoryInfo).</param>
        public void AddExclude(DirectoryInfo exclude) {
            _excludes.Add(exclude.FullName);
        }

        /// <summary> Adds an exclude for the game scanning process. Overload: Uses strings </summary>
        /// <param name="exclude"> The exclude directory to add (as a string).</param>
        public void AddExclude(string exclude) {
            _excludes.Add(exclude);
        }

        /// <summary> Removes an exclude for the game scanning process. </summary>
        /// <param name="exclude"> The exclude directory to remove (as a DirectoryInfo).</param>
        public void RemoveExclude(DirectoryInfo exclude) {
            _excludes.Remove(exclude.FullName);
        }

        /// <summary> Removes an exclude for the game scanning process. Overload: Uses strings </summary>
        /// <param name="exclude"> The exclude directory to remove (as a string).</param>
        public void RemoveExclude(string exclude) {
            _excludes.Remove(exclude);
        }

        /// <summary> Clears the excludes to empty. </summary>
        /// <returns> The previous excludes before the clear.</returns>
        public HashSet<DirectoryInfo> ClearExcludes() {
            var prevExcludes = Excludes.ToHashSet();
            _excludes.Clear();
            return prevExcludes;
        }
        #endregion

        #region Ignore Management
        /// <summary> Sets the ignores for the game scanning process. </summary>
        /// <param name="ignores"> The list of ignores to set.</param>
        public void SetIgnores(ICollection<string> ignores) {
            Ignores = ignores.ToHashSet();
        }

        /// <summary> Adds a ignore for the game scanning process. </summary>
        /// <param name="ignore"> The ignore to add.</param>
        public void AddIgnore(string ignore) {
            Ignores.Add(ignore);
        }

        /// <summary> Removes a ignore for the game scanning process. </summary>
        /// <param name="ignore"> The ignore to remove.</param>
        public void RemoveIgnore(string ignore) {
            Ignores.Remove(ignore);
        }

        /// <summary> Clears the ignores to empty. </summary>
        /// <returns> The previous ignores before the clear.</returns>
        public HashSet<string> ClearIgnores() {
            var prevIgnores = Ignores.ToHashSet();
            Ignores.Clear();
            return prevIgnores;
        }

        /// <summary> Resets the ignores to the default values. </summary>
        /// <returns> The previous ignores before the reset.</returns>
        public HashSet<string> ResetIgnores() {
            var prevIgnores = Ignores.ToHashSet();
            Ignores = DefaultIgnores.ToHashSet();
            return prevIgnores;
        }
        #endregion

    }
}
