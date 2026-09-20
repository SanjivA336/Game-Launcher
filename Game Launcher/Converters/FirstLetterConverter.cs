using System.Globalization;
using System.Windows.Data;

namespace Game_Launcher.Converters {
    /// <summary> Turns a game name into its first letter/digit, used as the big initial on placeholder covers. </summary>
    public class FirstLetterConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            string? text = value as string;
            char first = text?.FirstOrDefault(char.IsLetterOrDigit) ?? '?';
            return char.ToUpperInvariant(first).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
