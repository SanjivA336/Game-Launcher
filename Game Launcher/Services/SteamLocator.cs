using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace Game_Launcher.Services {
    /// <summary>
    /// Finds Steam's game folders. Read-only: it only reads the registry and Steam's own library list, and never writes to either.
    /// </summary>
    public static class SteamLocator {

        /// <summary> Where Steam is installed (from the registry), or null when Steam isn't installed. </summary>
        public static string? FindSteamPath() {
            try {
                // Per-user setting written by Steam itself; uses forward slashes ("c:/program files (x86)/steam")
                if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string userPath && Directory.Exists(userPath)) {
                    return Path.GetFullPath(userPath);
                }

                if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) is string machinePath && Directory.Exists(machinePath)) {
                    return Path.GetFullPath(machinePath);
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) {
                // Registry not readable: treat as "Steam not found"
            }
            return null;
        }

        /// <summary>
        /// Pulls the library folder paths out of the text of Steam's libraryfolders.vdf. Each library looks like
        /// <c>"path"    "D:\\Games\\Steam"</c> (backslashes are doubled inside the file).
        /// </summary>
        public static IReadOnlyList<string> ParseLibraryFolders(string vdfText) {
            var paths = new List<string>();
            foreach (Match match in Regex.Matches(vdfText, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)) {
                string path = match.Groups[1].Value.Replace(@"\\", @"\");
                if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase)) {
                    paths.Add(path);
                }
            }
            return paths;
        }

        /// <summary> Every Steam library's "steamapps\common" folder that exists on this PC (where Steam keeps the actual games). </summary>
        /// <param name="steamPath"> Steam's install folder; looked up in the registry when left out.</param>
        public static IReadOnlyList<string> FindCommonFolders(string? steamPath = null) {
            steamPath ??= FindSteamPath();
            if (steamPath is null) {
                return [];
            }

            var libraries = new List<string> { steamPath };
            try {
                string vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf)) {
                    libraries.AddRange(ParseLibraryFolders(File.ReadAllText(vdf)));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                // Unreadable list: the main install is still reported
            }

            return libraries
                .Select(lib => Path.Combine(lib, "steamapps", "common"))
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
