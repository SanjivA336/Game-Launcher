namespace Game_Launcher.Helpers {
    /// <summary> Turns a date into friendly text like "3 hours ago". </summary>
    public static class RelativeTime {
        public static string Format(DateTime when, DateTime? now = null) {
            TimeSpan age = (now ?? DateTime.Now) - when;

            if (age < TimeSpan.FromMinutes(1)) return "just now";
            if (age < TimeSpan.FromHours(1)) return Plural((int)age.TotalMinutes, "minute");
            if (age < TimeSpan.FromDays(1)) return Plural((int)age.TotalHours, "hour");
            if (age < TimeSpan.FromDays(2)) return "yesterday";
            if (age < TimeSpan.FromDays(30)) return Plural((int)age.TotalDays, "day");
            return when.ToString("MMM d, yyyy");
        }

        private static string Plural(int amount, string unit) => amount == 1 ? $"1 {unit} ago" : $"{amount} {unit}s ago";
    }
}
