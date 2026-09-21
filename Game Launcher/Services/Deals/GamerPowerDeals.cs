using System.Net.Http;
using Game_Launcher.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Game_Launcher.Services.Deals {
    /// <summary>
    /// GamerPower's list of free-game giveaways. It covers the stores that have no feed of their own (GOG, itch.io, IndieGala, Steam,
    /// Ubisoft...), so it is where most "free for a while" games show up.
    /// </summary>
    public class GamerPowerDeals : IDealProvider {
        public const string ProviderName = "GamerPower";
        public const string ProviderHost = "www.gamerpower.com";

        private readonly HttpClient _http;
        private readonly Uri _url;

        public string Name => ProviderName;

        public GamerPowerDeals(HttpClient http, Uri? url = null) {
            _http = http;
            _url = url ?? new Uri("https://www.gamerpower.com/api/giveaways?type=game&platform=pc");
        }

        public async Task<IReadOnlyList<Deal>> FetchAsync(CancellationToken ct) {
            string json = await _http.GetStringAsync(_url, ct);
            return Parse(json);
        }

        public static IReadOnlyList<Deal> Parse(string json) {
            using var doc = JsonDocument.Parse(json);

            // With nothing on offer the site answers with an object like {"status":0,...} instead of a list
            if (doc.RootElement.ValueKind != JsonValueKind.Array) {
                return [];
            }

            var deals = new List<Deal>();
            foreach (var item in doc.RootElement.EnumerateArray()) {
                try {
                    if (Str(item, "status") is string status && !status.Equals("Active", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    if (Str(item, "platforms") is string platforms && !platforms.Contains("PC", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    var (title, store) = CleanTitle(Str(item, "title") ?? string.Empty);
                    if (title.Length == 0) {
                        continue;
                    }

                    // The API's own link goes through gamerpower.com; it's only used when it's an allowed address
                    string? link = new[] { Str(item, "open_giveaway_url"), Str(item, "gamerpower_url") }.FirstOrDefault(DealLinks.IsSafe);
                    if (link is null) {
                        continue;
                    }

                    // The store the game is free on is what matters for filtering, so that is the provider; GamerPower is just where it was listed
                    var provider = store is null ? new DealStores.Store(ProviderName, ProviderHost) : DealStores.FromName(store);
                    deals.Add(new Deal(title, provider.Name, provider.Host, link, Str(item, "image") ?? Str(item, "thumbnail"),
                        ParseWorth(Str(item, "worth")), 0m, 100, ParseEnd(Str(item, "end_date")), "via GamerPower"));
                }
                catch (Exception ex) when (ex is InvalidOperationException or FormatException) {
                    // skip this entry only
                }
            }
            return deals;
        }

        /// <summary> "Sausage Hunter (Indiegala) Giveaway" becomes ("Sausage Hunter", "Indiegala"). </summary>
        public static (string Title, string? Store) CleanTitle(string raw) {
            // The tail is "(Store) Giveaway", sometimes with a word between: "(Steam) Key Giveaway", "(Epic Games) DLC Giveaway"
            var match = Regex.Match(raw.Trim(), @"^(?<title>.*?)\s*\((?<store>[^()]+)\)\s*(?:\w+\s+)*Giveaway\s*$", RegexOptions.IgnoreCase);
            if (match.Success) {
                return (match.Groups["title"].Value.Trim(), match.Groups["store"].Value.Trim());
            }
            return (Regex.Replace(raw.Trim(), @"\s*(?:(?:Key|DLC|Beta|Demo)\s+)?Giveaway\s*$", string.Empty, RegexOptions.IgnoreCase).Trim(), null);
        }

        // "$29.99" -> 29.99; "N/A" -> unknown
        private static decimal? ParseWorth(string? worth) {
            if (worth is null) return null;
            string number = worth.Replace("$", string.Empty).Trim();
            return decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) && value > 0 ? value : null;
        }

        // "2026-09-23 23:59:00" (UTC) -> a moment in time; "N/A" -> no end date given
        private static DateTimeOffset? ParseEnd(string? end) {
            return DateTimeOffset.TryParseExact(end, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
                ? value
                : null;
        }

        private static string? Str(JsonElement el, string property) =>
            el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
