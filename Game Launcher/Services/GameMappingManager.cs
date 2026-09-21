using Game_Launcher.Helpers;
using Game_Launcher.Models;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Game_Launcher.Services {
    public class GameMappingManager {

        private static readonly string MappingsPath = AppPaths.MappingsFile;

        #region Game Scanning
        /// <summary> Scans a single game directory for executables and returns a GameMapping object. </summary>
        /// <param name="dirPath"> The directory path of the game.</param>
        /// <param name="errors"> The error messages encountered during the scan.</param>
        /// <returns> A GameMapping object representing the game, or null if no executables were found.</returns>
        public static GameMapping? ScanSingleGame(DirectoryInfo dirPath, List<string>? errors = null) {
            errors ??= new List<string>();

            if (!dirPath.Exists) {
                // Check if the directory exists in mappings
                var mappings = LoadMappings();
                var existingMapping = mappings.FirstOrDefault(m => dirPath.FullName.Equals(m.DirPath?.FullName ?? GameMapping.UNKNOWN_PLACEHOLDER, StringComparison.OrdinalIgnoreCase)); 
                if (existingMapping != null) {
                    // If it exists, return the existing mapping
                    errors.Add($"Directory is not currently installed, found previous mappings: {dirPath.FullName}");
                    return existingMapping;
                }

                errors.Add($"Directory does not exist: {dirPath.FullName}");
                return null;
            }

            // Create a temporary preferences instance to reuse the existing logic
            var prefs = Preferences.Load();
            var configuredRoots = prefs.Roots; // remembered before we overwrite them, so the game can be named correctly

            // Preserve only the ignores, replace roots and excludes with the target directory only
            prefs.SetRoots([dirPath.FullName]);
            prefs.ClearExcludes();

            // Find executables in the given directory (and subdirs if needed)
            var executables = FindExecutables(prefs, errors);

            var group = executables.Where(f => f.Directory?.FullName == dirPath.FullName).ToList();

            // If no executables found for this directory, return null
            if (group.Count == 0) {
                return null;
            }

            // Construct and return a new mapping without persisting it
            var newMapping = new GameMapping(dirPath.FullName, group);
            newMapping.Name = GuessGameName(dirPath, configuredRoots);
            if (prefs.CleanNewGameNames) {
                newMapping.Name = NameCleaner.Clean(newMapping.Name);
            }
            if (dirPath.Exists && group.Count > 0) {
                newMapping.AddTag("Installed");
            }

            return newMapping;
        }

        /// <summary> Scans a single game directory for executables and returns a GameMapping object. </summary>
        /// <param name="dirPath"> The directory path of the game.</param>
        /// <param name="errors"> The error messages encountered during the scan.</param>
        /// <returns> A GameMapping object representing the game, or null if no executables were found.</returns>
        public static GameMapping? ScanSingleGame(string dirPath, List<string>? errors = null) {
            errors ??= new List<string>();
            if (string.IsNullOrWhiteSpace(dirPath)) {
                errors.Add("Invalid Parameter: Directory path cannot be null or empty.");
                return null;
            }
            var directoryInfo = new DirectoryInfo(dirPath);
            return ScanSingleGame(directoryInfo, errors);
        }

        /// <summary> Gets all the game mappings from the specified root paths, ignoring the specified paths and keywords. </summary>
        /// <param name="rootPaths"> The root paths to search for games.</param>
        /// <param name="ignoredPaths"> The paths to ignore during the search.</param>
        /// <param name="ignoredKeywords"> The keywords to ignore in the file names.</param>
        /// <param name="errors"> The error messages encountered during the search.</param>
        /// <returns> A list of game mappings for all games found.</returns>
        /// <param name="installsProvider"> Reads which games the launchers say are installed (to work out each game's source). Replaceable in tests.</param>
        public static ScanResult ScanGames(List<string>? errors = null, Preferences? prefs = null, Func<IReadOnlyList<LauncherInstall>>? installsProvider = null) {
            errors ??= new List<string>();

            // Get saved game mappings
            var mappings = GameMappingManager.LoadMappings();
            var mappingDict = mappings.Where(m => m.DirPath != null).ToDictionary(m => m.DirPath!.FullName, m => m, StringComparer.OrdinalIgnoreCase);
            int newGames = 0;

            // Find all game executables (with the given settings, or the saved ones)
            prefs ??= Preferences.Load();
            var roots = prefs.Roots;
            var executables = FindExecutables(prefs, errors);

            // Group executables by game
            var groups = executables.GroupBy(f => f.Directory?.FullName ?? "Unknown").ToList();

            // Add games that don't show up in the mappings
            foreach (var group in groups) {
                if (!mappingDict.ContainsKey(group.Key)) {
                    var newMapping = new GameMapping(group.Key, group.ToList());
                    newMapping.Name = GuessGameName(new DirectoryInfo(group.Key), roots);
                    if (prefs.CleanNewGameNames) {
                        newMapping.Name = NameCleaner.Clean(newMapping.Name);
                    }

                    // A cover left over from an earlier time this game was in the library is simply reused (custom ones stay custom).
                    // Otherwise it's a new game, so it gets a cover lookup once.
                    if (CoverStore.FindExisting(group.Key) is { } existingCover) {
                        newMapping.CoverPath = existingCover.FileName;
                        newMapping.CoverIsCustom = existingCover.IsCustom;
                    }
                    else {
                        newMapping.CoverLookupPending = true;
                    }

                    mappings.Add(newMapping);
                    mappingDict[group.Key] = newMapping;
                    newGames++;
                }
            }

            // Which launcher each game came from, from the launchers' own install records
            var installs = (installsProvider ?? LauncherLibraries.InstallsOnThisPc)();

            // Update the IsInstalled property for each mapping
            foreach (var mapping in mappings) {
                // (a game that has disappeared keeps the source it had, since its launcher's record is gone too)
                string source = mapping.DirPathRaw is null ? "Other" : LauncherLibraries.SourceOf(mapping.DirPathRaw, installs);
                if (mapping.Source is null || source != "Other") {
                    mapping.Source = source;
                }

                if(mapping.DirPath != null && mapping.DirPath.Exists && mapping.Executables.Count > 0) {
                    mapping.AddTag("Installed");
                }
                else {
                    mapping.RemoveTag("Installed");
                }

                // Games saved before "date added" existed (and brand-new ones) get it filled in here.
                // The folder's creation date is roughly when the game was installed; reading it changes nothing on disk.
                if (mapping.DateAdded is null) {
                    mapping.DateAdded = mapping.DirPath is { Exists: true } dir ? dir.CreationTime : DateTime.Now;
                }
            }

            // Save the updated mappings
            GameMappingManager.SaveMappings(mappings);
            Debug.WriteLine($"Scanned {mappings.Count} games from {executables.Count} executables found in {mappingDict.Count} directories.");
            return new ScanResult(mappings.Count, newGames, errors.ToList());
        }

        /// <summary> What a scan found. </summary>
        /// <param name="TotalGames"> Games in the library after the scan.</param>
        /// <param name="NewGames"> Games that weren't in the library before.</param>
        /// <param name="Errors"> Folders that couldn't be read (permissions, disappeared drives...).</param>
        public record ScanResult(int TotalGames, int NewGames, IReadOnlyList<string> Errors);

        /// <summary> Names a game after the folder directly under its scan root, not the (possibly nested) folder holding the .exe. </summary>
        /// <remarks> Without this, "Games\Gamma World\bin\Gamma.exe" would be named "bin". If several roots contain the folder, the deepest one wins. </remarks>
        /// <param name="gameDir"> The folder that holds the game's executables.</param>
        /// <param name="roots"> The configured scan roots.</param>
        /// <returns> The guessed game name (falls back to the folder's own name if it isn't under any root).</returns>
        private static string GuessGameName(DirectoryInfo gameDir, IEnumerable<DirectoryInfo> roots) {
            // Steam always installs games as "...\steamapps\common\<Game>", so the game's name is the folder after
            // "common" no matter which root (e.g. "C:\SteamLibrary" vs "C:\SteamLibrary\steamapps\common") was scanned.
            const string steamMarker = @"\steamapps\common\";
            int steamIndex = gameDir.FullName.IndexOf(steamMarker, StringComparison.OrdinalIgnoreCase);
            if (steamIndex >= 0) {
                return gameDir.FullName.Substring(steamIndex + steamMarker.Length).Split(Path.DirectorySeparatorChar)[0];
            }

            string? bestPrefix = null;
            foreach (var root in roots) {
                string prefix = root.FullName.EndsWith(Path.DirectorySeparatorChar) ? root.FullName : root.FullName + Path.DirectorySeparatorChar;
                if (gameDir.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (bestPrefix == null || prefix.Length > bestPrefix.Length)) {
                    bestPrefix = prefix;
                }
            }

            if (bestPrefix == null) {
                return gameDir.Name;
            }

            return gameDir.FullName.Substring(bestPrefix.Length).Split(Path.DirectorySeparatorChar)[0];
        }

        /// <summary> Finds all executable files in the specified root paths, ignoring the specified paths and keywords. </summary>
        /// <param name="rootPaths"> The root paths to search for executables.</param>
        /// <param name="ignoredPaths"> The paths to ignore during the search.</param>
        /// <param name="ignoredKeywords"> The keywords to ignore in the file names.</param>
        /// <param name="errors"> The error messages encountered during the search.</param>
        /// <returns> A list of executable files found.</returns>
        public static List<FileInfo> FindExecutables(Preferences prefs, List<string>? errors = null) {
            errors ??= new List<string>();

            // Get preferences
            if (prefs.Roots == null) {
                errors.Add("Invalid Operation: No roots to search.");
                return new List<FileInfo>();
            }

            var searchQueue = new Queue<DirectoryInfo>();
            var results = new List<FileInfo>();

            // Initialize the search queue with the root paths
            foreach (var root in prefs.Roots) {
                searchQueue.Enqueue(root);
            }

            // Overlapping roots (e.g. "D:\Games" and "D:\Games\Amazon Games\Library") would otherwise scan
            // the same folder twice and list its executables twice.
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (searchQueue.Count > 0) {
                var currentDir = searchQueue.Dequeue();

                if (!visited.Add(currentDir.FullName)) {
                    continue;
                }

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
                    errors.Add($"{currentDir.FullName}: {e.Message}");
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
                        errors.Add($"{currentDir.FullName}: {e.Message}");
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

            // Roots, and the folders sitting directly under them, are the user's own choices/game folders
            // (e.g. "Suyu-Windows_x86_64", "Trials Fusion"), so never skip them because of a keyword.
            // Keyword skipping is for junk deeper inside a game, like "x86" or "_CommonRedist".
            var roots = prefs.Roots;
            if (roots.Any(root => root.FullName.Equals(dir.FullName, StringComparison.OrdinalIgnoreCase))
                || (dir.Parent != null && roots.Any(root => root.FullName.Equals(dir.Parent.FullName, StringComparison.OrdinalIgnoreCase)))) {
                return false;
            }

            // Match keywords against the folder's own name only. Matching the full path would skip a
            // game just because some parent folder (or the game's own path) contains a word like "trial" or "32".
            if (prefs.Ignores.Any(keyword => dir.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))) {
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

            // Write to a temp file first so a crash mid-write can't leave a half-written mappings.json
            string tempPath = MappingsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, MappingsPath, overwrite: true);
        }

        /// <summary> Loads the game mappings from a JSON file. </summary>
        /// <returns> A list of game mappings loaded from the file.</returns>
        public static List<GameMapping> LoadMappings() {
            if (!File.Exists(MappingsPath))
                return new List<GameMapping>();

            try {
                string json = File.ReadAllText(MappingsPath);
                var loaded = JsonSerializer.Deserialize<List<GameMapping>>(json) ?? new List<GameMapping>();

                // Tags renamed in a newer version are converted as the games load (a no-op once converted)
                foreach (var mapping in loaded) {
                    TagCatalog.MigrateLegacyTags(mapping);
                }
                return loaded;
            }
            catch (JsonException) {
                // Corrupt file: keep it aside rather than crash at startup or silently overwrite it later
                File.Move(MappingsPath, MappingsPath + ".bad", overwrite: true);
                return new List<GameMapping>();
            }
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
            mapping.ExecutablesRaw = mapping.ExecutablesRaw.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Load existing mappings
            var mappings = LoadMappings();

            // Check if the mapping already exists in the list
            if (mappings.Any(m => mapping.DirPath.FullName.Equals(m.DirPath?.FullName ?? GameMapping.UNKNOWN_PLACEHOLDER, StringComparison.OrdinalIgnoreCase))) {
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
            var existingMapping = mappings.FirstOrDefault(m => mapping.DirPath.FullName.Equals(m.DirPath?.FullName ?? GameMapping.UNKNOWN_PLACEHOLDER, StringComparison.OrdinalIgnoreCase));
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

            // (No directory-exists check on purpose: editing a game's name/tags must work even when its drive is unplugged)

            // Load existing mappings
            var mappings = LoadMappings();

            // Find the existing mapping
            var existingMapping = mappings.FirstOrDefault(m => mapping.DirPath.FullName.Equals(m.DirPath?.FullName ?? GameMapping.UNKNOWN_PLACEHOLDER, StringComparison.OrdinalIgnoreCase));
            if (existingMapping is null) {
                error = "Invalid Operation: Cannot update a mapping that does not exist. Try adding it instead.";
                return false;
            }

            // The cover itself is managed by the cover system (downloads and the cover picker save straight away), and the copy
            // being saved may be older than what they stored. So keep the stored cover details...
            mapping.CoverPath = existingMapping.CoverPath;
            mapping.CoverMatchedName = existingMapping.CoverMatchedName;
            mapping.CoverIsCustom = existingMapping.CoverIsCustom;

            // ...except for "should this game be looked up again?": yes if it was renamed, or reset (Reset marks the copy as pending).
            // A game with a cover the user chose is never looked up.
            bool renamed = !string.Equals(existingMapping.Name, mapping.Name, StringComparison.OrdinalIgnoreCase);
            mapping.CoverLookupPending = !existingMapping.CoverIsCustom
                && (existingMapping.CoverLookupPending || mapping.CoverLookupPending || renamed);

            // Replace previous mapping (delete and add)
            mappings.Remove(existingMapping);
            mappings.Add(mapping);

            // Save the updated mappings
            SaveMappings(mappings);
            return true;
        }

        #region Re-pointing a game to a different folder
        /// <summary>
        /// Works out which folder a game lives in when the user picks one: the picked folder itself if it holds a launchable .exe,
        /// otherwise the single game folder found inside it. Read-only: it only lists folders and files.
        /// </summary>
        public static bool ResolveGameFolder(string picked, out string? gameDir, out List<FileInfo> executables, out string? error) {
            gameDir = null;
            executables = new List<FileInfo>();
            error = null;

            if (!Directory.Exists(picked)) {
                error = "That folder doesn't exist.";
                return false;
            }

            var prefs = Preferences.Load();
            prefs.SetRoots([picked]);
            prefs.ClearExcludes();

            var groups = FindExecutables(prefs)
                .GroupBy(f => f.Directory!.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groups.Count == 0) {
                error = "Nexus couldn't find a launchable .exe in that folder or inside it.";
                return false;
            }

            var chosen = groups.FirstOrDefault(g => g.Key.Equals(new DirectoryInfo(picked).FullName, StringComparison.OrdinalIgnoreCase));
            if (chosen is null && groups.Count == 1) {
                chosen = groups[0]; // the game sits one level down (e.g. picked "Game", exe is in "Game\bin")
            }

            if (chosen is null) {
                error = $"That folder holds several games ({string.Join(", ", groups.Take(3).Select(g => Path.GetFileName(g.Key)))}"
                        + (groups.Count > 3 ? ", ..." : "") + "). Pick the folder of the one you want.";
                return false;
            }

            gameDir = chosen.Key;
            executables = chosen.ToList();
            return true;
        }

        /// <summary>
        /// Points an existing game at a different folder (e.g. after moving it to another drive). The game keeps its name, tags,
        /// play history and cover; its executables are re-read from the new folder. Refuses if another game already uses that folder.
        /// </summary>
        /// <param name="oldDirPath"> The game's current folder (identifies the game).</param>
        /// <param name="pickedFolder"> The folder the user chose.</param>
        /// <param name="updated"> The saved game on success.</param>
        public static bool Repoint(string oldDirPath, string pickedFolder, out GameMapping? updated, out string? error) {
            updated = null;

            var mappings = LoadMappings();
            var stored = mappings.FirstOrDefault(m => oldDirPath.Equals(m.DirPathRaw, StringComparison.OrdinalIgnoreCase));
            if (stored is null) {
                error = "This game isn't in the library any more.";
                return false;
            }

            if (!ResolveGameFolder(pickedFolder, out string? newDir, out var executables, out error) || newDir is null) {
                return false;
            }

            if (newDir.Equals(oldDirPath, StringComparison.OrdinalIgnoreCase)) {
                error = "That's already this game's folder.";
                return false;
            }

            var clash = mappings.FirstOrDefault(m => !ReferenceEquals(m, stored) && newDir.Equals(m.DirPathRaw, StringComparison.OrdinalIgnoreCase));
            if (clash is not null) {
                error = $"\"{clash.Name}\" in your library already uses that folder. Hide or fix that one first.";
                return false;
            }

            // Keep the executables the user added by hand, when they exist in the new folder too, and the chosen main one when possible
            string oldPrimaryName = stored.PrimaryExecutable?.Name ?? string.Empty;
            var relatives = executables.Select(f => Path.GetRelativePath(newDir, f.FullName)).ToList();
            foreach (string oldRelative in stored.ExecutablesRaw) {
                if (!relatives.Contains(oldRelative, StringComparer.OrdinalIgnoreCase) && File.Exists(Path.Combine(newDir, oldRelative))) {
                    relatives.Add(oldRelative);
                }
            }

            int primary = relatives.FindIndex(r => Path.GetFileName(r).Equals(oldPrimaryName, StringComparison.OrdinalIgnoreCase));

            stored.DirPathRaw = newDir;
            stored.ExecutablesRaw = relatives;
            stored.PrimaryExecutableIndex = Math.Max(primary, 0);
            stored.AddTag(TagCatalog.Installed);

            // The cover file's name holds a fingerprint of the folder, so it has to be renamed along with it
            try {
                string? movedCover = CoverStore.MoveForNewFolder(oldDirPath, stored);
                if (movedCover is not null) {
                    stored.CoverPath = movedCover;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                Debug.WriteLine($"Could not move the cover: {ex.Message}");
                stored.CoverPath = null;
                stored.CoverLookupPending = !stored.CoverIsCustom; // fall back to looking it up again
            }

            SaveMappings(mappings);
            updated = stored;
            error = null;
            return true;
        }
        #endregion

        /// <summary>
        /// Adds tags to a stored game (never removes any) and saves. Used by the bulk "add suggested tags" run.
        /// </summary>
        /// <returns> The tags that were actually new for the game (empty when it had them all, or when it is no longer in the library).</returns>
        public static IReadOnlyList<string> AddTags(string dirPathRaw, IEnumerable<string> tags) {
            var mappings = LoadMappings();
            var stored = mappings.FirstOrDefault(m => dirPathRaw.Equals(m.DirPathRaw, StringComparison.OrdinalIgnoreCase));
            if (stored is null) {
                return [];
            }

            var added = tags.Where(t => !stored.HasTag(t)).Distinct().ToList();
            if (added.Count > 0) {
                foreach (string tag in added) stored.AddTag(tag);
                SaveMappings(mappings);
            }
            return added;
        }

        /// <summary> Saves a game's play history (last played + launch count) without touching anything else about it. </summary>
        /// <param name="game"> The game, whose LastPlayed and LaunchCount have already been updated.</param>
        /// <param name="error"> The error message if the operation fails.</param>
        /// <returns> True if operation was a success and false otherwise. </returns>
        public static bool RecordLaunch(GameMapping game, out string? error) {
            error = null;

            var mappings = LoadMappings();
            var stored = mappings.FirstOrDefault(m => game.DirPathRaw.Equals(m.DirPathRaw, StringComparison.OrdinalIgnoreCase));
            if (stored is null) {
                error = "Invalid Operation: Cannot record a launch for a game that isn't in the library.";
                return false;
            }

            // Copy only the play-history fields so edits made elsewhere (name, tags, ...) aren't overwritten
            stored.LastPlayed = game.LastPlayed;
            stored.LaunchCount = game.LaunchCount;

            // A game with a story moves to "In Progress" the first time you launch it (what you set yourself is left alone)
            if (stored.HasTag(TagCatalog.Campaign) && !stored.HasTag(TagCatalog.InProgress) && !stored.HasTag(TagCatalog.Finished)) {
                stored.AddTag(TagCatalog.InProgress);
            }
            SaveMappings(mappings);
            return true;
        }

        /// <summary> Records that an automatic cover lookup finished for one game (found a cover or not), leaving everything else untouched. </summary>
        /// <param name="dirPathRaw"> The game's folder path (identifies the game).</param>
        /// <param name="coverFileName"> The downloaded cover's file name, or null if nothing was found (an existing cover is then kept).</param>
        /// <param name="matchedName"> The title the site matched it to.</param>
        /// <returns> The saved game, or null if it is no longer in the library.</returns>
        /// <param name="coverSource"> Where the cover came from ("Steam" or "SteamGridDB").</param>
        public static GameMapping? SetCover(string dirPathRaw, string? coverFileName, string? matchedName, string? coverSource = null) {
            var mappings = LoadMappings();
            var stored = mappings.FirstOrDefault(m => dirPathRaw.Equals(m.DirPathRaw, StringComparison.OrdinalIgnoreCase));
            if (stored is null) {
                return null;
            }

            // A cover the user picked is never touched by automatic lookups (this also covers picking one while a lookup was running)
            if (!stored.CoverIsCustom && coverFileName is not null) {
                stored.CoverPath = coverFileName;
                stored.CoverMatchedName = matchedName;
                stored.CoverSource = coverSource;
            }
            stored.CoverLookupPending = false;

            SaveMappings(mappings);
            return stored;
        }

        /// <summary> One-time tidy-up: covers saved by an earlier version were named only by a fingerprint ("93bbc17ef8ec.png"). Renames them to the current style. </summary>
        /// <returns> How many cover files were renamed (0 once everything is tidy, so it is cheap to call at every start).</returns>
        public static int MigrateLegacyCoverNames() {
            var mappings = LoadMappings();
            int moved = 0;

            foreach (var mapping in mappings) {
                if (string.IsNullOrEmpty(mapping.CoverPath) || CoverStore.IsCurrentName(mapping.CoverPath)) {
                    continue;
                }

                string oldPath = CoverArt.FullPath(mapping.CoverPath);
                if (!File.Exists(oldPath)) {
                    continue;
                }

                string newName = CoverStore.FileNameFor(mapping, Path.GetExtension(mapping.CoverPath), mapping.CoverIsCustom);
                try {
                    File.Move(oldPath, CoverArt.FullPath(newName), overwrite: true);
                    mapping.CoverPath = newName;
                    moved++;
                }
                catch (IOException ex) {
                    Debug.WriteLine($"Could not rename cover {mapping.CoverPath}: {ex.Message}");
                }
            }

            if (moved > 0) {
                SaveMappings(mappings);
            }
            return moved;
        }

        /// <summary> Makes an image the game's cover for good: automatic lookups will no longer touch it. </summary>
        /// <param name="coverFileName"> The image's file name inside the covers folder (already saved by CoverStore).</param>
        /// <param name="sourceTitle"> The SteamGridDB title it was chosen from, or null if it is the user's own image.</param>
        /// <returns> The saved game, or null if it is no longer in the library.</returns>
        public static GameMapping? SetCustomCover(string dirPathRaw, string coverFileName, string? sourceTitle) {
            var mappings = LoadMappings();
            var stored = mappings.FirstOrDefault(m => dirPathRaw.Equals(m.DirPathRaw, StringComparison.OrdinalIgnoreCase));
            if (stored is null) {
                return null;
            }

            stored.CoverPath = coverFileName;
            stored.CoverMatchedName = sourceTitle;
            stored.CoverIsCustom = true;
            stored.CoverLookupPending = false;

            SaveMappings(mappings);
            return stored;
        }

        /// <summary> Gives up a custom cover: its file is deleted and the game gets an automatic cover lookup again. </summary>
        /// <returns> The saved game, or null if it is no longer in the library.</returns>
        public static GameMapping? UseAutomaticCover(string dirPathRaw) {
            var mappings = LoadMappings();
            var stored = mappings.FirstOrDefault(m => dirPathRaw.Equals(m.DirPathRaw, StringComparison.OrdinalIgnoreCase));
            if (stored is null) {
                return null;
            }

            CoverStore.DeleteFor(stored.DirPathRaw);
            stored.CoverPath = null;
            stored.CoverMatchedName = null;
            stored.CoverIsCustom = false;
            stored.CoverLookupPending = true;

            SaveMappings(mappings);
            return stored;
        }

        #region Name clean-up
        /// <summary> One game's name before and after cleaning. </summary>
        public record NameChange(string DirPathRaw, string OldName, string NewName);

        /// <summary> What "clean up all names" would change, without changing anything (used for the confirmation preview). </summary>
        public static List<NameChange> PreviewNameCleanup() {
            return LoadMappings()
                .Select(m => new NameChange(m.DirPathRaw, m.Name, NameCleaner.Clean(m.Name)))
                .Where(c => !string.Equals(c.OldName, c.NewName, StringComparison.Ordinal))
                .ToList();
        }

        /// <summary> Cleans every existing game name. Covers are left alone: cleaning doesn't change which game a name means, and automatic lookups already clean names before searching. </summary>
        /// <returns> How many names changed.</returns>
        public static int CleanUpAllNames() {
            var mappings = LoadMappings();
            int changed = 0;

            foreach (var mapping in mappings) {
                string cleaned = NameCleaner.Clean(mapping.Name);
                if (!string.Equals(mapping.Name, cleaned, StringComparison.Ordinal)) {
                    mapping.Name = cleaned;
                    changed++;
                }
            }

            if (changed > 0) {
                SaveMappings(mappings);
            }
            return changed;
        }
        #endregion

        /// <summary> Gets a single mapping by its directory path. </summary>
        /// <param name="DirPath"> The directory path of the game.</param>
        /// <param name="error"> The error message if the operation fails.</param>
        /// <returns> The game mapping if found, null otherwise.</returns>
        public static GameMapping? GetMapping(string DirPath, out string? error) {
            error = null;
            if (DirPath is null) {
                error = "Invalid Parameter: Directory path cannot be null.";
                return null;
            }

            // Load existing mappings
            var mappings = LoadMappings();

            // Find the mapping by its directory path
            var mapping = mappings.FirstOrDefault(m => DirPath.Equals(m.DirPath?.FullName ?? GameMapping.UNKNOWN_PLACEHOLDER, StringComparison.OrdinalIgnoreCase));
            if (mapping is null) {
                error = "Invalid Operation: Mapping not found.";
                return null;
            }
            return mapping;
        }

        /// <summary> Gets closest mappings. Sorts by closest to name, then returns the first <paramref name="limit"/> mappings. </summary>
            /// <param name="mappings"> The list of game mappings to search.</param>
            /// <param name="limit"> The maximum number of mappings to return. 0 returns all results in order. </param>
            /// <returns> A list of game mappings that match the search criteria.</returns>
        public static List<GameMapping> SearchMappings(string? searchTerm = null, int limit = 0) {
            var mappings = LoadMappings();
            if (string.IsNullOrWhiteSpace(searchTerm)) {
                return mappings;
            }

            // Order by proximity to the search term
            var orderedMappings = mappings
                .Select(m => new { Mapping = m, Score = NameCompareStrict(m.Name, searchTerm) })
                .Where(x => x.Score < 3) // remove weak matches
                .OrderBy(x => x.Score)
                .ThenBy(x => Math.Abs(x.Mapping.Name.Length - searchTerm.Length)) // optional
                .Select(x => x.Mapping)
                .ToList();

            // If limit is set, return only the first 'limit' mappings
            if (limit > 0 && limit < orderedMappings.Count) {
                orderedMappings = orderedMappings.Take(limit).ToList();
            }

            return orderedMappings;
        }

        private static int NameCompareStrict(string name, string searchTerm) {
            if (name.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
                return 0; // Best match
            if (name.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                return 1;
            if (name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                return 2;

            return 3; // fallback: no match or weak match
        }

        #endregion
    }
}
