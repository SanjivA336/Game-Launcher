using Game_Launcher.Models;
using System.IO;
using System.Text.Json;

namespace Game_Launcher.Services {
    public class GameMappingManager {

        private static readonly string MappingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "mappings.json");

        #region Game Scanning
        /// <summary> Gets all the game mappings from the specified root paths, ignoring the specified paths and keywords. </summary>
        /// <param name="rootPaths"> The root paths to search for games.</param>
        /// <param name="ignoredPaths"> The paths to ignore during the search.</param>
        /// <param name="ignoredKeywords"> The keywords to ignore in the file names.</param>
        /// <param name="errors"> The error messages encountered during the search.</param>
        /// <returns> A list of game mappings for all games found.</returns>
        public static void ScanGames(out string[] errors) {
            errors = [];

            // Get saved game mappings
            var mappings = GameMappingManager.LoadMappings();
            var mappingDict = mappings.ToDictionary(m => m.DirPath.FullName, m => m, StringComparer.OrdinalIgnoreCase);

            // Find all game executables
            var executables = FindExecutables(out errors);

            // Group executables by game
            var groups = executables.GroupBy(f => f.Directory?.FullName ?? "Unknown").ToList();

            // Add games that don't show up in the mappings
            foreach (var group in groups) {
                if (!mappingDict.ContainsKey(group.Key)) {
                    var newMapping = new GameMapping(group.Key, group.ToList());
                    mappings.Add(newMapping);
                    mappingDict[group.Key] = newMapping;
                }
            }

            // Update the IsInstalled property for each mapping
            foreach (var mapping in mappings) {
                if(mapping.DirPath.Exists && mapping.Executables.Count > 0) {
                    mapping.AddTag("Installed");
                }
                else {
                    mapping.RemoveTag("Installed");
                }
            }

            // Save the updated mappings
            GameMappingManager.SaveMappings(mappings);
        }

        /// <summary> Finds all executable files in the specified root paths, ignoring the specified paths and keywords. </summary>
        /// <param name="rootPaths"> The root paths to search for executables.</param>
        /// <param name="ignoredPaths"> The paths to ignore during the search.</param>
        /// <param name="ignoredKeywords"> The keywords to ignore in the file names.</param>
        /// <param name="errors"> The error messages encountered during the search.</param>
        /// <returns> A list of executable files found.</returns>
        public static List<FileInfo> FindExecutables(out string[] errors) {
            errors = [];

            // Get preferences
            var prefs = Preferences.Load();
            if (prefs.Roots == null) {
                errors.Append("Invalid Operation: No roots to search.");
                return new List<FileInfo>();
            }

            var searchQueue = new Queue<DirectoryInfo>();
            var results = new List<FileInfo>();

            // Initialize the search queue with the root paths
            foreach (var root in prefs.Roots) {
                searchQueue.Enqueue(root);
            }

            while (searchQueue.Count > 0) {
                var currentDir = searchQueue.Dequeue();

                // Skip ignored paths
                if (IsIgnoredPath(currentDir, prefs)) {
                    continue;
                }

                bool foundFiles = false;

                // Search for executables in the current directory
                try {
                    var files = currentDir.GetFiles("*.exe");
                    foreach (var file in files) {
                        // Skip files with ignored keywords
                        if (prefs.Ignores.Any(keyword => file.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))) {
                            continue;
                        }
                        results.Add(file);
                        foundFiles = true;
                    }
                }
                catch (Exception e) {
                    errors.Append($"{e.InnerException} in {currentDir.FullName}: {e.Message}");
                    continue;
                }

                // Enqueue subdirectories for further searching (if no files found)
                if (!foundFiles) {
                    try {
                        var subDirs = currentDir.GetDirectories();
                        foreach (var subDir in subDirs) {
                            if (!IsIgnoredPath(subDir, prefs)) {
                                searchQueue.Enqueue(subDir);
                            }
                        }
                    }
                    catch (Exception e) {
                        errors.Append($"{e.InnerException} in {currentDir.FullName}: {e.Message}");
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
        public static bool IsIgnoredPath(DirectoryInfo dir, Preferences prefs) {

            // Skip ignored paths
            if (prefs.Excludes.Any(path => path.FullName.Equals(dir.FullName, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }

            // Skip paths with ignored keywords
            if (prefs.Ignores.Any(keyword => dir.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }

            return false;
        }
        #endregion

        #region JSON Serialization
        /// <summary> Saves the game mappings to a JSON file. </summary>
        /// <param name="mappings"> The list of game mappings to save.</param>
        public static void SaveMappings(List<GameMapping> mappings) {
            mappings.Sort((x, y) => x.Name.CompareTo(y.Name));

            Directory.CreateDirectory(Path.GetDirectoryName(MappingsPath) ?? string.Empty);
            string json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(MappingsPath, json);
        }

        /// <summary> Loads the game mappings from a JSON file. </summary>
        /// <returns> A list of game mappings loaded from the file.</returns>
        public static List<GameMapping> LoadMappings() {
            if (!File.Exists(MappingsPath))
                return new List<GameMapping>();

            string json = File.ReadAllText(MappingsPath);
            return JsonSerializer.Deserialize<List<GameMapping>>(json) ?? new List<GameMapping>();
        }
        #endregion

        #region Mapping Management
        /// <summary> Add a single mapping. </summary>
        /// <param name="mapping"> The mapping to add.</param>
        /// <param name="error"> The error message if the operation fails.</param>
        /// <retrurns> True if operation was a success and false otherwise. </retrurns>
        public static bool AddMapping(GameMapping mapping, out string? error) {
            error = null;

            // Check if the mapping is null
            if (mapping is null || mapping.DirPath is null || mapping.Executables is null) {
                error = "Invalid Parameter: Mapping and its main fields cannot be null.";
                return false;
            }

            // Check if the directory exists
            if (!mapping.DirPath.Exists) {
                error = "Invalid Parameter: Directory does not exist.";
                return false;
            }

            // Remove all duplicate executable files
            var distinctExecutables = mapping.Executables.Distinct().ToList();
            mapping.Executables.Clear();
            mapping.Executables.AddRange(distinctExecutables);

            // Load existing mappings
            var mappings = LoadMappings();

            // Check if the mapping already exists in the list
            if (mappings.Any(m => m.DirPath.FullName.Equals(mapping.DirPath.FullName, StringComparison.OrdinalIgnoreCase))) {
                error = "Invalid Operation: Cannot add the same mapping twice. Try updating the mapping instead.";
                return false;
            }

            // Check if the executables exist
            foreach (var executable in mapping.Executables) {
                if (!executable.Exists) {
                    error = $"Invalid Parameter: Executable {executable.Name} does not exist.";
                    return false;
                }
            }

            // Add the new mapping to the list and save
            mappings.Add(mapping);
            SaveMappings(mappings);
            return true;
        }

        /// <summary> Add a single mapping. Overload: paramter-based adding. </summary>
        /// <param name="dirPath"> The directory path of the game.</param>
        /// <param name="executables"> The list of executable files for the game.</param>
        /// <param name="error"> The error message if the operation fails.</param>
        /// <retrurns> True if operation was a success and false otherwise. </retrurns>
        public static bool AddMapping(string dirPath, List<FileInfo> executables, out string? error) {
            // Check if the mapping is null
            if (dirPath is null || executables is null) {
                error = "Invalid Parameter: A mapping's main fields cannot be null.";
                return false;
            }

            // Create a new mapping
            return AddMapping(new GameMapping(dirPath, executables), out error);
        }

        /// <summary> Deletes a single mapping. </summary>
        /// <param name="mapping"> The mapping to delete.</param>
        /// <param name="error"> The error message if the operation fails.</param>
        /// <retrurns> True if operation was a success and false otherwise. </retrurns>
        public static bool DeleteMapping(GameMapping mapping, out string? error) {
            error = null;

            // Check if the mapping is null
            if (mapping is null || mapping.DirPath is null) {
                error = "Invalid Parameter: Mapping and its main fields cannot be null.";
                return false;
            }

            // Load existing mappings
            var mappings = LoadMappings();

            // Check if the mapping already exists in the list
            var existingMapping = mappings.FirstOrDefault(m => m.DirPath.FullName.Equals(mapping.DirPath.FullName, StringComparison.OrdinalIgnoreCase));
            if (existingMapping == null) {
                error = "Invalid Operation: Cannot delete a mapping that does not exist.";
                return false;
            }

            // Remove the mapping from the list and save
            mappings.Remove(existingMapping);
            SaveMappings(mappings);
            return true;
        }

        /// <summary> Deletes a single mapping. Overload: search by path. </summary>
        /// <param name="dirPath"> The directory path of the game.</param>
        /// <param name="error"> The error message if the operation fails.</param>
        /// <retrurns> True if operation was a success and false otherwise. </retrurns>
        public static bool DeleteMapping(string dirPath, out string? error) {
            // Check if the mapping is null
            if (dirPath is null) {
                error = "Invalid Parameter: A mapping's main fields cannot be null.";
                return false;
            }
            // Create a new mapping
            return DeleteMapping(new GameMapping(dirPath, new List<FileInfo>()), out error);
        }

        /// <summary> Updates a single mapping. </summary>
        /// <param name="mapping"> The mapping to update.</param>
        /// <param name="error"> The error message if the operation fails.</param>
        /// <retrurns> True if operation was a success and false otherwise. </retrurns>
        public static bool UpdateMapping(GameMapping mapping, out string? error) {
            error = null;

            // Check if the mapping is null
            if (mapping is null || mapping.DirPath is null || mapping.Executables is null) {
                error = "Invalid Parameter: Mapping and its main fields cannot be null.";
                return false;
            }

            // Check if the directory exists
            if (!mapping.DirPath.Exists) {
                error = "Invalid Parameter: Directory does not exist.";
                return false;
            }

            // Load existing mappings
            var mappings = LoadMappings();

            // Find the existing mapping
            var existingMapping = mappings.FirstOrDefault(m => m.DirPath.FullName.Equals(mapping.DirPath.FullName, StringComparison.OrdinalIgnoreCase));
            if (existingMapping is null) {
                error = "Invalid Operation: Cannot update a mapping that does not exist. Try adding it instead.";
                return false;
            }

            // Replace previous mapping (delete and add)
            mappings.Remove(existingMapping);
            mappings.Add(mapping);

            // Save the updated mappings
            SaveMappings(mappings);
            return true;
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
            if (limit == 0) {
                return sortedMappings;
            }
            return sortedMappings.Take(limit).ToList();
        }
        #endregion
    }
}
