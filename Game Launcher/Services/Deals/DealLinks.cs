using System.Diagnostics;

namespace Game_Launcher.Services.Deals {
    /// <summary> Decides which web addresses a deal may open, and opens them. Deals only ever open a web page; they never run anything. </summary>
    public static class DealLinks {

        // A link is only opened when it is https and on one of these sites (or a sub-domain of one)
        private static readonly string[] AllowedHosts = [
            "steampowered.com",
            "epicgames.com",
            "gamerpower.com",
            "cheapshark.com",
        ];

        public static bool IsSafe(string? url) {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps) {
                return false;
            }

            return AllowedHosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
                                            || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));
        }

        /// <returns> True if the browser was asked to open the page; false if the link isn't allowed or couldn't be opened.</returns>
        public static bool Open(string url) {
            if (!IsSafe(url)) {
                return false;
            }

            try {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                return true;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) {
                Debug.WriteLine($"Could not open {url}: {ex.Message}");
                return false;
            }
        }
    }
}
