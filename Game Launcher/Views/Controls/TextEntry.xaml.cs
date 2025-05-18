using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace Game_Launcher.Views {
    public partial class TextEntry : UserControl {

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register("Placeholder", typeof(string), typeof(TextEntry), new PropertyMetadata(string.Empty));
        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        private void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var control = (TextEntry)d;
            control.PlaceholderText.Text = (string)e.NewValue;
        }

        public static readonly DependencyProperty InputProperty = DependencyProperty.Register("Input", typeof(string), typeof(TextEntry), new PropertyMetadata(string.Empty));
        public string Input {
            get { return (string)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        private void OnInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var control = (TextEntry)d;
            control.InputBox.Text = (string)e.NewValue;
            control.PlaceholderText.Visibility = string.IsNullOrEmpty((string)e.NewValue) ? Visibility.Visible : Visibility.Collapsed;
        }

        public TextEntry() {
            InitializeComponent();
            InputBox.TextChanged += Input_TextChanged;

            UpdatePlaceholderVisibility();
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e) {
            Input = InputBox.Text;
            UpdatePlaceholderVisibility();
        }

        private void UpdatePlaceholderVisibility() {
            if (string.IsNullOrEmpty(Input)) {
                PlaceholderText.Visibility = Visibility.Visible;
            }
            else {
                PlaceholderText.Visibility = Visibility.Collapsed;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e) {
            InputBox.Clear();
            Input = string.Empty;
            UpdatePlaceholderVisibility();
        }

    }
}
