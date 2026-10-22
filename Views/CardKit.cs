using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WindowsGuardianWatchdog.Views
{
    /// <summary>
    /// Builds the result cards shared by the Notices, Quarantine, Who can connect, App health and Reports pages,
    /// so each page only says what goes on a card.
    /// </summary>
    internal static class CardKit
    {
        public static readonly Brush Critical = Brush("DangerBrush", 255, 255, 106, 33);
        public static readonly Brush Warning = Brush("WarningBrush", 255, 255, 173, 34);
        public static readonly Brush Good = Brush("SuccessBrush", 255, 0, 226, 255);
        public static readonly Brush Muted = Brush("TextSecondaryBrush", 255, 145, 169, 204);

        public static Border Card(string title, Brush accent, string label, params string?[] lines)
        {
            var body = new StackPanel { Spacing = 6 };
            var heading = new Grid { ColumnSpacing = 12 };
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 15, TextWrapping = TextWrapping.Wrap });
            var chip = new TextBlock { Text = label, Foreground = accent, FontWeight = FontWeights.SemiBold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(chip, 1);
            heading.Children.Add(chip);
            body.Children.Add(heading);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                body.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap, Foreground = Muted, IsTextSelectionEnabled = true });
            }

            var card = new Border
            {
                Child = body,
                BorderBrush = accent,
                BorderThickness = new Thickness(3, 1, 1, 1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18),
                Background = Brush("SurfaceBrush", 255, 7, 17, 31)
            };
            return card;
        }

        public static Button AddButton(Border card, string text, RoutedEventHandler click, bool danger = false)
        {
            var body = (StackPanel)card.Child;
            if (body.Children.LastOrDefault() is not StackPanel { Tag: "actions" } actions)
            {
                actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0), Tag = "actions" };
                body.Children.Add(actions);
            }
            var button = new Button { Content = text };
            if (Style(danger ? "DangerButtonStyle" : "SecondaryButtonStyle") is Style style) button.Style = style;
            button.Click += click;
            actions.Children.Add(button);
            return button;
        }

        public static TextBlock Empty(string text) =>
            new() { Text = text, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

        public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string content, string action)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = action,
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = root
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        public static void OpenInWindows(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { Debug.WriteLine($"Could not open {path}: {ex.Message}"); }
        }

        private static Style? Style(string key) =>
            Application.Current.Resources.TryGetValue(key, out var value) ? value as Style : null;

        private static Brush Brush(string key, byte a, byte r, byte g, byte b) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Brush brush
                ? brush
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(a, r, g, b));
    }
}
