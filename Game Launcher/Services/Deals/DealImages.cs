using System.Net.Http;
using System.IO;
using System.Net;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Game_Launcher.Services.Deals {
    /// <summary>
    /// Loads game pictures and provider logos straight into memory. Nothing is written to disk, so closing Nexus forgets them
    /// (deal images are only ever shown, never stored).
    /// </summary>
    public static class DealImages {

        private const int MaxImageBytes = 3 * 1024 * 1024;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(12);

        // Logos are tiny and shared by many cards, so they're kept for the session: one download per provider
        private static readonly Dictionary<string, Task<ImageSource?>> LogoCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary> Only https pictures from named hosts: no plain http, no bare IP addresses, no local addresses. </summary>
        public static bool IsAllowedImageUrl(string? url) {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                   && uri.Scheme == Uri.UriSchemeHttps
                   && uri.HostNameType == UriHostNameType.Dns
                   && !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary> Downloads and decodes a picture. Returns null on any problem: the card just keeps its placeholder. </summary>
        /// <param name="allow"> Address check; tests pass a looser one so a local fake server can be used.</param>
        public static async Task<ImageSource?> LoadAsync(HttpClient http, string? url, int decodeWidth, Func<string?, bool>? allow = null, CancellationToken ct = default) {
            if (!(allow ?? IsAllowedImageUrl)(url)) {
                return null;
            }

            try {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(Timeout);

                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxImageBytes) {
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, timeout.Token);
                if (buffer.Length == 0 || buffer.Length > MaxImageBytes) {
                    return null;
                }

                buffer.Position = 0;
                return await Task.Run(() => Decode(buffer, decodeWidth), timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or NotSupportedException or FileFormatException or ArgumentException) {
                return null;
            }
        }

        private static ImageSource? Decode(Stream data, int decodeWidth) {
            try {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // read everything now, so the stream can go
                image.StreamSource = data;
                if (decodeWidth > 0) {
                    image.DecodePixelWidth = decodeWidth; // keeps big store images from using much memory
                }
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException) {
                return null;
            }
        }

        /// <summary> A provider's logo, fetched once from its own website's favicon. Null when it has none (the card shows a letter badge instead). </summary>
        public static Task<ImageSource?> LoadLogoAsync(HttpClient http, string host) {
            lock (LogoCache) {
                if (!LogoCache.TryGetValue(host, out var task)) {
                    task = LoadFaviconAsync(http, host);
                    LogoCache[host] = task;

                    // A failure isn't remembered, so a logo missed while offline is fetched again on a later visit
                    _ = task.ContinueWith(t => {
                        if (t.Result is null) {
                            lock (LogoCache) { LogoCache.Remove(host); }
                        }
                    }, TaskScheduler.Default);
                }
                return task;
            }
        }

        private static async Task<ImageSource?> LoadFaviconAsync(HttpClient http, string host) {
            string url = $"https://{host}/favicon.ico";
            try {
                using var timeout = new CancellationTokenSource(Timeout);
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 256 * 1024) {
                    return null;
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
                return await Task.Run(() => DecodeIcon(bytes));
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException) {
                return null;
            }
        }

        // A .ico holds several sizes; take the largest one
        private static ImageSource? DecodeIcon(byte[] bytes) {
            try {
                var decoder = BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
                frame.Freeze();
                return frame;
            }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException) {
                return null;
            }
        }
    }
}
