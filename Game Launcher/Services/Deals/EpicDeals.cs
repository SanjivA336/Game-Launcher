using System.Net.Http;
using Game_Launcher.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Game_Launcher.Services.Deals {
    /// <summary> The Epic Games Store's current promotions (mostly the weekly free games), from the store's public promotions feed. </summary>
    public class EpicDeals : IDealProvider {
        public const string ProviderName = "Epic Games Store";
        public const string ProviderHost = "store.epicgames.com";

        private readonly HttpClient _http;
        private readonly IReadOnlyList<Uri> _urls;

        public string Name => ProviderName;

        /// <param name="urls"> Addresses to try in order. The normal one sometimes doesn't answer over IPv6, so an IPv4-only twin is the fallback.</param>
        public EpicDeals(HttpClient http, IReadOnlyList<Uri>? urls = null) {
            _http = http;
            _urls = urls ?? [
                new Uri("https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions?locale=en-US&country=US&allowCountries=US"),
                new Uri("https://store-site-backend-static-ipv4.ak.epicgames.com/freeGamesPromotions?locale=en-US&country=US&allowCountries=US"),
            ];
        }

        public async Task<IReadOnlyList<Deal>> FetchAsync(CancellationToken ct) {
            Exception? last = null;
            foreach (var url in _urls) {
                try {
                    // Each try gets its own short deadline so a dead address doesn't use up the whole time allowed
                    using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    attempt.CancelAfter(TimeSpan.FromSeconds(8));
                    string json = await _http.GetStringAsync(url, attempt.Token);
                    return Parse(json, DateTimeOffset.UtcNow);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested) {
                    last = ex;
                }
            }
            throw last ?? new HttpRequestException("Epic didn't answer.");
        }

        public static IReadOnlyList<Deal> Parse(string json, DateTimeOffset now) {
            using var doc = JsonDocument.Parse(json);
            var deals = new List<Deal>();

            if (!TryPath(doc.RootElement, out var elements, "data", "Catalog", "searchStore", "elements")) {
                throw new FormatException("Epic's answer didn't contain its promotions list.");
            }

            foreach (var game in elements.EnumerateArray()) {
                try {
                    string title = game.GetProperty("title").GetString() ?? string.Empty;
                    var offer = CurrentOffer(game, now);
                    if (offer is null || title.Length == 0) {
                        continue; // not on promotion right now (upcoming offers are skipped)
                    }

                    // "discountPercentage" is how much of the price is STILL PAID: 0 means free, 20 means 80% off
                    int paidPercent = offer.Value.PaidPercent;
                    if (paidPercent >= 100) {
                        continue;
                    }

                    decimal? original = null;
                    decimal? listed = null; // the price Epic itself lists (it isn't updated for free games, but is exact for partial discounts)
                    if (TryPath(game, out var total, "price", "totalPrice") && total.TryGetProperty("originalPrice", out var op) && op.TryGetInt64(out long cents)) {
                        decimal unit = (decimal)Math.Pow(10, TryPath(total, out var dec, "currencyInfo", "decimals") && dec.TryGetInt32(out int d) ? d : 2);
                        original = cents / unit;
                        if (total.TryGetProperty("discountPrice", out var dp) && dp.TryGetInt64(out long discountCents)) {
                            listed = discountCents / unit;
                        }
                    }

                    decimal? current = paidPercent == 0 ? 0m
                        : listed is decimal l && original is decimal full && l < full ? l
                        : original is decimal o ? Math.Round(o * paidPercent / 100m, 2)
                        : null;
                    deals.Add(new Deal(title, ProviderName, ProviderHost, StoreUrl(game), Image(game), original, current, 100 - paidPercent, offer.Value.EndsAt));
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or FormatException) {
                    // skip this entry only
                }
            }
            return deals;
        }

        private readonly record struct Offer(int PaidPercent, DateTimeOffset? EndsAt);

        // The promotion that is running at this moment, if any
        private static Offer? CurrentOffer(JsonElement game, DateTimeOffset now) {
            if (!TryPath(game, out var promos, "promotions", "promotionalOffers") || promos.ValueKind != JsonValueKind.Array) {
                return null;
            }

            foreach (var group in promos.EnumerateArray()) {
                if (!group.TryGetProperty("promotionalOffers", out var offers)) {
                    continue;
                }

                foreach (var offer in offers.EnumerateArray()) {
                    if (!offer.TryGetProperty("startDate", out var s) || !offer.TryGetProperty("endDate", out var e)
                        || !DateTimeOffset.TryParse(s.GetString(), out var start) || !DateTimeOffset.TryParse(e.GetString(), out var end)) {
                        continue;
                    }

                    if (start <= now && now < end && TryPath(offer, out var pct, "discountSetting", "discountPercentage") && pct.TryGetInt32(out int paid)) {
                        return new Offer(paid, end);
                    }
                }
            }
            return null;
        }

        private static string StoreUrl(JsonElement game) {
            // The store page name lives in offerMappings (or, for older entries, productSlug / urlSlug). Long hex ids are internal, not page names.
            string? slug = null;
            if (game.TryGetProperty("offerMappings", out var mappings) && mappings.ValueKind == JsonValueKind.Array) {
                slug = mappings.EnumerateArray().Select(m => m.TryGetProperty("pageSlug", out var p) ? p.GetString() : null).FirstOrDefault(s => !string.IsNullOrEmpty(s));
            }
            slug ??= Str(game, "productSlug")?.Split('/')[0];
            slug ??= Str(game, "urlSlug");

            return string.IsNullOrEmpty(slug) || Regex.IsMatch(slug, "^[0-9a-f]{32}$") || !Regex.IsMatch(slug, "^[A-Za-z0-9._-]+$")
                ? "https://store.epicgames.com/en-US/free-games"
                : $"https://store.epicgames.com/en-US/p/{slug}";
        }

        private static string? Image(JsonElement game) {
            if (!game.TryGetProperty("keyImages", out var images) || images.ValueKind != JsonValueKind.Array) {
                return null;
            }

            var list = images.EnumerateArray().Select(i => (Type: Str(i, "type"), Url: Str(i, "url"))).Where(i => i.Url is not null).ToList();
            foreach (string wanted in new[] { "OfferImageWide", "DieselStoreFrontWide", "Thumbnail" }) {
                var hit = list.FirstOrDefault(i => i.Type == wanted);
                if (hit.Url is not null) return hit.Url;
            }
            return list.FirstOrDefault().Url;
        }

        private static string? Str(JsonElement el, string property) =>
            el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static bool TryPath(JsonElement root, out JsonElement found, params string[] path) {
            found = root;
            foreach (string step in path) {
                if (found.ValueKind != JsonValueKind.Object || !found.TryGetProperty(step, out found)) {
                    return false;
                }
            }
            return found.ValueKind != JsonValueKind.Null;
        }
    }
}
