using System.Net.Http;
using Game_Launcher.Models;
using System.Globalization;
using System.Text.Json;

namespace Game_Launcher.Services.Deals {
    /// <summary> CheapShark's list of the best current discounts across the big PC stores (Steam, GOG, Epic, Humble, Ubisoft, Fanatical). </summary>
    public class CheapSharkDeals : IDealProvider {
        public const string ProviderName = "CheapShark";

        private readonly HttpClient _http;
        private readonly Uri _url;

        public string Name => ProviderName;

        public CheapSharkDeals(HttpClient http, Uri? url = null) {
            _http = http;
            _url = url ?? new Uri("https://www.cheapshark.com/api/1.0/deals?onSale=1&pageSize=30&sortBy=DealRating&storeID=" + string.Join(",", DealStores.CheapSharkIds));
        }

        public async Task<IReadOnlyList<Deal>> FetchAsync(CancellationToken ct) {
            string json = await _http.GetStringAsync(_url, ct);
            return Parse(json);
        }

        public static IReadOnlyList<Deal> Parse(string json) {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) {
                throw new FormatException("CheapShark's answer wasn't a list of deals.");
            }

            var deals = new List<Deal>();
            foreach (var item in doc.RootElement.EnumerateArray()) {
                try {
                    string? title = Str(item, "title");
                    string? dealId = Str(item, "dealID");
                    string storeId = Str(item, "storeID") ?? string.Empty;
                    if (title is null || dealId is null || DealStores.FromCheapSharkId(storeId) is not { } store) {
                        continue;
                    }

                    decimal? sale = Money(item, "salePrice");
                    decimal? normal = Money(item, "normalPrice");
                    int? percent = Money(item, "savings") is decimal s ? (int)Math.Round(s, MidpointRounding.AwayFromZero) : null;

                    // dealID arrives already percent-encoded, so it goes into the link as it is. Clicking goes through CheapShark to the store.
                    deals.Add(new Deal(title, store.Name, store.Host, $"https://www.cheapshark.com/redirect?dealID={dealId}",
                        Str(item, "thumb"), normal, sale, percent, null, "via CheapShark"));
                }
                catch (Exception ex) when (ex is InvalidOperationException or FormatException) {
                    // skip this entry only
                }
            }
            return deals;
        }

        private static decimal? Money(JsonElement item, string property) =>
            Str(item, property) is string text && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) ? value : null;

        private static string? Str(JsonElement el, string property) =>
            el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
