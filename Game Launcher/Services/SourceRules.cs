using System.IO;

namespace Game_Launcher.Services {
    /// <summary> Small rules applied to folders the user adds as scan folders. </summary>
    public static class SourceRules {

        /// <summary> A folder to scan, plus an explanation when it differs from what the user picked. </summary>
        public record NormalizedRoot(string Path, string? Notice);

        /// <summary>
        /// Cleans up a folder the user wants to scan. Mostly this is the "Steam folder gotcha": Steam's own folder contains
        /// steam.exe, so scanning it would treat the whole of Steam as ONE game and never look at the real games.
        /// The games live in "steamapps\common", so that's what gets used instead.
        /// </summary>
        public static NormalizedRoot NormalizeNewRoot(string path) {
            string full = Path.GetFullPath(path);

            // Drop a trailing slash ("D:\Games\") but keep a drive root ("D:\") intact
            string? root = Path.GetPathRoot(full);
            if (!string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) {
                full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            string common = Path.Combine(full, "steamapps", "common");

            if (File.Exists(Path.Combine(full, "steam.exe"))) {
                return Directory.Exists(common)
                    ? new NormalizedRoot(common, "That's the Steam folder itself, so Nexus added its steamapps\\common folder (where Steam keeps your games) instead.")
                    : new NormalizedRoot(full, "That looks like the Steam folder, but it has no steamapps\\common folder yet, so no games will be found in it.");
            }

            if (Directory.Exists(common)) {
                return new NormalizedRoot(common, "That's a Steam library, so Nexus added its steamapps\\common folder (where Steam keeps the games) instead.");
            }

            return new NormalizedRoot(full, null);
        }

        /// <summary> True when <paramref name="path"/> is <paramref name="folder"/> itself or somewhere inside it. </summary>
        public static bool IsInside(string path, string folder) {
            string prefix = folder.EndsWith(Path.DirectorySeparatorChar) ? folder : folder + Path.DirectorySeparatorChar;
            return path.Equals(folder, StringComparison.OrdinalIgnoreCase) || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary> Counts how many games sit under each root. A game overlapped by several roots is counted once, under the deepest one. </summary>
        public static Dictionary<string, int> CountGamesPerRoot(IEnumerable<string> roots, IEnumerable<string> gameFolders) {
            var rootList = roots.ToList();
            var counts = rootList.ToDictionary(r => r, _ => 0, StringComparer.OrdinalIgnoreCase);

            foreach (string game in gameFolders) {
                string? best = rootList
                    .Where(r => IsInside(game, r))
                    .OrderByDescending(r => r.Length)
                    .FirstOrDefault();
                if (best is not null) {
                    counts[best]++;
                }
            }
            return counts;
        }
    }
}
