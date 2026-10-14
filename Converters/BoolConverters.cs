using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace WindowsGuardianWatchdog.Converters
{
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var visible = value is bool flag && flag;
            if (parameter is string mode && mode.Equals("invert", StringComparison.OrdinalIgnoreCase)) visible = !visible;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            value is Visibility visibility && visibility == Visibility.Visible;
    }

    public sealed class BoolToStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var enabled = value is bool flag && flag;
            var color = enabled
                ? Windows.UI.Color.FromArgb(255, 0, 226, 255)
                : Windows.UI.Color.FromArgb(255, 255, 106, 33);
            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}
