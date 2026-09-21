using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Game_Launcher.Helpers {
    /// <summary> Everything about how a game's cover is stored and drawn: downloaded images, or a generated placeholder when there is none. </summary>
    public static class CoverArt {

        /// <summary> Downloaded covers live here. These are Nexus's own files (next to mappings.json), never inside a game's folder. </summary>
        public static string CoversDirectory => AppPaths.CoversDirectory;

        /// <summary> Full path of a cover file. Only the file NAME of the input is used, so a doctored mappings.json can't point outside the covers folder. </summary>
        public static string FullPath(string fileName) => Path.Combine(CoversDirectory, Path.GetFileName(fileName));

        // Decoding a 600x900 PNG takes a while and the library rebuilds its tiles on every keystroke, so decoded covers are kept.
        // The key includes the file's last-write time, so a re-downloaded cover is picked up automatically.
        private static readonly Dictionary<(string Path, long Stamp), ImageBrush> ImageCache = new();

        /// <summary> The brush to paint a game's cover with: its downloaded image if it has one, otherwise a generated gradient. </summary>
        /// <param name="hasImage"> True if a real downloaded cover is being used (so the placeholder's big letter should be hidden).</param>
        public static Brush CreateCoverBrush(string seed, string? coverFileName, out bool hasImage) {
            ImageBrush? image = TryLoadImage(coverFileName);
            hasImage = image is not null;
            return image ?? CreateBrush(seed);
        }

        private static ImageBrush? TryLoadImage(string? coverFileName) {
            if (string.IsNullOrWhiteSpace(coverFileName)) {
                return null;
            }

            string path = FullPath(coverFileName);
            if (!File.Exists(path)) {
                return null;
            }

            var key = (path, File.GetLastWriteTimeUtc(path).Ticks);
            if (ImageCache.TryGetValue(key, out var cached)) {
                return cached;
            }

            try {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // read the whole file now, so the file isn't locked and can be replaced later
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // WPF otherwise remembers images by file path, and a cover is replaced under the same name
                bitmap.DecodePixelWidth = 360;                 // covers show at most ~200 px wide; no need to keep 600 px in memory
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();

                var brush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                brush.Freeze();
                ImageCache[key] = brush;
                return brush;
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or InvalidOperationException) {
                return null; // a damaged image file just means "no cover"
            }
        }

        /// <summary> Builds a dark two-tone gradient whose color is derived from <paramref name="seed"/> (e.g. the game's folder path). </summary>
        /// <remarks>
        /// The hash is hand-written on purpose: string.GetHashCode() is randomized every time a .NET app starts,
        /// which would give every game a different color on every launch.
        /// </remarks>
        public static Brush CreateBrush(string seed) {
            uint hash = 2166136261; // FNV-1a: a tiny, stable string hash
            foreach (char c in seed.ToLowerInvariant()) {
                hash = (hash ^ c) * 16777619;
            }

            double hue = hash % 360;
            var brush = new LinearGradientBrush(FromHsl(hue, 0.42, 0.30), FromHsl((hue + 35) % 360, 0.48, 0.15), 60);
            brush.Freeze(); // frozen brushes are cheaper and safe to share
            return brush;
        }

        /// <summary> Converts hue (0-360), saturation and lightness (0-1) to a Color. </summary>
        private static Color FromHsl(double hue, double saturation, double lightness) {
            double chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            double x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
            double m = lightness - chroma / 2;

            (double r, double g, double b) = (int)(hue / 60) switch {
                0 => (chroma, x, 0.0),
                1 => (x, chroma, 0.0),
                2 => (0.0, chroma, x),
                3 => (0.0, x, chroma),
                4 => (x, 0.0, chroma),
                _ => (chroma, 0.0, x),
            };

            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }
    }
}
