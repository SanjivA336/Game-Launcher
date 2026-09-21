using System.Net.Http;
using Game_Launcher.Models;
using System.Diagnostics;
using System.Net;

namespace Game_Launcher.Services.Deals {

    /// <param name="Deals"> Every deal found, de-duplicated (free ones first, then by discount).</param>
    /// <param name="Failed"> Names of the sources that couldn't be reached or understood.</param>
    /// <param name="FetchedAt"> When the answer was gathered.</param>
    /// <param name="AllFailed"> True when nothing at all could be fetched (most likely: offline).</param>
    public record DealsResult(IReadOnlyList<Deal> Deals, IReadOnlyList<string> Failed, DateTimeOffset FetchedAt, bool AllFailed);

    /// <summary>
    /// Gathers deals from every source at once and remembers the answer for a while (in memory only, gone when Nexus closes).
    /// Nothing is fetched until <see cref="LoadAsync"/> is called, which the Deals page does when it opens.
    /// </summary>
    public class DealsService {

        private static readonly TimeSpan FullCacheTime = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan PartialCacheTime = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(15);

        private readonly IReadOnlyList<IDealProvider> _providers;
        private readonly Func<DateTimeOffset> _now;
        private DealsResult? _cache;
        private DateTimeOffset _cacheExpires;

        /// <summary> The service the app uses. Created on first use, so nothing happens at startup. </summary>
        public static DealsService Shared { get; } = new(CreateDefaultProviders());

        public DealsService(IReadOnlyList<IDealProvider> providers, Func<DateTimeOffset>? now = null) {
            _providers = providers;
            _now = now ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary> The shared web client: identifies itself politely and never keeps cookies. </summary>
        public static HttpClient CreateHttpClient() {
            var http = new HttpClient(new HttpClientHandler { UseCookies = false, AutomaticDecompression = DecompressionMethods.All }) {
                Timeout = Timeout.InfiniteTimeSpan, // each request has its own deadline instead
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("NexusLauncher/1.0");
            return http;
        }

        private static IReadOnlyList<IDealProvider> CreateDefaultProviders() {
            var http = CreateHttpClient();
            // Order is also the priority when two sources list the same game: the earlier one is kept
            return [new EpicDeals(http), new SteamDeals(http), new GamerPowerDeals(http), new CheapSharkDeals(http)];
        }

        /// <summary> Deals from every source. Uses the remembered answer while it's fresh, unless <paramref name="force"/> is set. </summary>
        public async Task<DealsResult> LoadAsync(bool force, CancellationToken ct = default) {
            if (!force && _cache is not null && _now() < _cacheExpires) {
                return _cache;
            }

            var attempts = await Task.WhenAll(_providers.Select(p => FetchOneAsync(p, ct)));

            var failed = attempts.Where(a => a.Deals is null).Select(a => a.Provider.Name).ToList();
            var deals = Merge(attempts.Where(a => a.Deals is not null).SelectMany(a => a.Deals!));
            var result = new DealsResult(deals, failed, _now(), failed.Count == _providers.Count);

            // A completely failed attempt isn't remembered (opening the page again tries again); a partial one only briefly
            if (!result.AllFailed) {
                _cache = result;
                _cacheExpires = _now() + (failed.Count == 0 ? FullCacheTime : PartialCacheTime);
            }
            return result;
        }

        private static async Task<(IDealProvider Provider, IReadOnlyList<Deal>? Deals)> FetchOneAsync(IDealProvider provider, CancellationToken ct) {
            try {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(ProviderTimeout);
                return (provider, await provider.FetchAsync(timeout.Token));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested) {
                // One source being down (or changing its format) must never take the others with it
                Debug.WriteLine($"Deals: {provider.Name} failed: {ex.Message}");
                return (provider, null);
            }
        }

        /// <summary>
        /// Removes repeats of the same game AT THE SAME STORE (the same free game listed by Epic, GamerPower and CheapShark), keeping the source
        /// that came first. The same game at different stores is kept, so stores can be compared. Ordered: free first, then biggest discount.
        /// </summary>
        public static IReadOnlyList<Deal> Merge(IEnumerable<Deal> deals) {
            var kept = new List<Deal>();
            foreach (var deal in deals) {
                if (!kept.Any(k => k.ProviderName == deal.ProviderName && Deal.SameGame(k.Title, deal.Title))) {
                    kept.Add(deal);
                }
            }

            return kept
                .OrderByDescending(d => d.IsFree)
                .ThenByDescending(d => d.DiscountPercent ?? 0)
                .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
