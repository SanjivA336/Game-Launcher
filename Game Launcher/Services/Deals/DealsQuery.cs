using Game_Launcher.Models;

namespace Game_Launcher.Services.Deals {

    public enum DealSort { Best, Name, InitialPrice, DiscountedPrice, Expires, ReductionPercent, ReductionDollars }

    public enum PriceFilter { All, FreeOnly, PaidOnly }

    /// <summary> One entry in the sort dropdown. </summary>
    /// <param name="DefaultDescending"> Which direction feels natural when this sort is first picked.</param>
    public record DealSortOption(DealSort Key, string Label, bool DefaultDescending, string AscendingLabel, string DescendingLabel) {
        public string DirectionLabel(bool descending) => descending ? DescendingLabel : AscendingLabel;
        public override string ToString() => Label; // what screen readers and UI tests read for a dropdown item
    }

    /// <summary>
    /// One game on the Deals page. The same game can be on offer in several stores; <see cref="Shown"/> is the offer the card displays
    /// (the best one among the stores the filters allow) and <see cref="Others"/> are the remaining stores it is also on.
    /// </summary>
    public record DealGroup(string Title, Deal Shown, IReadOnlyList<Deal> Others);

    /// <summary> Everything that decides which deals the page shows and in what order: search, store filter, price filter, sort. </summary>
    /// <param name="Providers"> Stores to restrict to (empty = any store).</param>
    /// <param name="MatchAllProviders"> True = the game must be on offer at EVERY selected store (AND). False = ANY selected store is enough (OR).</param>
    /// <param name="HideOwned"> Leave out games that are already in the user's library.</param>
    public record DealsQuery(
        string? Search,
        IReadOnlyCollection<string> Providers,
        bool MatchAllProviders,
        PriceFilter Price,
        bool HideOwned,
        DealSort Sort,
        bool Descending) {

        public static readonly IReadOnlyList<DealSortOption> SortOptions = [
            new(DealSort.Best, "Best deal", true, "Worst first", "Best first"),
            new(DealSort.Name, "Name", false, "A to Z", "Z to A"),
            new(DealSort.InitialPrice, "Initial price", false, "Low to high", "High to low"),
            new(DealSort.DiscountedPrice, "Discounted price", false, "Low to high", "High to low"),
            new(DealSort.Expires, "Expires", false, "Soonest first", "Latest first"),
            new(DealSort.ReductionPercent, "Reduction %", true, "Small to large", "Large to small"),
            new(DealSort.ReductionDollars, "Reduction $", true, "Small to large", "Large to small"),
        ];

        /// <summary> Groups the offers by game, applies the filters and search, and sorts the result. </summary>
        /// <param name="isOwned"> Says whether a game is already in the user's library (by its title).</param>
        public List<DealGroup> Apply(IEnumerable<Deal> deals, Func<string, bool> isOwned) {
            var result = new List<DealGroup>();

            foreach (var offers in GroupByGame(deals)) {
                string title = offers.OrderBy(o => o.Title.Length).First().Title;

                if (HideOwned && isOwned(title)) continue;
                if (!string.IsNullOrWhiteSpace(Search) && !offers.Any(o => o.Title.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase))) continue;

                // Only offers that pass the price filter count; the store filter then works on those
                var passing = offers.Where(PassesOfferFilters).ToList();
                var inScope = Providers.Count == 0 ? passing : passing.Where(o => Providers.Contains(o.ProviderName)).ToList();
                if (inScope.Count == 0) continue;

                // AND: every selected store must have an offer that passes. (OR needs nothing more: inScope is not empty.)
                if (Providers.Count > 0 && MatchAllProviders && !Providers.All(p => passing.Any(o => o.ProviderName == p))) continue;

                var shown = inScope.OrderByDescending(o => o.IsFree).ThenBy(o => o.CurrentPrice ?? decimal.MaxValue).First();
                result.Add(new DealGroup(title, shown, offers.Where(o => o.ProviderName != shown.ProviderName).ToList()));
            }

            return SortGroups(result);
        }

        private bool PassesOfferFilters(Deal deal) {
            if (Price == PriceFilter.FreeOnly && !deal.IsFree) return false;
            if (Price == PriceFilter.PaidOnly && deal.IsFree) return false;
            return true;
        }

        /// <summary> Puts offers for the same game together (see <see cref="Deal.SameGame"/>). </summary>
        public static List<List<Deal>> GroupByGame(IEnumerable<Deal> deals) {
            var groups = new List<List<Deal>>();
            foreach (var deal in deals) {
                var group = groups.FirstOrDefault(g => g.Any(o => Deal.SameGame(o.Title, deal.Title)));
                if (group is null) {
                    groups.Add([deal]);
                }
                else {
                    group.Add(deal);
                }
            }
            return groups;
        }

        private List<DealGroup> SortGroups(List<DealGroup> groups) {
            return Sort switch {
                DealSort.Name => (Descending
                    ? groups.OrderByDescending(g => g.Title, StringComparer.CurrentCultureIgnoreCase)
                    : groups.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase)).ToList(),
                DealSort.InitialPrice => OrderByValue(groups, g => g.Shown.OriginalPrice),
                DealSort.DiscountedPrice => OrderByValue(groups, g => g.Shown.CurrentPrice),
                DealSort.Expires => OrderByValue(groups, g => g.Shown.EndsAt),
                DealSort.ReductionPercent => OrderByValue(groups, g => g.Shown.IsFree ? 100 : g.Shown.DiscountPercent),
                DealSort.ReductionDollars => OrderByValue(groups, g => g.Shown.ReductionDollars),
                _ => BestFirst(groups),
            };
        }

        // "Best deal": free first, then the biggest discount, then name. Descending here means best first (the natural direction).
        private List<DealGroup> BestFirst(List<DealGroup> groups) {
            var ordered = groups
                .OrderByDescending(g => g.Shown.IsFree)
                .ThenByDescending(g => g.Shown.DiscountPercent ?? 0)
                .ThenBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (!Descending) {
                ordered.Reverse();
            }
            return ordered;
        }

        /// <summary>
        /// Sorts by a value that some deals don't have (no expiry date, unknown normal price). Deals WITHOUT a value always go last,
        /// in name order, whichever direction is chosen. Ties are broken by name.
        /// </summary>
        private List<DealGroup> OrderByValue<T>(List<DealGroup> groups, Func<DealGroup, T?> key) where T : struct, IComparable<T> {
            var withValue = groups.Where(g => key(g).HasValue);
            var ordered = Descending
                ? withValue.OrderByDescending(g => key(g)!.Value).ThenBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase)
                : withValue.OrderBy(g => key(g)!.Value).ThenBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase);

            var without = groups.Where(g => !key(g).HasValue).OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase);
            return ordered.Concat(without).ToList();
        }

        /// <summary> The stores that appear in the offers, with how many games each one has, most first. </summary>
        public static List<(string Name, int Games)> ProviderCounts(IEnumerable<Deal> deals) {
            return GroupByGame(deals)
                .SelectMany(offers => offers.Select(o => o.ProviderName).Distinct())
                .GroupBy(name => name)
                .Select(g => (Name: g.Key, Games: g.Count()))
                .OrderByDescending(p => p.Games).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    /// <summary> Splitting a long list into pages. </summary>
    public static class Pager {

        /// <summary> The items on one page (the page number is corrected if it is out of range), and how many pages there are. </summary>
        public static (IReadOnlyList<T> Items, int Page, int TotalPages) Slice<T>(IReadOnlyList<T> all, int page, int pageSize) {
            pageSize = Math.Max(pageSize, 1);
            int totalPages = Math.Max(1, (int)Math.Ceiling(all.Count / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            return (all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, totalPages);
        }

        /// <summary>
        /// The page numbers to show as buttons, with 0 standing for a "..." gap: always the first and last page and the ones around the
        /// current page, e.g. 1 ... 4 5 6 ... 12.
        /// </summary>
        public static IReadOnlyList<int> ButtonNumbers(int current, int totalPages) {
            var wanted = new SortedSet<int> { 1, totalPages, current - 1, current, current + 1 };
            var buttons = new List<int>();
            int previous = 0;
            foreach (int page in wanted.Where(p => p >= 1 && p <= totalPages)) {
                if (previous != 0 && page - previous == 2) {
                    buttons.Add(previous + 1); // a gap of one page is shown as that page, not as "..."
                }
                else if (previous != 0 && page - previous > 2) {
                    buttons.Add(0);
                }
                buttons.Add(page);
                previous = page;
            }
            return buttons;
        }
    }
}
