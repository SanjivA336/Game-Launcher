using System.Net.Http;
using System.Text.Json;

namespace Game_Launcher.Services.Steam {

    public record SteamSearchHit(int Id, string Name);

    /// <summary>
    /// Talks to Steam's public store endpoints: a name search, and the details of one app. No account or key is needed. These
    /// endpoints aren't officially documented, so every failure is reported to the caller instead of being trusted to keep working.
    /// </summary>
    public class SteamStoreClient {
        private readonly HttpClient _http;
        private readonly Uri _baseUri;

        /// <param name="baseUri"> The store's address; a test can point it at a local fake.</param>
        public SteamStoreClient(HttpClient http, Uri? baseUri = null) {
            _http = http;
            _baseUri = baseUri ?? new Uri("https://store.steampowered.com/");
        }

        /// <summary> Games (not DLC, soundtracks...) whose name matches the search text, best match first. Throws if Steam can't be reached. </summary>
        public async Task<IReadOnlyList<SteamSearchHit>> SearchAsync(string term, CancellationToken ct = default) {
            var url = new Uri(_baseUri, $"api/storesearch/?term={Uri.EscapeDataString(term)}&cc=us&l=english");
            string json = await _http.GetStringAsync(url, ct);
            return ParseSearch(json);
        }

        /// <summary> What Steam says about one app, or null when Steam doesn't know that ID. Throws if Steam can't be reached. </summary>
        public async Task<SteamAppInfo?> GetDetailsAsync(int appId, CancellationToken ct = default) {
            var url = new Uri(_baseUri, $"api/appdetails?appids={appId}&l=english");
            string json = await _http.GetStringAsync(url, ct);
            return ParseDetails(json, appId);
        }

        public static IReadOnlyList<SteamSearchHit> ParseSearch(string json) {
            using var doc = JsonDocument.Parse(json);
            var hits = new List<SteamSearchHit>();
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) {
                return hits;
            }

            foreach (var item in items.EnumerateArray()) {
                if (item.TryGetProperty("type", out var type) && type.GetString() is string t && t != "app") continue;
                if (item.TryGetProperty("id", out var id) && id.TryGetInt32(out int appId)
                    && item.TryGetProperty("name", out var name) && name.GetString() is string n) {
                    hits.Add(new SteamSearchHit(appId, n));
                }
            }
            return hits;
        }

        public static SteamAppInfo? ParseDetails(string json, int appId) {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(appId.ToString(), out var entry)
                || !entry.TryGetProperty("success", out var success) || !success.GetBoolean()
                || !entry.TryGetProperty("data", out var data)) {
                return null;
            }

            static List<string> Descriptions(JsonElement data, string property) =>
                data.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array
                    ? list.EnumerateArray().Select(e => e.TryGetProperty("description", out var d) ? d.GetString() : null).OfType<string>().Distinct().ToList()
                    : [];

            return new SteamAppInfo(
                appId,
                data.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                data.TryGetProperty("type", out var type) ? type.GetString() ?? string.Empty : string.Empty,
                Descriptions(data, "genres"),
                Descriptions(data, "categories"));
        }
    }
}
