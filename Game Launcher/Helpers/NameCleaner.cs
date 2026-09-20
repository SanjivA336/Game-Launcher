using System.Text.RegularExpressions;

namespace Game_Launcher.Helpers {
    /// <summary>
    /// Turns folder-style game names into readable ones using simple, predictable rules (no guessing, no lookups):
    /// <list type="number">
    /// <item> a trailing website tag is removed ("Game-SomeSite.com" becomes "Game") </item>
    /// <item> "-", "_", "." (and "~", "|") become spaces </item>
    /// <item> a capital letter right after a lowercase letter gets a space before it ("BelowZero" becomes "Below Zero") </item>
    /// <item> a digit right after a letter gets a space before it ("Titanfall2" becomes "Titanfall 2") </item>
    /// </list>
    /// The same input always gives the same output, and cleaning an already-clean name changes nothing.
    /// Known limits of rule 3: names that use capitals on purpose, like "DiRT" or "iRacing", get split.
    /// </summary>
    public static class NameCleaner {
        public static string Clean(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                return name;
            }

            string s = name;

            // 1. Site tags that download sites add to folder names. Needs a separator before it, so "Foo.com" alone is untouched.
            s = Regex.Replace(s, @"[\s._-]+[A-Za-z0-9]+\.(com|net|org|io|gg|to|me|cc|xyz)\s*$", string.Empty, RegexOptions.IgnoreCase);

            // 2. Separators become spaces
            s = Regex.Replace(s, @"[-_.~|]+", " ");

            // 3. lowercase followed by Capital: add a space between them
            s = Regex.Replace(s, @"(?<=\p{Ll})(?=\p{Lu})", " ");

            // 4. letter followed by a digit: add a space between them
            s = Regex.Replace(s, @"(?<=\p{L})(?=\d)", " ");

            s = Regex.Replace(s, @"\s+", " ").Trim();

            // Never turn a name into nothing (e.g. a folder literally called "...")
            return s.Length == 0 ? name.Trim() : s;
        }
    }
}
