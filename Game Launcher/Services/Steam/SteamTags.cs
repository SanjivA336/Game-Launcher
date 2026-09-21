using Game_Launcher.Models;

namespace Game_Launcher.Services.Steam {

    /// <summary> What Steam's store says about a game (only the parts Nexus uses). </summary>
    /// <param name="Genres"> Steam's genre names, e.g. "RPG".</param>
    /// <param name="Categories"> Steam's feature names, e.g. "Single-player", "Online Co-op", "Full controller support".</param>
    public record SteamAppInfo(int AppId, string Name, string Type, IReadOnlyList<string> Genres, IReadOnlyList<string> Categories);

    /// <summary> Turns what Steam says about a game into Nexus tags. Pure logic, so it can be tested without any network. </summary>
    public static class SteamTags {

        // Steam's broad genres that have a matching Nexus genre tag. (Steam files shooters under "Action", so those stay manual.)
        private static readonly Dictionary<string, string> GenreTags = new(StringComparer.OrdinalIgnoreCase) {
            ["Action"] = "Action", ["Adventure"] = "Adventure", ["RPG"] = "RPG", ["Strategy"] = "Strategy",
            ["Simulation"] = "Simulation", ["Racing"] = "Racing", ["Sports"] = "Sports",
        };

        /// <summary>
        /// The tags Steam's data supports: Connection, Players, Genre and Features. It cannot know Campaign, Roguelike, Open world,
        /// Party and the like, so those are never suggested. The result is in the order tags appear in the catalog.
        /// </summary>
        public static IReadOnlyList<string> Map(SteamAppInfo info) {
            bool Has(string category) => info.Categories.Any(c => c.Equals(category, StringComparison.OrdinalIgnoreCase));
            bool HasPart(string part) => info.Categories.Any(c => c.Contains(part, StringComparison.OrdinalIgnoreCase));

            bool single = Has("Single-player");
            bool coop = HasPart("Co-op");
            bool pvp = HasPart("PvP");
            bool mmo = Has("MMO") || info.Genres.Any(g => g.Equals("Massively Multiplayer", StringComparison.OrdinalIgnoreCase));
            bool local = HasPart("Shared/Split Screen") || HasPart("LAN") || HasPart("Local");
            bool multiplayer = Has("Multi-player") || coop || pvp || mmo;

            // Online: any online feature, or "multiplayer" with nothing said about it being local-only or playable alone
            bool online = Has("Online Co-op") || Has("Online PvP") || mmo || Has("Cross-Platform Multiplayer") || HasPart("Online")
                          || (multiplayer && !local && !single);
            bool offline = single || local;

            var tags = new HashSet<string>();
            if (online) tags.Add("Online");
            if (offline) tags.Add("Offline");
            if (single) tags.Add("Solo");
            if (coop) tags.Add("Co-op");
            if (pvp) tags.Add("Competitive");
            if (mmo) tags.Add("MMO");
            if (local) tags.Add("Local");

            // "Tracked Controller Support" is a VR feature, not a gamepad one
            if (info.Categories.Any(c => c.Contains("controller support", StringComparison.OrdinalIgnoreCase) && !c.Contains("Tracked", StringComparison.OrdinalIgnoreCase))) tags.Add("Controller");
            if (HasPart("VR")) tags.Add("VR");

            foreach (string genre in info.Genres) {
                if (GenreTags.TryGetValue(genre, out string? tag)) tags.Add(tag);
            }

            return TagCatalog.All.Where(t => t.Assignable && tags.Contains(t.Name)).Select(t => t.Name).ToList();
        }
    }
}
