using Game_Launcher.Models;

namespace Game_Launcher.Services {
    /// <summary> How "Pick something for me" leans when it chooses. </summary>
    public enum PickWeighting {
        /// <summary> Games you launch a lot are more likely. </summary>
        FavorMorePlayed,
        /// <summary> Every candidate is equally likely. </summary>
        None,
        /// <summary> Games you rarely launch (or never have) are more likely. </summary>
        FavorLessPlayed,
    }

    /// <summary> The rules behind "Pick something for me". Pure logic, so it can be tested without any window. </summary>
    public static class GamePicker {

        /// <summary> The games a pick may come from: installed, not hidden, and matching the chosen tags (any of them, or all of them). </summary>
        public static IReadOnlyList<GameMapping> Candidates(IEnumerable<GameMapping> games, IReadOnlyCollection<string> tags, bool matchAll) {
            var pool = games.Where(g => !g.HasTag(TagCatalog.Hidden) && g.HasTag(TagCatalog.Installed));

            if (tags.Count > 0) {
                pool = matchAll
                    ? pool.Where(g => tags.All(tag => TagCatalog.Matches(g, tag)))
                    : pool.Where(g => tags.Any(tag => TagCatalog.Matches(g, tag)));
            }

            return pool.ToList();
        }

        /// <summary>
        /// How likely a game is compared with the others: with <see cref="PickWeighting.FavorMorePlayed"/> it grows with each launch
        /// (1 + launches); with <see cref="PickWeighting.FavorLessPlayed"/> it shrinks with each launch (1 / (1 + launches)), so a
        /// never-played game is the most likely; with <see cref="PickWeighting.None"/> every game counts as 1.
        /// </summary>
        public static double Weight(GameMapping game, PickWeighting weighting) => weighting switch {
            PickWeighting.FavorMorePlayed => 1.0 + game.LaunchCount,
            PickWeighting.FavorLessPlayed => 1.0 / (1.0 + game.LaunchCount),
            _ => 1.0,
        };

        /// <summary> Chooses one game at random, leaning as asked. Returns null when there is nothing to choose from. </summary>
        /// <param name="avoidFolder"> The previous pick: it is left out when there is any other choice, so "Pick again" changes the answer.</param>
        public static GameMapping? Choose(IReadOnlyList<GameMapping> candidates, PickWeighting weighting, Random random, string? avoidFolder = null) {
            var pool = candidates;
            if (avoidFolder is not null && candidates.Count > 1) {
                pool = candidates.Where(g => !g.DirPathRaw.Equals(avoidFolder, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (pool.Count == 0) {
                return null;
            }

            // Weighted choice: lay the games end to end, each as long as its weight, and see where a random point lands
            double total = pool.Sum(g => Weight(g, weighting));
            double point = random.NextDouble() * total;
            foreach (var game in pool) {
                point -= Weight(game, weighting);
                if (point < 0) {
                    return game;
                }
            }
            return pool[^1]; // rounding safety net
        }
    }
}
