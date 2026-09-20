using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Game_Launcher.Helpers {
    /// <summary>
    /// Asks Windows 11's window compositor (DWM) to draw our borderless windows the native way:
    /// rounded corners, a dark frame, and the Mica backdrop (a translucent material tinted by the wallpaper).
    /// On older Windows these calls are skipped and the window simply stays square with a solid background.
    /// </summary>
    public static class WindowStyler {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        private const int DWMWCP_ROUND = 2;
        private const int DWMSBT_MAINWINDOW = 2; // Mica

        private const int Windows11Build = 22000;
        private const int Windows11MicaBuild = 22621; // the system backdrop setting arrived in 22H2

        /// <summary> Applies rounded corners, dark mode and Mica. Call once the window's handle exists (SourceInitialized). </summary>
        public static void Apply(Window window) {
            IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();
            int build = Environment.OSVersion.Version.Build;

            int on = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));

            if (build < Windows11Build) {
                return;
            }

            int round = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

            if (build >= Windows11MicaBuild) {
                int mica = DWMSBT_MAINWINDOW;
                if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref mica, sizeof(int)) == 0) {
                    // The window's solid fallback color would hide Mica, so make the background see-through
                    window.Background = Brushes.Transparent;
                }
            }
        }
    }
}
