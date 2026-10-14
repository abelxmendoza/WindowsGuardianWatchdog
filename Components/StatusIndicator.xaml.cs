using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WindowsGuardianWatchdog.Components
{
    public sealed partial class StatusIndicator : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusIndicator),
                new PropertyMetadata(string.Empty, OnVisualChanged));

        public static readonly DependencyProperty IndicatorOnProperty =
            DependencyProperty.Register(nameof(IndicatorOn), typeof(bool), typeof(StatusIndicator),
                new PropertyMetadata(false, OnVisualChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IndicatorOn
        {
            get => (bool)GetValue(IndicatorOnProperty);
            set => SetValue(IndicatorOnProperty, value);
        }

        public StatusIndicator()
        {
            InitializeComponent();
            UpdateVisual();
        }

        private static void OnVisualChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is StatusIndicator indicator) indicator.UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (StatusDot == null || Label == null) return;
            Label.Text = Text;
            var color = IndicatorOn
                ? Windows.UI.Color.FromArgb(255, 0, 226, 255)
                : Windows.UI.Color.FromArgb(255, 255, 106, 33);
            StatusDot.Fill = new SolidColorBrush(color);
        }
    }
}
