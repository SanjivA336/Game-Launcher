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
