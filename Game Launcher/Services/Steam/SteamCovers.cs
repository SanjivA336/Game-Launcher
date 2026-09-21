using System.Net;
using System.Net.Http;

namespace Game_Launcher.Services.Steam {
    /// <summary>
    /// Gets a game's portrait cover from Steam's content servers. No key or account is needed. Steam games are matched exactly through
    /// their local install record; other games by a close title match. Games Steam has no portrait for (some new or old ones) come back as
    /// "not found", and the caller falls back to something else.
    /// </summary>
    public class SteamCovers {
        private const int MaxImageBytes = 3 * 1024 * 1024;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        private readonly HttpClient _http;
        private readonly SteamTagSuggester _resolver;
        private readonly Uri _cdn;

        /// <param name="cdnBase"> Where the images are served from; a test can point it at a local fake.</param>
        public SteamCovers(HttpClient http, SteamTagSuggester resolver, Uri? cdnBase = null) {
            _http = http;
            _resolver = resolver;
            _cdn = cdnBase ?? new Uri("https://cdn.cloudflare.steamstatic.com/");
        }

        /// <summary> The covers the app uses: real Steam. </summary>
        public static SteamCovers Shared { get; } = new(Deals.DealsService.CreateHttpClient(), SteamTagSuggester.Shared);

        public async Task<CoverDownload> FindCoverAsync(string gameName, string gameFolder, CancellationToken ct = default) {
            try {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(Timeout);

                var (appId, steamName) = await _resolver.ResolveAsync(gameName, gameFolder, timeout.Token);
                if (appId is null) {
                    return new CoverDownload(CoverOutcome.NotFound);
                }

                var url = new Uri(_cdn, $"steam/apps/{appId}/library_600x900.jpg");
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden) {
                    return new CoverDownload(CoverOutcome.NotFound); // Steam simply has no portrait for this one
                }
                if (!response.IsSuccessStatusCode) {
                    return new CoverDownload(CoverOutcome.Unavailable, Detail: $"Steam answered {(int)response.StatusCode}");
                }
                if (response.Content.Headers.ContentLength > MaxImageBytes) {
                    return new CoverDownload(CoverOutcome.NotFound);
                }

                byte[] image = await response.Content.ReadAsByteArrayAsync(timeout.Token);

                // A JPEG starts with FF D8; anything else (an error page) is not a cover
                if (image.Length < 2000 || image.Length > MaxImageBytes || image[0] != 0xFF || image[1] != 0xD8) {
                    return new CoverDownload(CoverOutcome.NotFound);
                }

                return new CoverDownload(CoverOutcome.Found, image, ".jpg", steamName ?? gameName);
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or FormatException or InvalidOperationException
                                       || (ex is OperationCanceledException && !ct.IsCancellationRequested)) {
                return new CoverDownload(CoverOutcome.Unavailable, Detail: ex.Message);
            }
        }
    }
}
