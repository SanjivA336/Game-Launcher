using System.Globalization;

namespace Game_Launcher.Models {
    /// <summary> How good a deal is by its discount: under 30% low, under 70% medium, more than that (including free) high. </summary>
    public enum DealGrade { Low, Medium, High }

    /// <summary> How soon a deal ends, for colouring its date. </summary>
    public enum EndsUrgency { Unknown, Later, Soon, Urgent }

    /// <summary> One offer shown on the Deals page: a discounted game or a free one. Prices are US dollars. </summary>
    /// <param name="Title"> The game's name.</param>
    /// <param name="ProviderName"> Where it's on offer (Steam, Epic Games Store, GOG...).</param>
    /// <param name="ProviderHost"> The provider's website host, used to fetch its logo (e.g. "store.steampowered.com").</param>
    /// <param name="Url"> The page to open when the deal is clicked.</param>
    /// <param name="ImageUrl"> A picture of the game, or null. Shown live and never saved.</param>
    /// <param name="OriginalPrice"> The normal price, when known.</param>
    /// <param name="CurrentPrice"> The price now (0 = free).</param>
    /// <param name="DiscountPercent"> How much is taken off (100 = free).</param>
    /// <param name="EndsAt"> When the offer ends, when the source says.</param>
    /// <param name="Note"> Extra detail such as "via GamerPower".</param>
    public record Deal(
        string Title,
        string ProviderName,
        string ProviderHost,
        string Url,
        string? ImageUrl,
        decimal? OriginalPrice,
        decimal? CurrentPrice,
        int? DiscountPercent,
        DateTimeOffset? EndsAt,
        string? Note = null) {

        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        public bool IsFree => CurrentPrice == 0m || DiscountPercent == 100;

        public string CurrentPriceText => IsFree ? "Free" : CurrentPrice is decimal p ? p.ToString("C", Us) : string.Empty;
        public string OriginalPriceText => OriginalPrice is decimal p && p > 0m ? p.ToString("C", Us) : string.Empty;
        public string DiscountText => DiscountPercent is int d && d > 0 ? $"-{d}%" : string.Empty;

        /// <summary> How much money comes off, when both prices are known. </summary>
        public decimal? ReductionDollars => OriginalPrice is decimal o && CurrentPrice is decimal c && o >= c ? o - c : null;

        /// <summary> "Ends 09/24" (month/day, your local time), or "No Date Found" when the source gave none. </summary>
        public string EndsDateText => EndsAt is DateTimeOffset end ? "Ends " + end.ToLocalTime().ToString("MM/dd", Us) : "No Date Found";

        /// <summary> How soon the offer ends, for colouring the date: within a day is urgent, within a week is soon. </summary>
        public EndsUrgency Urgency(DateTimeOffset now) {
            if (EndsAt is not DateTimeOffset end) return EndsUrgency.Unknown;
            TimeSpan left = end - now;
            if (left <= TimeSpan.FromDays(1)) return EndsUrgency.Urgent;   // includes offers that already ended
            if (left <= TimeSpan.FromDays(7)) return EndsUrgency.Soon;
            return EndsUrgency.Later;
        }

        /// <summary> How good the discount is: under 30% is low, under 70% is medium, anything more (including free) is high. </summary>
        public DealGrade Grade => (IsFree ? 100 : DiscountPercent ?? 0) switch {
            < 30 => DealGrade.Low,
            < 70 => DealGrade.Medium,
            _ => DealGrade.High,
        };

        /// <summary> A lowercase letters-and-digits version of a title, used to spot the same game listed by two sources. </summary>
        public static string NormalizeTitle(string title) {
            var chars = title.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant);
            return new string(chars.ToArray());
        }

        // Words that only name a version of the same game. Anything else after the title (a "2", "Reloaded"...) means a different game.
        private static readonly HashSet<string> EditionWords = new(StringComparer.OrdinalIgnoreCase) {
            "edition", "complete", "definitive", "deluxe", "ultimate", "gold", "goty", "game", "year", "of", "the", "remastered",
            "remaster", "standard", "special", "premium", "enhanced", "legendary", "anniversary", "directors", "cut", "digital",
            "collectors", "founders", "bundle",
        };

        private static List<string> Words(string title) =>
            System.Text.RegularExpressions.Regex.Matches(title, @"[\p{L}\p{N}]+").Select(m => m.Value.ToLowerInvariant()).ToList();

        /// <summary>
        /// True when two titles are the same game: equal, or one is the other plus edition words ("Game" vs "Game: Complete Edition").
        /// A sequel is NOT the same game ("Portal" vs "Portal 2").
        /// </summary>
        public static bool SameGame(string a, string b) {
            var x = Words(a);
            var y = Words(b);
            if (x.Count == 0 || y.Count == 0) {
                return false;
            }

            var shorter = x.Count <= y.Count ? x : y;
            var longer = x.Count <= y.Count ? y : x;
            return shorter.SequenceEqual(longer.Take(shorter.Count)) && longer.Skip(shorter.Count).All(EditionWords.Contains);
        }
    }
}
