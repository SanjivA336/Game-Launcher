namespace Game_Launcher.Services.Deals {
    /// <summary>
    /// The stores that deals can be for. Every source (Steam's own feed, GamerPower, CheapShark...) spells store names its own way
    /// ("Epic Games", "epic games store", store number 25), so they are all mapped to one name here. That is what lets the
    /// Deals page filter by store and recognise the same offer coming from two sources.
    /// </summary>
    public static class DealStores {

        public record Store(string Name, string Host);

        private static readonly Store Steam = new("Steam", "store.steampowered.com");
        private static readonly Store Epic = new("Epic Games Store", "store.epicgames.com");
        private static readonly Store Gog = new("GOG", "www.gog.com");
        private static readonly Store Ubisoft = new("Ubisoft Store", "store.ubisoft.com");
        private static readonly Store Humble = new("Humble Store", "www.humblebundle.com");
        private static readonly Store Fanatical = new("Fanatical", "www.fanatical.com");
        private static readonly Store IndieGala = new("IndieGala", "www.indiegala.com");
        private static readonly Store Itch = new("itch.io", "itch.io");

        // Lowercase spellings seen in the sources -> the store
        private static readonly Dictionary<string, Store> ByName = new(StringComparer.OrdinalIgnoreCase) {
            ["steam"] = Steam,
            ["epic"] = Epic, ["epic games"] = Epic, ["epic games store"] = Epic, ["egs"] = Epic,
            ["gog"] = Gog, ["gog.com"] = Gog,
            ["ubisoft"] = Ubisoft, ["ubisoft connect"] = Ubisoft, ["ubisoft store"] = Ubisoft, ["uplay"] = Ubisoft,
            ["humble"] = Humble, ["humble bundle"] = Humble, ["humble store"] = Humble,
            ["fanatical"] = Fanatical,
            ["indiegala"] = IndieGala,
            ["itch"] = Itch, ["itch.io"] = Itch, ["itchio"] = Itch,
        };

        // CheapShark numbers its stores
        private static readonly Dictionary<string, Store> ByCheapSharkId = new() {
            ["1"] = Steam, ["7"] = Gog, ["11"] = Humble, ["13"] = Ubisoft, ["15"] = Fanatical, ["25"] = Epic,
        };

        /// <summary> The ids to ask CheapShark for. </summary>
        public static IEnumerable<string> CheapSharkIds => ByCheapSharkId.Keys;

        public static Store? FromCheapSharkId(string id) => ByCheapSharkId.GetValueOrDefault(id);

        /// <summary> Turns a store as a source spells it into the one shared name. A store we don't know keeps its own name (and has no logo host). </summary>
        public static Store FromName(string raw) {
            string name = raw.Trim();
            return ByName.TryGetValue(name, out var known) ? known : new Store(name, string.Empty);
        }
    }
}
