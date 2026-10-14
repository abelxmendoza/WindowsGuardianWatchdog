using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WindowsGuardianWatchdog.Components
{
    public sealed partial class PageGuide : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageGuide), new PropertyMetadata(string.Empty, Changed));
        public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(nameof(Summary), typeof(string), typeof(PageGuide), new PropertyMetadata(string.Empty, Changed));
        public static readonly DependencyProperty WhyProperty = DependencyProperty.Register(nameof(Why), typeof(string), typeof(PageGuide), new PropertyMetadata(string.Empty, Changed));
        public static readonly DependencyProperty PriorityProperty = DependencyProperty.Register(nameof(Priority), typeof(string), typeof(PageGuide), new PropertyMetadata("Recommended", Changed));
        public static readonly DependencyProperty RankProperty = DependencyProperty.Register(nameof(Rank), typeof(string), typeof(PageGuide), new PropertyMetadata(string.Empty, Changed));

        public PageGuide()
        {
            InitializeComponent();
            Loaded += (_, _) => Apply();
        }

        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string Summary { get => (string)GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }
        public string Why { get => (string)GetValue(WhyProperty); set => SetValue(WhyProperty, value); }
        public string Priority { get => (string)GetValue(PriorityProperty); set => SetValue(PriorityProperty, value); }
        public string Rank { get => (string)GetValue(RankProperty); set => SetValue(RankProperty, value); }

        private static void Changed(DependencyObject source, DependencyPropertyChangedEventArgs args) => ((PageGuide)source).Apply();

        private void Apply()
        {
            if (TitleText == null) return;
            TitleText.Text = Title;
            SummaryText.Text = Summary;
            WhyText.Text = Why;
            PriorityText.Text = string.IsNullOrWhiteSpace(Priority) ? "Recommended" : Priority;
            var hasRank = !string.IsNullOrWhiteSpace(Rank);
            RankChip.Visibility = hasRank ? Visibility.Visible : Visibility.Collapsed;
            RankText.Text = hasRank ? $"Step {Rank}" : string.Empty;
            PaintPriority(PriorityText.Text);
        }

        private void PaintPriority(string priority)
        {
            var (background, foreground) = priority switch
            {
                "Do this first" => ("#3A2A08", "#FFAD22"),
                "Careful" => ("#3A1A0C", "#FF8A16"),
                "Optional" => ("#102033", "#91A9CC"),
                "Advanced" => ("#10244A", "#47BFFF"),
                _ => ("#06283A", "#00E2FF")
            };
            PriorityChip.Background = Brush(background);
            PriorityText.Foreground = Brush(foreground);
        }

        private static SolidColorBrush Brush(string color) => new(Microsoft.UI.ColorHelper.FromArgb(
            255,
            Convert.ToByte(color.Substring(1, 2), 16),
            Convert.ToByte(color.Substring(3, 2), 16),
            Convert.ToByte(color.Substring(5, 2), 16)));
    }
}
