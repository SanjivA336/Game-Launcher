using System.Windows;
using System.Windows.Controls;

namespace Game_Launcher.Views {
    public partial class TextEntry : UserControl {
        // Dependency property for the placeholder text
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register("Placeholder", typeof(string), typeof(TextEntry), new PropertyMetadata(string.Empty));

        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        // Dependency property for the input text
        public static readonly DependencyProperty InputProperty =
            DependencyProperty.Register("Input", typeof(string), typeof(TextEntry), new PropertyMetadata(string.Empty));

        public string Input {
            get { return (string)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        // Dependency property for an optional leading icon (a Segoe Fluent Icons character, e.g. "" for a magnifier)
        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(TextEntry), new PropertyMetadata(string.Empty, OnGlyphChanged));

        // Runs whenever Glyph changes: show the icon slot only when there is an icon to show
        private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var entry = (TextEntry)d;
            string glyph = e.NewValue as string ?? string.Empty;
            entry.GlyphIcon.Text = glyph;
            entry.GlyphIcon.Visibility = glyph.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public string Glyph {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public TextEntry() {
            InitializeComponent();
        }

        // Event handler for the clear button
        private void Clear_Click(object sender, RoutedEventArgs e) {
            Input = string.Empty;
            InputBox.Focus();
        }
    }
}
