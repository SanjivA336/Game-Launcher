using Game_Launcher.Helpers;
using Microsoft.Win32;
using System.IO;

namespace Game_Launcher.Services {
    /// <summary> A launcher Nexus knows about (it can be linked on the Apps tab), whether or not it is installed. </summary>
    public record KnownAppInfo(string Id, string Name);

    /// <summary> A known launcher that was found on this PC, and where. </summary>
    public record DetectedApp(string KnownId, string Name, string ExePath);

    /// <summary> One entry from Windows' "installed programs" list. </summary>
    public record UninstallEntry(string DisplayName, string InstallLocation, string DisplayIcon);

    /// <summary>
    /// Finds common game launchers (Steam, Epic, GOG Galaxy, ...) so the user doesn't have to browse for them.
    /// Launchers live in different places than games, so it looks in three: the usual install folders, Windows' installed-programs list,
    /// and the Start Menu shortcuts. Read-only: it only reads the registry and lists files.
    /// </summary>
    public static class KnownApps {

        /// <summary> Everything detection reads, gathered up so tests can supply a fake PC. </summary>
        public record Sources(
            string? SteamPath,
            IReadOnlyList<UninstallEntry> Uninstall,
            IReadOnlyList<string> StartMenuShortcuts,
            Func<string, string> ExpandEnv,
            Func<string, ShortcutResolver.Target?> ResolveShortcut,
            Func<string, bool> FileExists);

        private record Known(string Id, string Name, string[] DefaultPaths, string[] ExeNames, string[] RegistryNames, string? ShortcutExe);

        // Environment variables are written as %NAME% and expanded at detection time
        private static readonly Known[] List = [
            // Steam records its install folder in the registry, so it needs no default paths
            new("steam", "Steam", [], ["steam.exe"], ["Steam"], "steam.exe"),
            new("epic", "Epic Games Launcher",
                [@"%ProgramFiles(x86)%\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe", @"%ProgramFiles(x86)%\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"],
                ["EpicGamesLauncher.exe"], ["Epic Games Launcher"], "EpicGamesLauncher.exe"),
            new("gog", "GOG Galaxy",
                [@"%ProgramFiles(x86)%\GOG Galaxy\GalaxyClient.exe", @"%ProgramFiles%\GOG Galaxy\GalaxyClient.exe"],
                ["GalaxyClient.exe"], ["GOG GALAXY"], "GalaxyClient.exe"),
            new("ubisoft", "Ubisoft Connect",
                [@"%ProgramFiles(x86)%\Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe"],
                ["UbisoftConnect.exe", "upc.exe"], ["Ubisoft Connect"], "UbisoftConnect.exe"),
            new("ea", "EA app",
                [@"%ProgramFiles%\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe"],
                ["EADesktop.exe"], ["EA app", "EA Desktop"], "EADesktop.exe"),
            new("battlenet", "Battle.net",
                [@"%ProgramFiles(x86)%\Battle.net\Battle.net Launcher.exe", @"%ProgramFiles(x86)%\Battle.net\Battle.net.exe"],
                ["Battle.net Launcher.exe", "Battle.net.exe"], ["Battle.net"], "Battle.net Launcher.exe"),
            new("amazon", "Amazon Games",
                [@"%LOCALAPPDATA%\Amazon Games\App\Amazon Games.exe"],
                ["Amazon Games.exe"], ["Amazon Games"], "Amazon Games.exe"),
            new("itch", "itch",
                [@"%LOCALAPPDATA%\itch\itch.exe"],
                ["itch.exe"], ["itch"], "itch.exe"),
            new("riot", "Riot Client",
                [@"C:\Riot Games\Riot Client\RiotClientServices.exe"],
                ["RiotClientServices.exe"], ["Riot Client"], null), // a Riot shortcut usually launches one game, so shortcuts aren't matched
            new("rockstar", "Rockstar Games Launcher",
                [@"%ProgramFiles%\Rockstar Games\Launcher\Launcher.exe"],
                ["Launcher.exe"], ["Rockstar Games Launcher"], null), // "Launcher.exe" is too generic a name to match shortcuts by
            new("playnite", "Playnite",
                [@"%LOCALAPPDATA%\Playnite\Playnite.DesktopApp.exe"],
                ["Playnite.DesktopApp.exe"], ["Playnite"], "Playnite.DesktopApp.exe"),
            new("heroic", "Heroic Games Launcher",
                [@"%LOCALAPPDATA%\Programs\heroic\Heroic.exe"],
                ["Heroic.exe"], ["Heroic Games Launcher"], "Heroic.exe"),
            new("minecraft", "Minecraft Launcher",
                [@"%ProgramFiles(x86)%\Minecraft Launcher\MinecraftLauncher.exe"],
                ["MinecraftLauncher.exe"], ["Minecraft Launcher"], "MinecraftLauncher.exe"),
        ];

        /// <summary> Every launcher Nexus can link, in the order the Apps tab lists them. </summary>
        public static IReadOnlyList<KnownAppInfo> Catalog { get; } = List.Select(k => new KnownAppInfo(k.Id, k.Name)).ToList();

        /// <summary> Looks for launchers on this PC. Cheap enough to run every time the Apps page opens (run it off the UI thread). </summary>
        public static IReadOnlyList<DetectedApp> DetectOnThisPc() {
            return Detect(new Sources(
                SteamLocator.FindSteamPath(),
                ReadUninstallEntries(),
                FindStartMenuShortcuts(),
                Environment.ExpandEnvironmentVariables,
                ShortcutResolver.Resolve,
                File.Exists));
        }

        public static IReadOnlyList<DetectedApp> Detect(Sources src) {
            var found = new List<DetectedApp>();

            // Reading a shortcut is a slow call and every launcher checks every shortcut, so each one is read only once
            var resolved = new Dictionary<string, ShortcutResolver.Target?>(StringComparer.OrdinalIgnoreCase);
            var resolveOnce = src.ResolveShortcut;
            src = src with {
                ResolveShortcut = lnk => {
                    if (!resolved.TryGetValue(lnk, out var target)) {
                        target = resolveOnce(lnk);
                        resolved[lnk] = target;
                    }
                    return target;
                }
            };

            foreach (var known in List) {
                var hit = FindBySteamPath(known, src) ?? FindByDefaultPaths(known, src) ?? FindByUninstallList(known, src) ?? FindByShortcut(known, src);
                if (hit is not null && !found.Any(f => f.ExePath.Equals(hit.ExePath, StringComparison.OrdinalIgnoreCase))) {
                    found.Add(hit);
                }
            }

            return found;
        }

        // Steam: its install folder is recorded in the registry, so there is no guessing about where it is
        private static DetectedApp? FindBySteamPath(Known known, Sources src) {
            if (known.Id != "steam" || src.SteamPath is null) return null;
            string steamExe = Path.Combine(src.SteamPath, "steam.exe");
            return src.FileExists(steamExe) ? new DetectedApp(known.Id, known.Name, steamExe) : null;
        }

        private static DetectedApp? FindByDefaultPaths(Known known, Sources src) {
            foreach (string template in known.DefaultPaths) {
                string path = src.ExpandEnv(template);
                if (src.FileExists(path)) {
                    return new DetectedApp(known.Id, known.Name, path);
                }
            }
            return null;
        }

        private static DetectedApp? FindByUninstallList(Known known, Sources src) {
            foreach (var entry in src.Uninstall) {
                bool nameMatches = known.RegistryNames.Any(n => entry.DisplayName.Equals(n, StringComparison.OrdinalIgnoreCase)
                                                                || entry.DisplayName.StartsWith(n + " ", StringComparison.OrdinalIgnoreCase));
                if (!nameMatches) {
                    continue;
                }

                // The list often names the program's icon file, which is the exe itself: "C:\...\Launcher.exe",0
                string icon = entry.DisplayIcon.Split(',')[0].Trim('"', ' ');
                if (icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && src.FileExists(icon)
                    && known.ExeNames.Contains(Path.GetFileName(icon), StringComparer.OrdinalIgnoreCase)) {
                    return new DetectedApp(known.Id, known.Name, icon);
                }

                if (!string.IsNullOrWhiteSpace(entry.InstallLocation)) {
                    foreach (string exeName in known.ExeNames) {
                        string candidate = Path.Combine(entry.InstallLocation.Trim('"'), exeName);
                        if (src.FileExists(candidate)) {
                            return new DetectedApp(known.Id, known.Name, candidate);
                        }
                    }
                }
            }
            return null;
        }

        private static DetectedApp? FindByShortcut(Known known, Sources src) {
            if (known.ShortcutExe is null) {
                return null;
            }

            foreach (string lnk in src.StartMenuShortcuts) {
                var target = src.ResolveShortcut(lnk);
                if (target is not null
                    && Path.GetFileName(target.TargetPath).Equals(known.ShortcutExe, StringComparison.OrdinalIgnoreCase)
                    && src.FileExists(target.TargetPath)) {
                    return new DetectedApp(known.Id, known.Name, target.TargetPath);
                }
            }
            return null;
        }

        #region Reading the real PC
        private static List<UninstallEntry> ReadUninstallEntries() {
            var entries = new List<UninstallEntry>();
            const string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            const string uninstallKey32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

            foreach (var (hive, subKey) in new[] { (Registry.LocalMachine, uninstallKey), (Registry.LocalMachine, uninstallKey32), (Registry.CurrentUser, uninstallKey) }) {
                try {
                    using var key = hive.OpenSubKey(subKey);
                    if (key is null) {
                        continue;
                    }

                    foreach (string name in key.GetSubKeyNames()) {
                        using var app = key.OpenSubKey(name);
                        if (app?.GetValue("DisplayName") is string displayName) {
                            entries.Add(new UninstallEntry(displayName, app.GetValue("InstallLocation") as string ?? string.Empty, app.GetValue("DisplayIcon") as string ?? string.Empty));
                        }
                    }
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) {
                    // A hive we can't read: the other two places still count
                }
            }
            return entries;
        }

        private static List<string> FindStartMenuShortcuts() {
            var roots = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            };

            var shortcuts = new List<string>();
            foreach (string root in roots.Where(Directory.Exists)) {
                try {
                    shortcuts.AddRange(Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    // Some sub-folder wasn't readable: use what we have
                }
            }
            return shortcuts;
        }
        #endregion
    }
}
