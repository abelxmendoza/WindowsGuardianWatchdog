using Microsoft.UI.Xaml.Controls;

namespace WindowsGuardianWatchdog.Components
{
    public sealed partial class StatusIndicator : UserControl
    {
        public static readonly Microsoft.UI.Xaml.DependencyProperty TextProperty =
            Microsoft.UI.Xaml.DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(StatusIndicator),
                new Microsoft.UI.Xaml.PropertyMetadata(string.Empty));

        public static readonly Microsoft.UI.Xaml.DependencyProperty IsEnabledProperty =
            Microsoft.UI.Xaml.DependencyProperty.Register(
                nameof(IsEnabled),
                typeof(bool),
                typeof(StatusIndicator),
                new Microsoft.UI.Xaml.PropertyMetadata(false));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsEnabled
        {
            get => (bool)GetValue(IsEnabledProperty);
            set => SetValue(IsEnabledProperty, value);
        }

        public StatusIndicator()
        {
            this.InitializeComponent();
        }
    }
}

