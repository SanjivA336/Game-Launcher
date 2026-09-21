using System.IO;
using System.Text.RegularExpressions;

namespace Game_Launcher.Services.Steam {
    /// <summary>
    /// Finds a Steam game's app ID from Steam's own install records on disk. Read-only: it only reads the small
    /// "appmanifest_*.acf" files that sit next to the game folders. No network is involved, and the match is exact.
    /// </summary>
    public static class SteamAppIds {

        /// <param name="gameFolder"> The game's folder, e.g. "D:\Games\Steam\steamapps\common\Terraria".</param>
        /// <returns> The app ID, or null when the folder isn't inside a Steam library (or Steam has no record of it).</returns>
        public static int? FromGameFolder(string gameFolder) {
            const string marker = @"\steamapps\common\";
            int at = gameFolder.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;

            string steamApps = gameFolder[..(at + @"\steamapps".Length)];
            string installDir = gameFolder[(at + marker.Length)..].Split('\\', '/')[0];
            if (installDir.Length == 0 || !Directory.Exists(steamApps)) return null;

            try {
                foreach (string manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf")) {
                    string text = File.ReadAllText(manifest);
                    var dir = Regex.Match(text, "\"installdir\"\\s+\"([^\"]+)\"");
                    var id = Regex.Match(text, "\"appid\"\\s+\"(\\d+)\"");
                    if (dir.Success && id.Success && dir.Groups[1].Value.Equals(installDir, StringComparison.OrdinalIgnoreCase)) {
                        return int.Parse(id.Groups[1].Value);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                // unreadable library: treated as "no record"
            }
            return null;
        }
    }
}
