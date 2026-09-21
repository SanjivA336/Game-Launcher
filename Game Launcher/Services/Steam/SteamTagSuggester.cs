using Game_Launcher.Helpers;
using Game_Launcher.Models;
using System.Net.Http;
using System.Text.Json;

namespace Game_Launcher.Services.Steam {

    public enum SuggestOutcome { Found, NotFound, Failed }

    /// <summary> The result of looking a game up on Steam. </summary>
    /// <param name="Tags"> The Nexus tags Steam's data supports (may be empty even when the game was found).</param>
    /// <param name="SteamName"> The name Steam has for it, when found.</param>
    /// <param name="AppId"> The Steam app ID, when found.</param>
    /// <param name="Detail"> Why it failed, when it did.</param>
    public record TagSuggestion(SuggestOutcome Outcome, IReadOnlyList<string> Tags, string? SteamName = null, int? AppId = null, string? Detail = null);

    /// <summary>
    /// Works out which Steam game a Nexus game is, and what tags Steam's data suggests for it. Steam games are matched exactly through
    /// Steam's local install records; other games by name, accepting only a close title match (a wrong tag is worse than no tag).
    /// </summary>
    public class SteamTagSuggester {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        private readonly SteamStoreClient _client;
        private readonly Func<string, int?> _localAppId;

        /// <param name="localAppId"> Finds an app ID from the game's folder without any network (replaceable in tests).</param>
        public SteamTagSuggester(SteamStoreClient client, Func<string, int?>? localAppId = null) {
            _client = client;
            _localAppId = localAppId ?? SteamAppIds.FromGameFolder;
        }

        /// <summary> The suggester the app uses: one shared web client, real Steam. </summary>
        public static SteamTagSuggester Shared { get; } = new(new SteamStoreClient(Deals.DealsService.CreateHttpClient()));

        /// <param name="gameName"> The name as shown in Nexus.</param>
        /// <param name="gameFolder"> The game's folder (used to find a Steam app ID locally).</param>
        public async Task<TagSuggestion> SuggestAsync(string gameName, string gameFolder, CancellationToken ct = default) {
            try {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(RequestTimeout);

                var (appId, _) = await ResolveAsync(gameName, gameFolder, timeout.Token);
                if (appId is null) {
                    return new TagSuggestion(SuggestOutcome.NotFound, []);
                }

                var info = await _client.GetDetailsAsync(appId.Value, timeout.Token);
                return info is null
                    ? new TagSuggestion(SuggestOutcome.NotFound, [])
                    : new TagSuggestion(SuggestOutcome.Found, SteamTags.Map(info), info.Name, info.AppId);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or InvalidOperationException
                                       || (ex is OperationCanceledException && !ct.IsCancellationRequested)) {
                return new TagSuggestion(SuggestOutcome.Failed, [], Detail: ex.Message);
            }
        }

        /// <summary> The Steam app ID for a game: exact from the local install record, otherwise a close-title match from a name search. </summary>
        public async Task<(int? AppId, string? SteamName)> ResolveAsync(string gameName, string gameFolder, CancellationToken ct = default) {
            if (_localAppId(gameFolder) is int local) {
                return (local, null);
            }

            // Try the name as written, then cleaned ("Core_Keeper" -> "Core Keeper"), since folder-style names rarely match
            foreach (string query in new[] { gameName, NameCleaner.Clean(gameName) }.Where(q => q.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)) {
                var hits = await _client.SearchAsync(query, ct);
                var match = hits.FirstOrDefault(h => Deal.SameGame(query, h.Name));
                if (match is not null) {
                    return (match.Id, match.Name);
                }
            }
            return (null, null);
        }
    }
}
