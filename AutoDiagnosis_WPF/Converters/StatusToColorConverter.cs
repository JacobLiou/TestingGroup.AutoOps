using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MockDiagTool.Models;

namespace MockDiagTool.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CheckStatus status)
        {
            return status switch
            {
                CheckStatus.Pass => new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53)),   // Green
                CheckStatus.Warning => new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26)), // Orange
                CheckStatus.Fail => new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x45)),    // Red
                CheckStatus.Fixed => new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53)),   // Green
                CheckStatus.Scanning => new SolidColorBrush(Color.FromRgb(0x29, 0xB6, 0xF6)),// Blue
                _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),                   // Gray
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CheckStatus status)
        {
            return status switch
            {
                CheckStatus.Pass => new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0xC8, 0x53)),
                CheckStatus.Warning => new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xA7, 0x26)),
                CheckStatus.Fail => new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0x45, 0x45)),
                CheckStatus.Fixed => new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0xC8, 0x53)),
                CheckStatus.Scanning => new SolidColorBrush(Color.FromArgb(0x20, 0x29, 0xB6, 0xF6)),
                _ => new SolidColorBrush(Color.FromArgb(0x10, 0x88, 0x88, 0x88)),
            };
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ScoreToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int score)
        {
            if (score >= 80) return new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53));
            if (score >= 60) return new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26));
            return new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x45));
        }
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ScoreToAngleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int score)
        {
            return score / 100.0 * 360.0;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        return System.Windows.Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToFixVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length > 0 && values[0] is MockDiagTool.Models.CheckStatus status)
        {
            return status is MockDiagTool.Models.CheckStatus.Warning or MockDiagTool.Models.CheckStatus.Fail
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
