using Game_Launcher.Models;
using System.IO;

namespace Game_Launcher.Services {
    /// <summary>
    /// Answers "why isn't my game found?" for a folder, by walking the same rules the scanner uses and saying which one stops it.
    /// Read-only: it only lists folders and files.
    /// </summary>
    public static class ScanDiagnostics {

        /// <param name="Found"> True when the scanner would list at least one game in (or at) this folder.</param>
        /// <param name="Lines"> The explanation, one sentence per line.</param>
        public record Report(bool Found, IReadOnlyList<string> Lines);

        public static Report Explain(string folder, Preferences prefs) {
            var lines = new List<string>();
            var dir = new DirectoryInfo(folder);

            if (!dir.Exists) {
                lines.Add("That folder doesn't exist right now. If the game is on another drive, check that it's connected.");
                return new Report(false, lines);
            }

            var roots = prefs.Roots.Select(r => r.FullName).ToList();
            string? root = roots.Where(r => SourceRules.IsInside(dir.FullName, r)).OrderByDescending(r => r.Length).FirstOrDefault();

            if (root is null) {
                var inner = roots.Where(r => SourceRules.IsInside(r, dir.FullName)).ToList();
                if (inner.Count > 0) {
                    lines.Add($"This folder contains one of your scan folders ({inner[0]}). Pick the game's own folder instead.");
                }
                else {
                    string? parent = dir.Parent?.FullName;
                    lines.Add("It isn't inside any of your scan folders, so Nexus never looks there.");
                    lines.Add(parent is null
                        ? "Add this folder as a scan folder."
                        : $"Add this folder or its parent ({parent}) as a scan folder.");
                }
                return new Report(false, lines);
            }

            // Walk from the scan folder down to this folder, checking each step the way the scanner does
            var chain = new List<DirectoryInfo>();
            for (var d = dir; d is not null && !d.FullName.Equals(root, StringComparison.OrdinalIgnoreCase); d = d.Parent) {
                chain.Add(d);
            }
            chain.Add(new DirectoryInfo(root));
            chain.Reverse(); // scan folder first, chosen folder last

            foreach (var step in chain) {
                var excluded = prefs.Excludes.FirstOrDefault(e => e.FullName.Equals(step.FullName, StringComparison.OrdinalIgnoreCase));
                if (excluded is not null) {
                    lines.Add($"\"{excluded.FullName}\" is in your excluded folders, so Nexus skips it and everything inside it.");
                    return new Report(false, lines);
                }

                if (GameMappingManager.IsIgnoredPath(step, prefs)) {
                    string keyword = prefs.Ignores.First(k => step.Name.Contains(k, StringComparison.OrdinalIgnoreCase));
                    lines.Add($"The folder \"{step.Name}\" is skipped because its name contains the ignore word \"{keyword}\". Remove that word to include it.");
                    return new Report(false, lines);
                }

                // A folder holding a usable .exe counts as a game, and the scanner doesn't look any deeper
                if (!step.FullName.Equals(dir.FullName, StringComparison.OrdinalIgnoreCase)) {
                    var (usable, _) = ClassifyExecutables(step, prefs);
                    if (usable.Count > 0) {
                        lines.Add($"\"{step.FullName}\" already holds {usable[0].Name}, so Nexus treats it as one game and doesn't look inside it. This folder is part of that game.");
                        return new Report(false, lines);
                    }
                }
            }

            // The folder itself
            var (usableHere, skippedHere) = ClassifyExecutables(dir, prefs);
            if (usableHere.Count > 0) {
                lines.Add($"Found: Nexus lists this folder as a game (launcher: {usableHere[0].Name}).");
                return new Report(true, lines);
            }

            if (skippedHere.Count > 0) {
                lines.Add($"This folder has {skippedHere.Count} .exe file(s), but every one was skipped by an ignore word: "
                          + string.Join(", ", skippedHere.Take(4).Select(s => $"{s.File.Name} (\"{s.Keyword}\")")) + ".");
            }
            else {
                lines.Add("There are no .exe files directly in this folder. Nexus only launches .exe files (not shortcuts or .bat files).");
            }

            // The scanner would keep looking in subfolders; ask it what it would find there
            var sub = prefs.Clone();
            sub.SetRoots([dir.FullName]);
            sub.ClearExcludes();
            var subExes = GameMappingManager.FindExecutables(sub);
            if (subExes.Count > 0) {
                var gameFolders = subExes.Select(f => f.Directory!.FullName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                lines.Add($"Nexus does find {gameFolders.Count} game folder(s) inside it: " + string.Join(", ", gameFolders.Take(3).Select(g => Path.GetFileName(g))) + (gameFolders.Count > 3 ? ", ..." : "."));
                return new Report(true, lines);
            }

            lines.Add("No usable .exe files were found anywhere inside it either.");
            return new Report(false, lines);
        }

        /// <summary> Splits a folder's .exe files into usable ones and ones skipped because of an ignore word. </summary>
        private static (List<FileInfo> Usable, List<(FileInfo File, string Keyword)> Skipped) ClassifyExecutables(DirectoryInfo dir, Preferences prefs) {
            var usable = new List<FileInfo>();
            var skipped = new List<(FileInfo, string)>();
            try {
                foreach (var file in dir.GetFiles("*.exe")) {
                    string? keyword = prefs.Ignores.FirstOrDefault(k => file.Name.Contains(k, StringComparison.OrdinalIgnoreCase));
                    if (keyword is null) {
                        usable.Add(file);
                    }
                    else {
                        skipped.Add((file, keyword));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                // Unreadable folder: reported as "no .exe files", which is what the scanner would see too
            }
            return (usable, skipped);
        }
    }
}
