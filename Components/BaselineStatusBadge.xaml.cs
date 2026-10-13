using Microsoft.UI.Xaml.Controls;

namespace WindowsGuardianWatchdog.Components
{
    public sealed partial class BaselineStatusBadge : UserControl
    {
        public BaselineStatusBadge()
        {
            this.InitializeComponent();
        }

        public string Status
        {
            get => StatusText.Text;
            set => StatusText.Text = value;
        }
    }
}

