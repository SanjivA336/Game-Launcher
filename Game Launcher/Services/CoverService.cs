using Game_Launcher.Models;

namespace Game_Launcher.Services {
    public enum CoverRunStatus { Done, NoApiKey, InvalidKey, Unavailable }

    public record CoverRunResult(CoverRunStatus Status, int Downloaded, int NotFound, string? Detail = null);

    /// <summary> Downloads cover art for games that are waiting for it, and records the result in mappings.json. </summary>
    public static class CoverService {

        /// <summary>
        /// Should this game be looked up now? Only if it is flagged as pending (just added, renamed, or reset).
        /// Nothing else triggers a lookup: not starting Nexus, not a missing file, not a game that simply has no cover.
        /// </summary>
        public static bool NeedsCover(GameMapping game) {
            return game.CoverLookupPending
                && !game.CoverIsCustom
                && !string.IsNullOrWhiteSpace(game.Name)
                && game.Name != GameMapping.UNKNOWN_PLACEHOLDER;
        }

        /// <summary> Creates a client using the API key from Preferences, or null if there is no key yet. </summary>
        public static SteamGridDbClient? CreateClient() {
            string apiKey = Preferences.Load().SteamGridDbApiKey.Trim();
            if (apiKey.Length == 0) {
                return null;
            }

            // The environment variable is a test hook: it lets a local fake server stand in for the real site
            return new SteamGridDbClient(apiKey, Environment.GetEnvironmentVariable("NEXUS_STEAMGRIDDB_URL"));
        }

        /// <summary>
        /// Looks up and downloads covers for every pending game, one at a time (kind to the free API).
        /// Call it from the UI thread: the awaits keep the window responsive, and the callbacks then also run on the UI thread.
        /// </summary>
        /// <param name="onStatus"> Progress text like "Downloading cover art… 3 of 12".</param>
        /// <param name="onCoverChanged"> Called with the saved game after each lookup so the screen can refresh that game.</param>
        public static async Task<CoverRunResult> DownloadMissingCoversAsync(Action<string>? onStatus = null, Action<GameMapping>? onCoverChanged = null, CancellationToken ct = default) {
            // Check for work first, so a launch where nothing is pending doesn't even read the API key or open a connection
            if (!GameMappingManager.LoadMappings().Any(NeedsCover)) {
                return new(CoverRunStatus.Done, 0, 0);
            }

            var client = CreateClient();
            if (client is null) {
                return new(CoverRunStatus.NoApiKey, 0, 0);
            }

            int downloaded = 0, notFound = 0;
            string? lastFailure = null; // the reason a game was skipped in the most recent round (null = nothing was skipped)

            // Each round handles every pending game. Games that were skipped (slow response, hiccup) or renamed while we were busy
            // are still pending, so the next round retries them. Bounded, so it can't loop forever.
            for (int round = 0; round < 3; round++) {
                lastFailure = null;
                var pending = GameMappingManager.LoadMappings().Where(NeedsCover).ToList();
                if (pending.Count == 0) {
                    break;
                }

                int consecutiveFailures = 0;
                for (int i = 0; i < pending.Count; i++) {
                    ct.ThrowIfCancellationRequested();
                    var game = pending[i];
                    string searchedName = game.Name;
                    onStatus?.Invoke($"Downloading cover art… {i + 1} of {pending.Count}");

                    var result = await client.FindCoverAsync(searchedName, ct);
                    GameMapping? saved = null;

                    switch (result.Outcome) {
                        case CoverOutcome.Found or CoverOutcome.NotFound:
                            // Time has passed: the game may have been renamed (its result is stale; it stays pending for the next round)
                            // or given a custom cover by the user (which we must not replace).
                            var current = GameMappingManager.GetMapping(game.DirPathRaw, out _);
                            if (current is null || current.CoverIsCustom || !string.Equals(current.Name, searchedName, StringComparison.OrdinalIgnoreCase)) {
                                break;
                            }

                            if (result.Outcome == CoverOutcome.Found) {
                                // SaveAsync replaces any earlier cover file for this game
                                string fileName = await CoverStore.SaveAsync(current, result.Image!, result.Extension!, custom: false, ct);
                                saved = GameMappingManager.SetCover(current.DirPathRaw, fileName, result.MatchedName);
                                downloaded++;
                            }
                            else {
                                // Nothing found. Remember we asked (clears "pending"), and keep any existing cover.
                                saved = GameMappingManager.SetCover(current.DirPathRaw, null, null);
                                notFound++;
                            }
                            consecutiveFailures = 0;
                            break;

                        case CoverOutcome.InvalidKey:
                            return new(CoverRunStatus.InvalidKey, downloaded, notFound, result.Detail);

                        default:
                            // Unavailable (timeout, no internet, server hiccup): the game stays pending so it is retried, and we move on.
                            // One slow response shouldn't cost every other game its cover, but several in a row means we're offline.
                            lastFailure = result.Detail ?? "unknown error";
                            if (++consecutiveFailures >= 3) {
                                return new(CoverRunStatus.Unavailable, downloaded, notFound, lastFailure);
                            }
                            break;
                    }

                    if (saved is not null) {
                        onCoverChanged?.Invoke(saved);
                    }

                    await Task.Delay(result.Outcome == CoverOutcome.Unavailable ? 600 : 120, ct);
                }
            }

            return lastFailure is null
                ? new(CoverRunStatus.Done, downloaded, notFound)
                : new(CoverRunStatus.Unavailable, downloaded, notFound, lastFailure);
        }
    }
}
