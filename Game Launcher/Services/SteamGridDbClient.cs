using Game_Launcher.Helpers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Game_Launcher.Services {
    public enum CoverOutcome {
        Found,
        /// <summary> The site doesn't know this game (or has no cover for it). Asking again won't help until the name changes. </summary>
        NotFound,
        /// <summary> The API key was rejected. </summary>
        InvalidKey,
        /// <summary> No internet, a timeout, rate limiting, or a server error. Worth retrying later. </summary>
        Unavailable,
    }

    /// <summary> The result of one call to the site: what happened, the value if it worked, and a short technical reason if it didn't. </summary>
    public record ClientResult<T>(CoverOutcome Outcome, T? Value = default, string? Detail = null);

    /// <summary> A game found by searching SteamGridDB. </summary>
    public record GameSearchResult(long Id, string Name, bool Verified) {
        // Screen readers read a list item's name from ToString(); a record's default would be a long debug string
        public override string ToString() => Name;
    }

    /// <summary> One cover image available for a game. </summary>
    public record CoverOption(long Id, string Url, string ThumbUrl, string Extension, int Width, int Height, string Style, string Author);

    /// <summary> One page of covers (the site returns them in pages of up to 50). </summary>
    public record CoverPage(IReadOnlyList<CoverOption> Covers, int Page, int Total, int Limit) {
        public bool HasMore => (Page + 1) * Limit < Total;
    }

    /// <summary> An automatic lookup's outcome, including the downloaded image when it worked. </summary>
    public record CoverDownload(CoverOutcome Outcome, byte[]? Image = null, string? Extension = null, string? MatchedName = null, string? Detail = null);

    /// <summary> Talks to SteamGridDB (steamgriddb.com) through its official API: searching games, listing covers, downloading them. </summary>
    public class SteamGridDbClient {
        public const string DefaultBaseUrl = "https://www.steamgriddb.com/api/v2";

        // Portrait covers only (they fit the library's portrait cards), still images only, in formats WPF can display (it can't read WebP)
        private const string GridQuery = "dimensions=600x900,342x482,660x930&mimes=image/png,image/jpeg&types=static&nsfw=false&humor=false";

        // One shared HttpClient for the whole app: it keeps connections open and reuses them, which is what Microsoft recommends
        // (creating a new one per lookup wastes connections). Tests can pass their own handler instead.
        private static readonly HttpClient SharedHttp = CreateHttpClient(null);

        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public SteamGridDbClient(string apiKey, string? baseUrl = null, HttpMessageHandler? handler = null) {
            _apiKey = apiKey;
            _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
            _http = handler is null ? SharedHttp : CreateHttpClient(handler);
        }

        private static HttpClient CreateHttpClient(HttpMessageHandler? handler) {
            var http = handler is null ? new HttpClient() : new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("NexusLauncher/1.0");
            return http;
        }

        #region Building blocks (also used by the cover picker window)

        /// <summary> Searches the site for games matching some text. </summary>
        public Task<ClientResult<IReadOnlyList<GameSearchResult>>> SearchGamesAsync(string term, CancellationToken ct = default) {
            return GuardAsync<IReadOnlyList<GameSearchResult>>(async () => {
                string cleaned = term.Replace('/', ' ').Trim();
                if (cleaned.Length == 0) {
                    return new(CoverOutcome.NotFound);
                }

                var (failure, detail, root) = await GetJsonAsync("/search/autocomplete/" + Uri.EscapeDataString(cleaned), ct);
                if (failure is not null) {
                    return new(failure.Value, Detail: detail);
                }

                var games = root.GetProperty("data").EnumerateArray()
                    .Select(e => new GameSearchResult(
                        e.GetProperty("id").GetInt64(),
                        e.GetProperty("name").GetString() ?? string.Empty,
                        e.TryGetProperty("verified", out var v) && v.ValueKind == JsonValueKind.True))
                    .ToList();

                return new(games.Count == 0 ? CoverOutcome.NotFound : CoverOutcome.Found, games);
            });
        }

        /// <summary> Lists the covers available for a game (page 0 is the first page). </summary>
        public Task<ClientResult<CoverPage>> GetCoversAsync(long gameId, int page = 0, CancellationToken ct = default) {
            return GuardAsync<CoverPage>(async () => {
                var (failure, detail, root) = await GetJsonAsync($"/grids/game/{gameId}?{GridQuery}&page={page}", ct);
                if (failure is not null) {
                    return new(failure.Value, Detail: detail);
                }

                var covers = new List<CoverOption>();
                foreach (var grid in root.GetProperty("data").EnumerateArray()) {
                    string? url = Text(grid, "url");
                    if (string.IsNullOrEmpty(url)) {
                        continue;
                    }

                    string mime = Text(grid, "mime") ?? string.Empty;
                    string extension = mime == "image/jpeg" ? ".jpg" : Path.GetExtension(new Uri(url).AbsolutePath) is { Length: > 0 } ext ? ext.ToLowerInvariant() : ".png";
                    string author = grid.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object ? Text(a, "name") ?? string.Empty : string.Empty;

                    covers.Add(new CoverOption(
                        grid.TryGetProperty("id", out var id) && id.TryGetInt64(out long idValue) ? idValue : 0,
                        url,
                        Text(grid, "thumb") ?? url,
                        extension,
                        Number(grid, "width"),
                        Number(grid, "height"),
                        Text(grid, "style") ?? string.Empty,
                        author));
                }

                var pageInfo = new CoverPage(covers, Number(root, "page"), Number(root, "total"), Math.Max(1, Number(root, "limit")));
                return new(covers.Count == 0 ? CoverOutcome.NotFound : CoverOutcome.Found, pageInfo);
            });
        }

        /// <summary> Downloads an image. Image files are public, so the API key is deliberately NOT sent to the image server. </summary>
        public Task<ClientResult<byte[]>> DownloadAsync(string url, CancellationToken ct = default) {
            return GuardAsync<byte[]>(async () => {
                using var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) {
                    return new(CoverOutcome.Unavailable, Detail: $"image download: HTTP {(int)response.StatusCode}");
                }

                return new(CoverOutcome.Found, await response.Content.ReadAsByteArrayAsync(ct));
            });
        }

        // Turns "no internet / timeout / unexpected reply" into a result instead of a crash. Cancelling (closing a window) still cancels.
        private static async Task<ClientResult<T>> GuardAsync<T>(Func<Task<ClientResult<T>>> action) {
            try {
                return await action();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or UriFormatException) {
                if (ex is TaskCanceledException { CancellationToken.IsCancellationRequested: true }) {
                    throw;
                }

                return new(CoverOutcome.Unavailable, Detail: $"{ex.GetType().Name}: {ex.Message}" + (ex.InnerException is null ? "" : $" ({ex.InnerException.Message})"));
            }
        }

        private async Task<(CoverOutcome? Failure, string? Detail, JsonElement Root)> GetJsonAsync(string pathAndQuery, CancellationToken ct) {
            using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + pathAndQuery);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            using var response = await _http.SendAsync(request, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) {
                return (CoverOutcome.InvalidKey, $"HTTP {(int)response.StatusCode}", default);
            }
            if (response.StatusCode == HttpStatusCode.NotFound) {
                return (CoverOutcome.NotFound, null, default);
            }
            if (!response.IsSuccessStatusCode) {
                return (CoverOutcome.Unavailable, $"HTTP {(int)response.StatusCode}", default); // includes 429 (too many requests) and 5xx server errors
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            if (!root.TryGetProperty("success", out var success) || !success.GetBoolean()) {
                return (CoverOutcome.NotFound, null, default);
            }

            return (null, null, root.Clone()); // Clone so it survives after the document is disposed
        }

        private static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static int Number(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out int n) ? n : 0;

        #endregion

        /// <summary> The automatic lookup: finds the game by name, picks the best cover, and downloads it. </summary>
        public async Task<CoverDownload> FindCoverAsync(string gameName, CancellationToken ct = default) {
            if (string.IsNullOrWhiteSpace(gameName)) {
                return new(CoverOutcome.NotFound);
            }

            // 1. Search for the game
            GameSearchResult? game = null;
            GameSearchResult? firstResult = null;
            var exactTitles = NameQueries(gameName).Select(Squash).Where(s => s.Length > 0).ToHashSet();

            foreach (string query in NameQueries(gameName)) {
                var search = await SearchGamesAsync(query, ct);
                if (search.Outcome == CoverOutcome.NotFound) {
                    continue;
                }
                if (search.Outcome != CoverOutcome.Found) {
                    return new(search.Outcome, Detail: search.Detail);
                }

                // The site's search is fuzzy: "Titanfall2" lists "Titanfall" before "Titanfall 2". A result whose
                // title matches ours exactly (ignoring spacing/punctuation) beats whatever the site ranked first.
                game = search.Value!.FirstOrDefault(r => exactTitles.Contains(Squash(r.Name)));
                if (game is not null) {
                    break;
                }

                firstResult ??= search.Value![0];
            }

            game ??= firstResult;
            if (game is null) {
                return new(CoverOutcome.NotFound);
            }

            // 2. Pick a cover for that game
            var covers = await GetCoversAsync(game.Id, 0, ct);
            if (covers.Outcome != CoverOutcome.Found) {
                return new(covers.Outcome, Detail: covers.Detail);
            }

            // The site's normal "alternate" style is the standard cover; odd styles (logo-only etc.) are a fallback. Site ranking is kept otherwise.
            var chosen = covers.Value!.Covers.FirstOrDefault(c => c.Style == "alternate") ?? covers.Value.Covers[0];

            // 3. Download it
            var image = await DownloadAsync(chosen.Url, ct);
            return image.Outcome == CoverOutcome.Found
                ? new(CoverOutcome.Found, image.Value, chosen.Extension, game.Name)
                : new(image.Outcome, Detail: image.Detail);
        }

        #region Name handling
        /// <summary> The names worth searching for, best first: the cleaned-up name, then the name as-is. </summary>
        public static IReadOnlyList<string> NameQueries(string name) {
            var queries = new List<string>();
            string cleaned = NameCleaner.Clean(name.Replace('/', ' '));
            if (cleaned.Length > 0) {
                queries.Add(cleaned);
            }

            string original = name.Trim().Replace('/', ' ');
            if (original.Length > 0 && !queries.Contains(original, StringComparer.OrdinalIgnoreCase)) {
                queries.Add(original);
            }

            return queries;
        }

        /// <summary> Reduces a title to lowercase letters and digits so "Titanfall 2", "titanfall2" and "Blender (Program)" vs "Blender" compare equal. </summary>
        public static string Squash(string title) {
            string withoutTrailingNote = Regex.Replace(title, @"\s*\([^)]*\)\s*$", string.Empty);
            return new string(withoutTrailingNote.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }
        #endregion
    }
}
