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

        #region Game Scanning and Loading
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

        public string GetGameList(List<GameMapping> mappings) {
            StringBuilder sb = new StringBuilder();
            foreach (var game in mappings) {
                sb.AppendLine(game.ToString());
            }
            sb.AppendLine($"Found {mappings.Count(m => m.IsInstalled)} games");
            return sb.ToString();
        }

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

        public static void SaveMappings(List<GameMapping> mappings) {
            string json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions
            {
                WriteIndented = true // Makes the JSON file human-readable
            });
            File.WriteAllText("mappings.json", json);
        }

        public static List<GameMapping> LoadMappings() {
            if (!File.Exists("mappings.json"))
                return new List<GameMapping>(); // Return empty list if file doesn't exist

            string json = File.ReadAllText("mappings.json");
            return JsonSerializer.Deserialize<List<GameMapping>>(json) ?? new List<GameMapping>();
        }
        #endregion
    }
}