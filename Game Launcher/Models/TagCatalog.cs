namespace Game_Launcher.Models {
    /// <summary> One tag a game can have and the library can be filtered by. </summary>
    /// <param name="Name"> The text stored in mappings.json and shown in the UI. Renaming one later orphans existing data, so choose carefully.</param>
    /// <param name="Group"> The heading it appears under in the filter panel and options page.</param>
    /// <param name="Description"> Shown as a tooltip.</param>
    /// <param name="Assignable"> True if the user turns it on per game (the chips on the options page). False for tags Nexus works out itself.</param>
    public record TagDefinition(string Name, string Group, string Description, bool Assignable);

    /// <summary> The full list of tags. Tags describe how a game works, how you play it and where it came from. </summary>
    public static class TagCatalog {
        // Worked out by Nexus (never stored on the game, except Source which is stored at scan time)
        public const string Installed = "Installed";
        public const string NotInstalled = "Not installed";
        public const string NeverPlayed = "Never played";
        public const string RecentlyAdded = "Recently added";
        public const string Hidden = "Hidden";

        // Completion: pick one, and only for games with a story ending (Campaign)
        public const string InProgress = "In Progress";
        public const string Finished = "Finished";

        // Structure
        public const string Campaign = "Campaign";

        // Group names
        public const string CompletionGroup = "Completion";

        /// <summary> A game counts as "recently added" for this many days after it first shows up. </summary>
        public const int RecentlyAddedDays = 14;

        /// <summary> Every launcher a game can come from. Which one a game has is decided when the library is scanned. </summary>
        public static readonly IReadOnlyList<string> Sources = ["Steam", "Epic Games", "GOG", "Ubisoft Connect", "EA app", "Riot Games", "Rockstar Games", "Amazon Games", "Other"];

        public static readonly IReadOnlyList<TagDefinition> All = [
            // Status: worked out by Nexus (Hidden has its own switch on the options page)
            new(Installed, "Status", "The game's files were found on disk", false),
            new(NotInstalled, "Status", "The game's files weren't found (drive unplugged or uninstalled)", false),
            new(NeverPlayed, "Status", "You haven't launched it from Nexus yet", false),
            new(RecentlyAdded, "Status", $"Showed up in the last {RecentlyAddedDays} days", false),
            new(Hidden, "Status", "Hidden games only appear while this filter is on", false),

            // Source: the launcher the game came from, worked out when the library is scanned
            .. Sources.Select(s => new TagDefinition(s, "Source", s == "Other" ? "Not from a launcher Nexus recognises" : $"Installed through {s}", false)),

            // Completion (a game with a story: pick one, or none if you haven't started)
            new(InProgress, CompletionGroup, "You're partway through the story", true),
            new(Finished, CompletionGroup, "You've finished the story", true),

            // Connection
            new("Online", "Connection", "Can be played online", true),
            new("Offline", "Connection", "Can be played without internet (local play counts)", true),

            // Players
            new("Solo", "Players", "Can be played alone", true),
            new("Co-op", "Players", "Cooperative play with other people", true),
            new("Competitive", "Players", "Play against other people", true),
            new("MMO", "Players", "A big shared online world", true),
            new("Party", "Players", "Casual games for groups", true),
            new("Local", "Players", "Same-screen or LAN play", true),

            // Structure
            new(Campaign, "Structure", "Has a story with an ending", true),
            new("Roguelike", "Structure", "Played in repeated runs", true),
            new("Open world", "Structure", "A large world to explore freely", true),
            new("Sandbox", "Structure", "Build and experiment with few rules", true),
            new("Survival", "Structure", "Gather, craft and stay alive", true),

            // Genre
            new("Action", "Genre", "Fast-paced, action-focused", true),
            new("Adventure", "Genre", "Exploration and story", true),
            new("RPG", "Genre", "Role-playing", true),
            new("Strategy", "Genre", "Planning and tactics", true),
            new("Simulation", "Genre", "Simulates a real activity", true),
            new("Racing", "Genre", "Driving and racing", true),
            new("Sports", "Genre", "Sports games", true),
            new("Shooter", "Genre", "Gun-based combat", true),
            new("Puzzle", "Genre", "Solving puzzles", true),
            new("Platformer", "Genre", "Jumping between platforms", true),
            new("Horror", "Genre", "Made to scare", true),

            // Features
            new("Controller", "Features", "Supports gamepads", true),
            new("VR", "Features", "Supports virtual reality", true),
        ];

        /// <summary> Tags the user can switch on per game. </summary>
        public static IEnumerable<TagDefinition> AssignableTags => All.Where(t => t.Assignable);

        /// <summary> Groups where a game has at most one tag (turning one on turns the other off). </summary>
        public static bool IsSingleChoice(string group) => group == CompletionGroup;

        /// <summary> The launcher a game came from ("Other" until the library has been scanned with it recognised). </summary>
        public static string SourceOf(GameMapping game) => string.IsNullOrEmpty(game.Source) ? "Other" : game.Source;

        /// <summary> Does <paramref name="game"/> have <paramref name="tag"/>? Some tags are stored on the game, others are worked out from its state. </summary>
        public static bool Matches(GameMapping game, string tag) => tag switch {
            NotInstalled => !game.HasTag(Installed),
            NeverPlayed => game.LastPlayed is null,
            RecentlyAdded => game.DateAdded is DateTime added && added >= DateTime.Now.AddDays(-RecentlyAddedDays),
            _ when Sources.Contains(tag) => SourceOf(game) == tag,
            _ => game.HasTag(tag),
        };

        /// <summary> The user-assigned tags this game has, in catalog order (used for the chips on game cards). </summary>
        public static IEnumerable<string> AssignedTagsOf(GameMapping game) =>
            AssignableTags.Where(t => game.HasTag(t.Name)).Select(t => t.Name);

        // Tags from earlier versions that were renamed. Applied every time games are loaded (it does nothing once they're converted).
        private static readonly Dictionary<string, string> LegacyNames = new() {
            ["Single-player"] = "Solo",
            ["Multiplayer"] = "Online",
        };

        /// <summary> Converts tags saved by an earlier version to their current names. Unknown tags are left alone. </summary>
        /// <returns> True if the game had anything to convert.</returns>
        public static bool MigrateLegacyTags(GameMapping game) {
            bool changed = false;
            foreach (var (oldName, newName) in LegacyNames) {
                if (game.Tags.Remove(oldName)) {
                    game.Tags.Add(newName);
                    changed = true;
                }
            }
            return changed;
        }
    }
}
