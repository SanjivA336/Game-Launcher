namespace Game_Launcher.Models {
    /// <summary> One tag a game can have and the library can be filtered by. </summary>
    /// <param name="Name"> The text stored in mappings.json and shown in the UI. Renaming one later orphans existing data, so choose carefully.</param>
    /// <param name="Group"> The heading it appears under in the filter panel and options page.</param>
    /// <param name="Description"> Shown as a tooltip.</param>
    /// <param name="Assignable"> True if the user turns it on per game (the chips on the options page). False for tags Nexus works out itself.</param>
    public record TagDefinition(string Name, string Group, string Description, bool Assignable);

    /// <summary> The full list of tags. Tags describe how a game works (functionality), not its genre. </summary>
    public static class TagCatalog {
        public const string Installed = "Installed";
        public const string NotInstalled = "Not installed";
        public const string NeverPlayed = "Never played";
        public const string Hidden = "Hidden";

        public static readonly IReadOnlyList<TagDefinition> All = [
            // Status: worked out by Nexus (Hidden has its own switch on the options page)
            new(Installed, "Status", "The game's files were found on disk", false),
            new(NotInstalled, "Status", "The game's files weren't found (drive unplugged or uninstalled)", false),
            new(NeverPlayed, "Status", "You haven't launched it from Nexus yet", false),
            new(Hidden, "Status", "Hidden games only appear while this filter is on", false),

            // Players
            new("Single-player", "Players", "Can be played alone", true),
            new("Multiplayer", "Players", "Played with or against other people", true),
            new("Co-op", "Players", "Cooperative play with other people", true),

            // Connection
            new("Online", "Connection", "Needs an internet connection to play", true),
            new("Offline", "Connection", "Works without an internet connection", true),
            new("Local", "Connection", "Same-screen or LAN play", true),

            // Features
            new("Controller", "Features", "Supports gamepads", true),
            new("VR", "Features", "Supports virtual reality", true),
        ];

        /// <summary> Tags the user can switch on per game. </summary>
        public static IEnumerable<TagDefinition> AssignableTags => All.Where(t => t.Assignable);

        /// <summary> Does <paramref name="game"/> have <paramref name="tag"/>? Some tags are stored on the game, others are worked out from its state. </summary>
        public static bool Matches(GameMapping game, string tag) => tag switch {
            NotInstalled => !game.HasTag(Installed),
            NeverPlayed => game.LastPlayed is null,
            _ => game.HasTag(tag),
        };

        /// <summary> The user-assigned tags this game has, in catalog order (used for the chips on game cards). </summary>
        public static IEnumerable<string> AssignedTagsOf(GameMapping game) =>
            AssignableTags.Where(t => game.HasTag(t.Name)).Select(t => t.Name);
    }
}
