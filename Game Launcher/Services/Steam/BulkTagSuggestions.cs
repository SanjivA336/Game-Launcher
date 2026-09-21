using Game_Launcher.Models;

namespace Game_Launcher.Services.Steam {

    /// <summary> Tags added to one game by a bulk run. </summary>
    public record TagChange(string Game, IReadOnlyList<string> Added);

    /// <summary> What a bulk "add suggested tags" run did. </summary>
    /// <param name="Updated"> Games that got at least one new tag.</param>
    /// <param name="AlreadyHad"> Games found on Steam that already had every tag Steam suggested.</param>
    /// <param name="NotFound"> Games Steam doesn't know (or that couldn't be matched safely).</param>
    /// <param name="Failed"> Games that couldn't be checked because Steam couldn't be reached.</param>
    public record BulkTagResult(IReadOnlyList<TagChange> Updated, IReadOnlyList<string> AlreadyHad, IReadOnlyList<string> NotFound, IReadOnlyList<string> Failed);

    /// <summary> Adds Steam's suggested tags to many games, gently, one at a time. It only ever ADDS tags: nothing you set is removed. </summary>
    public static class BulkTagSuggestions {

        /// <summary> Games in a row that must fail before the run gives up (the remaining games are reported as failed): that means offline. </summary>
        public const int GiveUpAfterFailures = 3;

        /// <param name="games"> The games to look up.</param>
        /// <param name="suggest"> Looks one game up (the real one asks Steam; tests pass a fake).</param>
        /// <param name="progress"> Called before each game with (number done, total).</param>
        /// <param name="delay"> Pause between lookups, so Steam isn't hammered.</param>
        public static async Task<BulkTagResult> RunAsync(
            IReadOnlyList<GameMapping> games,
            Func<GameMapping, CancellationToken, Task<TagSuggestion>> suggest,
            Action<int, int>? progress,
            TimeSpan delay,
            CancellationToken ct = default) {

            var updated = new List<TagChange>();
            var already = new List<string>();
            var notFound = new List<string>();
            var failed = new List<string>();
            int consecutiveFailures = 0;

            for (int i = 0; i < games.Count; i++) {
                ct.ThrowIfCancellationRequested();
                var game = games[i];
                progress?.Invoke(i, games.Count);

                var result = await suggest(game, ct);
                switch (result.Outcome) {
                    case SuggestOutcome.Found:
                        consecutiveFailures = 0;
                        var added = GameMappingManager.AddTags(game.DirPathRaw, result.Tags);
                        if (added.Count > 0) updated.Add(new TagChange(game.Name, added));
                        else already.Add(game.Name);
                        break;

                    case SuggestOutcome.NotFound:
                        consecutiveFailures = 0;
                        notFound.Add(game.Name);
                        break;

                    default:
                        failed.Add(game.Name);
                        if (++consecutiveFailures >= GiveUpAfterFailures) {
                            failed.AddRange(games.Skip(i + 1).Select(g => g.Name)); // offline: no point trying the rest
                            progress?.Invoke(games.Count, games.Count);
                            return new BulkTagResult(updated, already, notFound, failed);
                        }
                        break;
                }

                if (i < games.Count - 1 && delay > TimeSpan.Zero) {
                    await Task.Delay(delay, ct);
                }
            }

            progress?.Invoke(games.Count, games.Count);
            return new BulkTagResult(updated, already, notFound, failed);
        }
    }
}
