using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Game_Launcher.Services {

    /// <summary> A folder that one launcher keeps its games in. </summary>
    public record LauncherLibrary(string Launcher, string Path);

    /// <summary> One installed game according to its launcher's own records: which launcher, and the game's install folder. </summary>
    public record LauncherInstall(string Launcher, string Folder);

    /// <summary>
    /// Finds where other game launchers keep their games, so those folders can be added as scan folders in one go.
    /// Read-only: it only reads the registry and the launchers' own install records; nothing is ever written.
    /// </summary>
    /// <remarks>
    /// Only launchers that record their install folders in a plain, readable place are supported: Steam (libraryfolders.vdf),
    /// Epic Games (install manifests), GOG, Ubisoft Connect, EA app and Rockstar (registry), and Riot (product settings files).
    /// Battle.net, itch and Amazon Games keep theirs in private databases, so they can't be read this way (add those folders by hand).
    /// </remarks>
    public static class LauncherLibraries {

        /// <summary> Everything the finder reads, gathered up so tests can supply a fake PC. </summary>
        /// <param name="SteamPath"> Steam's install folder, or null.</param>
        /// <param name="ProgramData"> The ProgramData folder (where Epic and Riot keep their install records).</param>
        /// <param name="SystemFolders"> Folders that must never become scan folders (Program Files, Windows...).</param>
        /// <param name="RegistryValues"> For a registry key under HKEY_LOCAL_MACHINE and a value name: that value from each of its sub-keys.</param>
        /// <param name="FindFiles"> Files matching a pattern anywhere below a folder.</param>
        /// <param name="ReadText"> A file's text, or null when it can't be read.</param>
        /// <param name="DirectoryExists"> Whether a folder exists.</param>
        public record Env(
            string? SteamPath,
            string ProgramData,
            IReadOnlySet<string> SystemFolders,
            Func<string, string, IReadOnlyList<(string SubKey, string Value)>> RegistryValues,
            Func<string, string, IReadOnlyList<string>> FindFiles,
            Func<string, string?> ReadText,
            Func<string, bool> DirectoryExists);

        /// <summary> Looks on this PC. Quick: a handful of registry keys and small files. </summary>
        public static IReadOnlyList<LauncherLibrary> FindOnThisPc() => Find(RealEnv());

        /// <summary> Every game the non-Steam launchers say is installed, with its exact install folder. Quick, like <see cref="FindOnThisPc"/>. </summary>
        public static IReadOnlyList<LauncherInstall> InstallsOnThisPc() => Installs(RealEnv());

        /// <summary> The exact install folder of each game the launchers know about (Steam games are recognised by their folder layout instead). </summary>
        public static IReadOnlyList<LauncherInstall> Installs(Env env) {
            var installs = new List<LauncherInstall>();

            void Add(string launcher, string? folder) {
                if (string.IsNullOrWhiteSpace(folder)) return;
                try {
                    string full = System.IO.Path.GetFullPath(folder.Trim().Trim('"')).TrimEnd(System.IO.Path.DirectorySeparatorChar);
                    installs.Add(new LauncherInstall(launcher, full));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) {
                    // an unreadable path is simply skipped
                }
            }

            foreach (string install in EpicInstallFolders(env)) Add("Epic Games", install);
            foreach (var (_, path) in env.RegistryValues(@"SOFTWARE\WOW6432Node\GOG.com\Games", "path")) Add("GOG", path);
            foreach (var (_, path) in env.RegistryValues(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs", "InstallDir")) Add("Ubisoft Connect", path);
            foreach (var (_, path) in env.RegistryValues(@"SOFTWARE\WOW6432Node\EA Games", "Install Dir")) Add("EA app", path);
            foreach (var (name, path) in env.RegistryValues(@"SOFTWARE\WOW6432Node\Rockstar Games", "InstallFolder")) {
                if (name.Equals("Launcher", StringComparison.OrdinalIgnoreCase) || name.Contains("Social Club", StringComparison.OrdinalIgnoreCase)) continue;
                Add("Rockstar Games", path);
            }
            foreach (string install in RiotInstallFolders(env)) Add("Riot Games", install);
            return installs;
        }

        /// <summary>
        /// Which launcher a game came from, judged by where it is installed: Steam by its "steamapps\common" folder, the others by their own
        /// install records, Amazon Games by its folder name. Anything else is "Other".
        /// </summary>
        public static string SourceOf(string gameFolder, IReadOnlyList<LauncherInstall> installs) {
            if (gameFolder.Contains(@"\steamapps\common\", StringComparison.OrdinalIgnoreCase) || gameFolder.Contains("/steamapps/common/", StringComparison.OrdinalIgnoreCase)) {
                return "Steam";
            }

            // The folder holding the .exe can sit deeper than the install folder ("...\Game\bin"), so "inside" counts too
            var hit = installs.FirstOrDefault(i => SourceRules.IsInside(gameFolder, i.Folder));
            if (hit is not null) return hit.Launcher;

            return gameFolder.Contains(@"\Amazon Games\", StringComparison.OrdinalIgnoreCase) ? "Amazon Games" : "Other";
        }

        public static IReadOnlyList<LauncherLibrary> Find(Env env) {
            var libraries = new List<LauncherLibrary>();

            void Add(string launcher, string? path) {
                if (string.IsNullOrWhiteSpace(path)) return;
                string full;
                try { full = System.IO.Path.GetFullPath(path.Trim().Trim('"')); }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return; }
                full = full.TrimEnd(System.IO.Path.DirectorySeparatorChar);

                bool isDriveRoot = full.Length <= 2; // "D:" once the trailing slash is gone: never scan a whole drive
                bool blocked = isDriveRoot || env.SystemFolders.Contains(full);
                if (blocked || !env.DirectoryExists(full)) return;
                if (libraries.Any(l => l.Path.Equals(full, StringComparison.OrdinalIgnoreCase))) return;
                libraries.Add(new LauncherLibrary(launcher, full));
            }

            // Steam already names its library folders exactly
            foreach (string common in SteamLocator.FindCommonFolders(env.SteamPath)) {
                Add("Steam", common);
            }

            // Every other launcher tells us where each installed GAME is; the scan folder is the folder that holds those game folders
            foreach (string install in EpicInstallFolders(env)) Add("Epic Games", RootFor(install));
            foreach (var (_, path) in env.RegistryValues(@"SOFTWARE\WOW6432Node\GOG.com\Games", "path")) Add("GOG", RootFor(path));
            foreach (var (_, path) in env.RegistryValues(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs", "InstallDir")) Add("Ubisoft Connect", RootFor(path));
            foreach (var (_, path) in env.RegistryValues(@"SOFTWARE\WOW6432Node\EA Games", "Install Dir")) Add("EA app", RootFor(path));
            foreach (var (name, path) in env.RegistryValues(@"SOFTWARE\WOW6432Node\Rockstar Games", "InstallFolder")) {
                // the launcher and Social Club are listed next to the games but aren't games
                if (name.Equals("Launcher", StringComparison.OrdinalIgnoreCase) || name.Contains("Social Club", StringComparison.OrdinalIgnoreCase)) continue;
                Add("Rockstar Games", RootFor(path));
            }
            foreach (string install in RiotInstallFolders(env)) Add("Riot Games", RiotRoot(install));

            return libraries;
        }

        /// <summary> The folder that holds a game's folder ("D:\Games\Epic\Fortnite" -> "D:\Games\Epic"). </summary>
        public static string? RootFor(string installFolder) {
            if (string.IsNullOrWhiteSpace(installFolder)) return null;
            string trimmed = installFolder.Trim().Trim('"').Replace('/', '\\').TrimEnd('\\');
            return System.IO.Path.GetDirectoryName(trimmed);
        }

        // Riot installs a product as "...\Riot Games\VALORANT\live", so the game folder is one level higher than the install folder
        private static string? RiotRoot(string installFolder) {
            string? root = RootFor(installFolder);
            return root is not null && System.IO.Path.GetFileName(installFolder.Trim().Replace('/', '\\').TrimEnd('\\')).Equals("live", StringComparison.OrdinalIgnoreCase)
                ? System.IO.Path.GetDirectoryName(root)
                : root;
        }

        // Epic keeps one small JSON "manifest" per installed item. Engines and plugins live in the same place, so only "games" count.
        private static IEnumerable<string> EpicInstallFolders(Env env) {
            string manifests = System.IO.Path.Combine(env.ProgramData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
            foreach (string file in env.FindFiles(manifests, "*.item")) {
                string? json = env.ReadText(file);
                if (json is null) continue;

                string? location = null;
                try {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("AppCategories", out var categories) && categories.ValueKind == JsonValueKind.Array
                        && !categories.EnumerateArray().Any(c => string.Equals(c.GetString(), "games", StringComparison.OrdinalIgnoreCase))) {
                        continue;
                    }
                    if (doc.RootElement.TryGetProperty("InstallLocation", out var loc) && loc.ValueKind == JsonValueKind.String) {
                        location = loc.GetString();
                    }
                }
                catch (JsonException) {
                    continue; // a damaged manifest is simply ignored
                }

                if (!string.IsNullOrWhiteSpace(location)) yield return location;
            }
        }

        // Riot writes "product_install_full_path: "D:/Games/Riot Games/VALORANT/live"" into a small settings file per product
        private static IEnumerable<string> RiotInstallFolders(Env env) {
            string metadata = System.IO.Path.Combine(env.ProgramData, "Riot Games", "Metadata");
            foreach (string file in env.FindFiles(metadata, "*.product_settings.yaml")) {
                string? text = env.ReadText(file);
                if (text is null) continue;
                var match = Regex.Match(text, @"product_install_full_path:\s*""?([^""\r\n]+)""?");
                if (match.Success) yield return match.Groups[1].Value.Trim();
            }
        }

        #region Reading the real PC
        private static Env RealEnv() {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var system = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                programData,
            };
            system.RemoveWhere(string.IsNullOrEmpty);

            return new Env(SteamLocator.FindSteamPath(), programData, system, ReadRegistryValues, FindFilesSafe, ReadTextSafe, Directory.Exists);
        }

        private static IReadOnlyList<(string, string)> ReadRegistryValues(string parentKey, string valueName) {
            var results = new List<(string, string)>();
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 }) {
                try {
                    using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using var parent = baseKey.OpenSubKey(parentKey);
                    if (parent is null) continue;

                    foreach (string name in parent.GetSubKeyNames()) {
                        using var sub = parent.OpenSubKey(name);
                        if (sub?.GetValue(valueName) is string value && !results.Contains((name, value))) {
                            results.Add((name, value));
                        }
                    }
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) {
                    // a key we can't read simply contributes nothing
                }
            }
            return results;
        }

        private static IReadOnlyList<string> FindFilesSafe(string folder, string pattern) {
            try {
                return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories).ToList() : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                return [];
            }
        }

        private static string? ReadTextSafe(string file) {
            try { return File.ReadAllText(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        }
        #endregion
    }
}
