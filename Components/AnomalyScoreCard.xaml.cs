using Microsoft.UI.Xaml.Controls;

namespace WindowsGuardianWatchdog.Components
{
    public sealed partial class AnomalyScoreCard : UserControl
    {
        public AnomalyScoreCard()
        {
            this.InitializeComponent();
        }

        public int Score
        {
            get => int.Parse(ScoreText.Text.Split('/')[0]);
            set => ScoreText.Text = $"{value}/100";
        }
    }
}

