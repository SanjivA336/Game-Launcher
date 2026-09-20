using Game_Launcher.Models;

namespace Game_Launcher.Services {
    public enum SortKey { Name, RecentlyPlayed, MostPlayed, RecentlyAdded }

    /// <summary> One entry in the sort dropdown. </summary>
    /// <param name="DefaultDescending"> Which direction feels natural when this sort is first picked (newest first, most first, A to Z).</param>
    public record SortOption(SortKey Key, string Label, bool DefaultDescending, string AscendingLabel, string DescendingLabel) {
        public string DirectionLabel(bool descending) => descending ? DescendingLabel : AscendingLabel;

        // Screen readers (and UI tests) read a dropdown item's name from ToString(); a record's default would be a long debug string
        public override string ToString() => Label;
    }

    /// <summary> Everything that decides which games the library shows and in what order: search, tag filters, sort. </summary>
    /// <param name="MatchAll"> True = a game needs ALL selected tags (AND). False = ANY selected tag is enough (OR).</param>
    public record LibraryQuery(string? Search, IReadOnlyCollection<string> Tags, bool MatchAll, SortKey Sort, bool Descending) {

        public static readonly IReadOnlyList<SortOption> SortOptions = [
            new(SortKey.Name, "Name", false, "A to Z", "Z to A"),
            new(SortKey.RecentlyPlayed, "Recently played", true, "Oldest first", "Newest first"),
            new(SortKey.MostPlayed, "Most played", true, "Least first", "Most first"),
            new(SortKey.RecentlyAdded, "Recently added", true, "Oldest first", "Newest first"),
        ];

        /// <summary> Filters and sorts the given games according to this query. </summary>
        public List<GameMapping> Apply(IEnumerable<GameMapping> games) {
            // Hidden games only appear while the "Hidden" tag filter is switched on
            bool showHidden = Tags.Contains(TagCatalog.Hidden);
            IEnumerable<GameMapping> result = games.Where(g => showHidden || !g.HasTag(TagCatalog.Hidden));

            if (!string.IsNullOrWhiteSpace(Search)) {
                string term = Search.Trim();
                result = result.Where(g => g.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (Tags.Count > 0) {
                result = MatchAll
                    ? result.Where(g => Tags.All(tag => TagCatalog.Matches(g, tag)))
                    : result.Where(g => Tags.Any(tag => TagCatalog.Matches(g, tag)));
            }

            return SortGames(result.ToList());
        }

        private List<GameMapping> SortGames(List<GameMapping> games) {
            return Sort switch {
                SortKey.RecentlyPlayed => OrderByValue(games, g => g.LastPlayed),
                SortKey.MostPlayed => OrderByValue(games, g => g.LaunchCount > 0 ? g.LaunchCount : (int?)null),
                SortKey.RecentlyAdded => OrderByValue(games, g => g.DateAdded),
                _ => (Descending
                        ? games.OrderByDescending(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                        : games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)).ToList(),
            };
        }

        /// <summary>
        /// Sorts by a value that some games don't have (never played, unknown date). Games WITHOUT a value always go
        /// last, in name order, whichever direction is chosen. Ties among the rest are also broken by name.
        /// </summary>
        private List<GameMapping> OrderByValue<T>(List<GameMapping> games, Func<GameMapping, T?> key) where T : struct, IComparable<T> {
            var withValue = games.Where(g => key(g).HasValue);
            var ordered = Descending
                ? withValue.OrderByDescending(g => key(g)!.Value).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                : withValue.OrderBy(g => key(g)!.Value).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase);

            var without = games.Where(g => !key(g).HasValue).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase);
            return ordered.Concat(without).ToList();
        }
    }
}
