using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Game_Launcher.Helpers {
    /// <summary> Gets a program's own icon so the Apps page looks like your real launchers. Icons live in memory only; nothing is saved. </summary>
    public static class IconExtractor {

        private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

        /// <returns> The icon of the program at <paramref name="exePath"/>, or null when the file is missing or has no readable icon.</returns>
        public static ImageSource? For(string exePath) {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) {
                return null;
            }

            return Cache.GetOrAdd(exePath, Extract);
        }

        /// <summary> Forgets a cached icon (used when an app is pointed at a different program). </summary>
        public static void Forget(string exePath) => Cache.TryRemove(exePath, out _);

        private static ImageSource? Extract(string exePath) {
            // Windows' own "what icon does this file have?" call: works for any exe, and falls back to the default program icon
            var info = new ShFileInfo();
            IntPtr result = SHGetFileInfo(exePath, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiLargeIcon);
            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) {
                return null;
            }

            try {
                BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze(); // frozen images can be used from any thread
                return source;
            }
            catch (Exception ex) when (ex is ArgumentException or COMException or InvalidOperationException) {
                return null;
            }
            finally {
                DestroyIcon(info.hIcon); // the copy WPF made is independent; the handle must be released
            }
        }

        private const uint ShgfiIcon = 0x100;
        private const uint ShgfiLargeIcon = 0x0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShFileInfo {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, ref ShFileInfo info, uint size, uint flags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
