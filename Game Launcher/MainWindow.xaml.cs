using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Diagnostics;
using System.Runtime.Serialization;
using System.IO;
using System.Collections.Immutable;

using System.Text.Json;
using Game_Launcher.Models;

using Game_Launcher.Views;
using System.Runtime.InteropServices;

namespace Game_Launcher {
    public partial class MainWindow : Window {

        public MainWindow() {
            InitializeComponent();

            // Root paths to search for executables
            var rootPaths = new List<DirectoryInfo> {
                new DirectoryInfo(@"D:\Games"),
                new DirectoryInfo(@"D:\Games\Amazon Games\Library"),
                new DirectoryInfo(@"D:\Games\Steam\steamapps\common"),
            };

            // Paths to ignore during the search
            var ignoredPaths = new List<DirectoryInfo> {
                new DirectoryInfo(@"D:\D:\Games\Amazon Games"),
                new DirectoryInfo(@"D:\Games\Steam"),
                new DirectoryInfo(@"D:\Games\UE_5.1"),
            };

            // Keywords to ignore in the file names
            var ignoredKeywords = new List<string> {
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

            // Update mappings
            var mappings = ReloadGames(rootPaths, ignoredPaths, ignoredKeywords);

            mappings.Sort((a, b) => a.Name.CompareTo(b.Name));


            // Show the library page
            PageHost.Navigate(new Views.Pages.Library(mappings));
        }

        #region Title Bar
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Maximized) {
                WindowState = WindowState.Normal;
                Maximize.Content = "❐";  // Maximize icon
            }
            else {
                WindowState = WindowState.Maximized;
                Maximize.Content = "❑";  // Restore icon
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) {
            Close();
        }
        #endregion

        #region Game Scanning
        /// <summary> Gets all the game mappings from the specified root paths, ignoring the specified paths and keywords. </summary>
        /// <param name="rootPaths"> The root paths to search for games.</param>
        /// <param name="ignoredPaths"> The paths to ignore during the search.</param>
        /// <param name="ignoredKeywords"> The keywords to ignore in the file names.</param>
        /// <returns> A list of game mappings for all games found.</returns>
        public List<GameMapping> ReloadGames(List<DirectoryInfo> rootPaths, List<DirectoryInfo> ignoredPaths, List<string> ignoredKeywords) {
            // Get saved game mappings
            var mappings = LoadMappings();
            var mappingDict = mappings.ToDictionary(m => m.DirPath.FullName, m => m, StringComparer.OrdinalIgnoreCase);

            // Find all game executables
            var executables = FindExecutables(rootPaths, ignoredPaths, ignoredKeywords);

            // Group executables by game
            var games = executables.GroupBy(f => f.Directory?.FullName ?? "Unknown").ToList();

            // Update mappings
            foreach (var game in games) {
                // Check if the game already has a mapping
                if (mappingDict.ContainsKey(game.Key)) {
                    // If it does, ensure it's labled as installed
                    mappingDict[game.Key].IsInstalled = true;
                }
                else {
                    // If it doesn't, add a new entry
                    var newMapping = new GameMapping(game.Key, game.ToList());
                    mappings.Add(newMapping);
                    mappingDict[game.Key] = newMapping;
                }
            }

            SaveMappings(mappings);
            return mappings;
        }

        /// <summary> Gets a string representation of the game list. </summary>
        /// <param name="mappings"> The list of game mappings.</param>
        /// <returns> A string representation of the game list.</returns>
        public string GetGameList(List<GameMapping> mappings) {
            StringBuilder sb = new StringBuilder();
            foreach (var game in mappings) {
                sb.AppendLine(game.ToString());
            }
            sb.AppendLine($"Found {mappings.Count(m => m.IsInstalled)} games");
            return sb.ToString();
        }

        /// <summary> Finds all executable files in the specified root paths, ignoring the specified paths and keywords. </summary>
        /// <param name="rootPaths"> The root paths to search for executables.</param>
        /// <param name="ignoredPaths"> The paths to ignore during the search.</param>
        /// <param name="ignoredKeywords"> The keywords to ignore in the file names.</param>
        /// <returns> A list of executable files found.</returns>
        public static List<FileInfo> FindExecutables(List<DirectoryInfo> rootPaths, List<DirectoryInfo> ignoredPaths, List<string> ignoredKeywords) {
            var searchQueue = new Queue<DirectoryInfo>();
            var results = new List<FileInfo>();

            // Initialize the search queue with the root paths
            foreach (var rootPath in rootPaths) {
                searchQueue.Enqueue(rootPath);
            }

            while (searchQueue.Count > 0) {
                var currentDir = searchQueue.Dequeue();

                // Skip ignored paths
                if (IsIgnoredPath(currentDir, ignoredPaths, ignoredKeywords)) {
                    continue;
                }

                bool foundFiles = false;

                // Search for executables in the current directory
                try {
                    var files = currentDir.GetFiles("*.exe");
                    foreach (var file in files) {
                        // Skip files with ignored keywords
                        if (ignoredKeywords.Any(keyword => file.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))) {
                            continue;
                        }
                        results.Add(file);
                        foundFiles = true;
                    }
                }
                catch (Exception e) {
                    Debug.WriteLine($"Error encountered while scanning for files in {currentDir.FullName}: {e.Message}");
                    continue;
                }

                // Enqueue subdirectories for further searching (if no files found)
                if (!foundFiles) {
                    try {
                        var subDirs = currentDir.GetDirectories();
                        foreach (var subDir in subDirs) {
                            if (!IsIgnoredPath(subDir, ignoredPaths, ignoredKeywords)) {
                                searchQueue.Enqueue(subDir);
                            }
                        }
                    }
                    catch (Exception e) {
                        Debug.WriteLine($"Error encountered while scanning for directories in {currentDir.FullName}: {e.Message}");
                        continue;
                    }
                }
            }
            return results;
        }

        /// <summary> Checks if the current directory should be ignored based on the specified paths and keywords. </summary>
        /// <param name="dir"> The directory to check.</param>
        /// <param name="ignoredPaths"> The paths to ignore during the search.</param>
        /// <param name="ignoredKeywords"> The keywords to ignore in the file names.</param>
        /// <returns> True if the directory should be ignored, false otherwise.</returns>
        public static bool IsIgnoredPath(DirectoryInfo dir, List<DirectoryInfo> ignoredPaths, List<string> ignoredKeywords) {

            // Skip ignored paths
            if (ignoredPaths.Any(path => path.FullName.Equals(dir.FullName, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }

            // Skip paths with ignored keywords
            if (ignoredKeywords.Any(keyword => dir.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }

            return false;
        }
        #endregion

        #region Mapping Management
        /// <summary> Saves the game mappings to a JSON file. </summary>
        /// <param name="mappings"> The list of game mappings to save.</param>
        public static void SaveMappings(List<GameMapping> mappings) {
            string json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions {WriteIndented = true });
            File.WriteAllText("mappings.json", json);
        }

        /// <summary> Loads the game mappings from a JSON file. </summary>
        /// <returns> A list of game mappings loaded from the file.</returns>
        public static List<GameMapping> LoadMappings() {
            if (!File.Exists("mappings.json"))
                return new List<GameMapping>(); // Return empty list if file doesn't exist

            string json = File.ReadAllText("mappings.json");
            return JsonSerializer.Deserialize<List<GameMapping>>(json) ?? new List<GameMapping>();
        }

        /// <summary> Deletes a single mapping. </summary>
        /// <param name="mapping"> The mapping to delete.</param>
        /// <returns> Returns null if success, or a string containing the first error message encountered. </returns>
        public static string? DeleteMapping(GameMapping mapping) {
            // Check if the mapping is null
            if (mapping is null || mapping.DirPath is null)
                return "Invalid Parameter: Mapping and its main fields cannot be null.";

            // Load existing mappings
            var mappings = LoadMappings();

            // Check if the mapping already exists in the list
            var existingMapping = mappings.FirstOrDefault(m => m.DirPath.FullName.Equals(mapping.DirPath.FullName, StringComparison.OrdinalIgnoreCase));
            if (existingMapping == null) {
                return "Invalid Operation: Cannot delete a mapping that does not exist.";
            }

            // Remove the mapping from the list and save
            mappings.Remove(existingMapping);
            SaveMappings(mappings);
            return null;
        }

        /// <summary> Deletes a single mapping. Overload: search by path. </summary>
        /// <param name="dirPath"> The directory path of the game.</param>
        /// <returns> Returns null if success, or a string containing the first error message encountered. </returns>
        public static string? DeleteMapping(string dirPath) {
            // Check if the mapping is null
            if (dirPath is null)
                return "Invalid Parameter: A mapping's main fields cannot be null.";
            // Create a new mapping
            return DeleteMapping(new GameMapping(dirPath, new List<FileInfo>()));
        }

        /// <summary> Add a single mapping. </summary>
        /// <param name="mapping"> The mapping to add.</param>
        /// <returns> Returns null if success, or a string containing the first error message encountered. </returns>
        public static string? AddMapping(GameMapping mapping) {

            // Check if the mapping is null
            if (mapping is null || mapping.DirPath is null || mapping.Executables is null)
                return "Invalid Parameter: Mapping and its main fields cannot be null.";

            // Check if the directory exists
            if(!mapping.DirPath.Exists) {
                return "Invalid Parameter: Directory does not exist.";
            }

            // Load existing mappings
            var mappings = LoadMappings();

            // Check if the mapping already exists in the list
            if (mappings.Any(m => m.DirPath.FullName.Equals(mapping.DirPath.FullName, StringComparison.OrdinalIgnoreCase))) {
                return "Invalid Operation: Cannot add the same mapping twice. Try updating the mapping instead.";
            }

            // Check if the executables exist
            foreach (var executable in mapping.Executables) {
                if (!executable.Exists) {
                    return $"Invalid Parameter: Executable {executable.Name} does not exist.";
                }
            }

            // Add the new mapping to the list and save
            mappings.Add(mapping);
            SaveMappings(mappings);
            return null;
        }

        /// <summary> Add a single mapping. Overload: paramter-based adding. </summary>
        /// <param name="dirPath"> The directory path of the game.</param>
        /// <param name="executables"> The list of executable files for the game.</param>
        /// <returns> Returns null if success, or a string containing the first error message encountered. </returns>
        public static string? AddMapping(string dirPath, List<FileInfo> executables) {
            // Check if the mapping is null
            if (dirPath is null || executables is null)
                return "Invalid Parameter: A mapping's main fields cannot be null.";

            // Create a new mapping
            return AddMapping(new GameMapping(dirPath, executables));
        }

        /// <summary> Updates a single mapping. </summary>
        /// <param name="mapping"> The mapping to update.</param>
        /// <returns> Returns null if success, or a string containing the first error message encountered. </returns>
        public static string? UpdateMapping(GameMapping mapping) {
            // Check if the mapping is null
            if (mapping is null || mapping.DirPath is null || mapping.Executables is null)
                return "Invalid Parameter: Mapping and its main fields cannot be null.";

            // Check if the directory exists
            if (!mapping.DirPath.Exists) {
                return "Invalid Parameter: Directory does not exist.";
            }

            // Check if the executables exist
            foreach (var executable in mapping.Executables) {
                if (!executable.Exists) {
                    return $"Invalid Parameter: Executable {executable.Name} does not exist.";
                }
            }

            // Load existing mappings
            var mappings = LoadMappings();

            // Check if the mapping already exists in the list
            var existingMapping = mappings.FirstOrDefault(m => m.DirPath.FullName.Equals(mapping.DirPath.FullName, StringComparison.OrdinalIgnoreCase));
            if (existingMapping is null) {
                return "Invalid Operation: Cannot update a mapping that does not exist. Try adding it instead.";
            }

            // Replace previous mapping (delete and add)
            mappings.Remove(existingMapping);
            mappings.Add(mapping);

            // Save the updated mappings
            SaveMappings(mappings);
            return null;
        }

        /// <summary> Gets closest mappings. Sorts by closest to name, then returns the first <paramref name="limit"/> mappings. </summary>
        /// <param name="mappings"> The list of game mappings to search.</param>
        /// <param name="limit"> The maximum number of mappings to return. 0 returns all results in order. </param>
        /// <returns> A list of game mappings that match the search criteria.</returns>
        public static List<GameMapping> SearchMappings(List<GameMapping> mappings, int limit) {
            // Check if the mapping is null
            if (mappings is null)
                return new List<GameMapping>();

            // Sort mappings by closest to name
            List<GameMapping> sortedMappings = mappings.OrderBy(m => m.Name).ToList();

            // Return the first <limit> mappings
            if(limit == 0) {
                return sortedMappings;
            }
            return (List<GameMapping>)sortedMappings.Take(limit);
        }
        #endregion
    }
}