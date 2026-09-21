using System.IO;

namespace Game_Launcher.Helpers {
    /// <summary>
    /// Where Nexus keeps its own files. Everything (settings, your games' details, cover images) lives in ONE folder:
    ///
    ///     %LOCALAPPDATA%\Nexus Launcher\        e.g. C:\Users\you\AppData\Local\Nexus Launcher\
    ///
    /// Why there and not next to the exe: an exe can be moved, rebuilt, deleted or run from a read-only folder, and your
    /// tags, history and covers shouldn't go with it. "Local" (not "Roaming") because the data holds this PC's folder paths.
    /// </summary>
    public static class AppPaths {
        /// <summary> Setting this environment variable points Nexus at a different data folder (used by tests, or to keep a separate library). </summary>
        public const string DataDirEnvironmentVariable = "NEXUS_DATA_DIR";

        private const string FolderName = "Nexus Launcher";

        /// <summary> The folder holding all of Nexus's own files. </summary>
        public static string DataDirectory { get; } = ResolveDataDirectory(
            Environment.GetEnvironmentVariable(DataDirEnvironmentVariable),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        public static string PreferencesFile => Path.Combine(DataDirectory, "preferences.json");
        public static string MappingsFile => Path.Combine(DataDirectory, "mappings.json");
        public static string AppsFile => Path.Combine(DataDirectory, "apps.json");
        public static string CoversDirectory => Path.Combine(DataDirectory, "Covers");

        /// <summary> Where earlier versions kept their data: a "UserData" folder next to the exe. </summary>
        public static string LegacyDataDirectory => Path.Combine(AppContext.BaseDirectory, "UserData");

        /// <summary> Works out the data folder: the override if one is given, otherwise the standard per-user location. </summary>
        public static string ResolveDataDirectory(string? overridePath, string localAppData) {
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(localAppData, FolderName)
                : Path.GetFullPath(overridePath);
        }

        /// <summary>
        /// One-time move of an earlier version's data folder into the new location. Copies (never deletes) the old folder,
        /// and only if the new location doesn't exist yet, so it can never overwrite data you already have.
        /// </summary>
        /// <returns> True if data was copied.</returns>
        public static bool MigrateLegacyData(string legacyDirectory, string newDirectory) {
            if (!Directory.Exists(legacyDirectory) || Directory.Exists(newDirectory)) {
                return false;
            }

            // Copy into a temporary folder and rename it at the end, so a crash halfway can't leave a half-copied library
            // that the next start would mistake for a finished one.
            string temporary = newDirectory + ".migrating";
            if (Directory.Exists(temporary)) {
                Directory.Delete(temporary, recursive: true);
            }

            CopyDirectory(legacyDirectory, temporary);
            Directory.Move(temporary, newDirectory);
            return true;
        }

        /// <summary> Runs the one-time migration for the standard locations. Does nothing when a custom data folder is in use. </summary>
        public static bool MigrateLegacyDataIfNeeded() {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DataDirEnvironmentVariable))) {
                return false;
            }

            try {
                return MigrateLegacyData(LegacyDataDirectory, DataDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                // Not fatal: Nexus simply starts with an empty library and the old folder is still there to copy by hand
                System.Diagnostics.Debug.WriteLine($"Could not migrate old data: {ex.Message}");
                return false;
            }
        }

        private static void CopyDirectory(string source, string destination) {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }
            foreach (string directory in Directory.GetDirectories(source)) {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }
    }
}
