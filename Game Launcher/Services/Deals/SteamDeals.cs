using System.Net.Http;
using Game_Launcher.Models;
using System.Text.Json;

namespace Game_Launcher.Services.Deals {
    /// <summary> Steam's current "specials" (discounted games), from the store's public featured-categories list. No account or key needed. </summary>
    public class SteamDeals : IDealProvider {
        public const string ProviderName = "Steam";
        public const string ProviderHost = "store.steampowered.com";

        private readonly HttpClient _http;
        private readonly Uri _url;

        public string Name => ProviderName;

        public SteamDeals(HttpClient http, Uri? url = null) {
            _http = http;
            _url = url ?? new Uri("https://store.steampowered.com/api/featuredcategories?cc=us&l=en");
        }

        public async Task<IReadOnlyList<Deal>> FetchAsync(CancellationToken ct) {
            string json = await _http.GetStringAsync(_url, ct);
            return Parse(json);
        }

        public static IReadOnlyList<Deal> Parse(string json) {
            using var doc = JsonDocument.Parse(json);
            var deals = new List<Deal>();

            if (!doc.RootElement.TryGetProperty("specials", out var specials) || !specials.TryGetProperty("items", out var items)) {
                throw new FormatException("Steam's answer didn't contain its specials list.");
            }

            foreach (var item in items.EnumerateArray()) {
                try {
                    if (!item.TryGetProperty("name", out var nameEl) || !item.TryGetProperty("id", out var idEl)) {
                        continue;
                    }
                    if (item.TryGetProperty("discounted", out var discounted) && discounted.ValueKind == JsonValueKind.False) {
                        continue;
                    }

                    long id = idEl.GetInt64();
                    decimal? original = Cents(item, "original_price");
                    decimal? current = Cents(item, "final_price");
                    int? percent = item.TryGetProperty("discount_percent", out var pct) && pct.TryGetInt32(out int p) ? p : null;

                    DateTimeOffset? ends = item.TryGetProperty("discount_expiration", out var exp) && exp.TryGetInt64(out long unix) && unix > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(unix)
                        : null;

                    string? image = Text(item, "header_image") ?? Text(item, "large_capsule_image");

                    deals.Add(new Deal(nameEl.GetString() ?? "Unknown game", ProviderName, ProviderHost,
                        $"https://store.steampowered.com/app/{id}", image, original, current, percent, ends));
                }
                catch (Exception ex) when (ex is InvalidOperationException or FormatException or KeyNotFoundException) {
                    // One odd entry shouldn't spoil the rest of the list
                }
            }
            return deals;
        }

        private static decimal? Cents(JsonElement item, string property) =>
            item.TryGetProperty(property, out var el) && el.TryGetInt64(out long cents) ? cents / 100m : null;

        private static string? Text(JsonElement item, string property) =>
            item.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }
}
