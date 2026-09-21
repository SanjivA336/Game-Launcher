using Game_Launcher.Helpers;
using Game_Launcher.Models;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace Game_Launcher.Services {
    /// <summary>
    /// Saves cover images in UserData/Covers. Files are named after the game so they can be found and replaced without any bookkeeping:
    ///
    ///     Trackmania-a1b2c3d4.cover.png    (downloaded automatically)
    ///     Trackmania-a1b2c3d4.custom.jpg   (chosen by the user)
    ///
    /// "Trackmania" is the game's exe name (so the folder is readable), "a1b2c3d4" is a short fingerprint of the game's folder path
    /// (two games can share an exe name), and "cover"/"custom" says who picked the image. Saving a cover for a game deletes every
    /// other file carrying that game's fingerprint, so a new cover replaces the old one no matter what extension or exe name it had.
    /// Deleting a game does NOT delete its cover: if the game comes back, its cover is picked up again.
    /// </summary>
    public static class CoverStore {
        private const int MaxImageHeight = 900; // covers are shown at most ~300 px tall; 900 keeps them sharp on high-DPI screens

        /// <summary> A short, stable fingerprint of a game's folder path. </summary>
        public static string GameKey(string gameDirPath) {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(gameDirPath.ToLowerInvariant()));
            return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
        }

        /// <summary> The file name a game's cover gets, e.g. "Trackmania-a1b2c3d4.cover.png". </summary>
        public static string FileNameFor(GameMapping game, string extension, bool custom) {
            string exe = Path.GetFileNameWithoutExtension(game.PrimaryExecutable?.Name ?? string.Empty);
            string readable = Regex.Replace(exe, @"[^\w\-]+", "_").Trim('_', '-', '.');
            if (readable.Length == 0) {
                readable = "game";
            }
            if (readable.Length > 40) {
                readable = readable[..40];
            }

            if (!extension.StartsWith('.')) {
                extension = "." + extension;
            }

            return $"{readable}-{GameKey(game.DirPathRaw)}.{(custom ? "custom" : "cover")}{extension.ToLowerInvariant()}";
        }

        /// <summary> True for file names in the current style ("Trackmania-a1b2c3d4.cover.png"); false for older ones that were only a fingerprint. </summary>
        public static bool IsCurrentName(string fileName) => Regex.IsMatch(fileName, @"^[\w\-]+-[0-9a-f]{8}\.(cover|custom)\.[A-Za-z]+$");

        private static IEnumerable<string> FilesFor(string gameDirPath) {
            return Directory.Exists(CoverArt.CoversDirectory)
                ? Directory.EnumerateFiles(CoverArt.CoversDirectory, $"*-{GameKey(gameDirPath)}.*")
                : Enumerable.Empty<string>();
        }

        /// <summary> Saves an image as the game's cover, replacing any previous cover for that game. Returns the new file name. </summary>
        public static async Task<string> SaveAsync(GameMapping game, byte[] image, string extension, bool custom, CancellationToken ct = default) {
            Directory.CreateDirectory(CoverArt.CoversDirectory);

            string fileName = FileNameFor(game, extension, custom);
            string path = CoverArt.FullPath(fileName);

            // Write to a temp file first so a half-written image can never be shown
            string tempPath = path + ".tmp";
            await File.WriteAllBytesAsync(tempPath, image, ct);
            File.Move(tempPath, path, overwrite: true);

            // The replacement rule: everything else with this game's fingerprint is now out of date
            RemoveAllExcept(game.DirPathRaw, fileName);
            return fileName;
        }

        /// <summary> Deletes every cover file belonging to a game (used when the user goes back to an automatic cover). </summary>
        public static void DeleteFor(string gameDirPath) => RemoveAllExcept(gameDirPath, keepFileName: null);

        private static void RemoveAllExcept(string gameDirPath, string? keepFileName) {
            foreach (string file in FilesFor(gameDirPath).ToList()) {
                if (keepFileName is not null && Path.GetFileName(file).Equals(keepFileName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                try {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    Debug.WriteLine($"Could not remove old cover {file}: {ex.Message}");
                }
            }
        }

        /// <summary> Finds a cover file left over from an earlier time this game was in the library (custom ones win). </summary>
        public static (string FileName, bool IsCustom)? FindExisting(string gameDirPath) {
            var found = FilesFor(gameDirPath)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Select(name => (Name: name, Match: Regex.Match(name, @"\.(cover|custom)\.(png|jpg|jpeg)$", RegexOptions.IgnoreCase)))
                .Where(x => x.Match.Success)
                .OrderByDescending(x => x.Match.Groups[1].Value.Equals("custom", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            return found.Name is null ? null : (found.Name, found.Match.Groups[1].Value.Equals("custom", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Moves a game's cover to the file name it needs after the game's folder changed (the name contains a fingerprint of the folder,
        /// so the old file would otherwise be orphaned and the game would look like it has no cover).
        /// </summary>
        /// <param name="oldDirPath"> The folder the game used to be in.</param>
        /// <param name="game"> The game, already updated to its new folder and executables.</param>
        /// <returns> The cover's new file name, or null if the game had no cover file.</returns>
        public static string? MoveForNewFolder(string oldDirPath, GameMapping game) {
            var existing = FindExisting(oldDirPath);
            if (existing is null) {
                return null;
            }

            string newName = FileNameFor(game, Path.GetExtension(existing.Value.FileName), existing.Value.IsCustom);
            string from = CoverArt.FullPath(existing.Value.FileName);
            string to = CoverArt.FullPath(newName);

            if (!from.Equals(to, StringComparison.OrdinalIgnoreCase)) {
                File.Move(from, to, overwrite: true); // this game's own cover wins over anything left behind for the new folder
            }

            RemoveAllExcept(oldDirPath, keepFileName: null);   // leftovers under the old fingerprint
            RemoveAllExcept(game.DirPathRaw, newName);          // leftovers under the new one
            return newName;
        }

        /// <summary> Copies a user's image in as the game's custom cover: any common image format, resized down if huge, stored as PNG. The original file is only read. </summary>
        /// <exception cref="InvalidOperationException"> The file couldn't be read as an image.</exception>
        public static async Task<string> ImportAsync(GameMapping game, string sourcePath, CancellationToken ct = default) {
            byte[] png;
            try {
                png = await Task.Run(() => NormalizeToPng(sourcePath), ct);
            }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or IOException or ArgumentException or UriFormatException) {
                throw new InvalidOperationException("That file couldn't be read as an image. Try a PNG, JPG, BMP, GIF or TIFF.", ex);
            }

            return await SaveAsync(game, png, ".png", custom: true, ct);
        }

        private static byte[] NormalizeToPng(string sourcePath) {
            var uri = new Uri(sourcePath);

            // Ask for the size first so only large images get shrunk (never enlarge a small one)
            int height = BitmapDecoder.Create(uri, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.None).Frames[0].PixelHeight;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // read everything now so the source file isn't left locked
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // the user may have edited the file since it was last read
            bitmap.UriSource = uri;
            if (height > MaxImageHeight) {
                bitmap.DecodePixelHeight = MaxImageHeight;
            }
            bitmap.EndInit();
            bitmap.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
    }
}
