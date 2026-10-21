using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace KnightLabChess.App.Converters;

/// <summary>
/// Converts a boolean to Visibility (true = Visible, false = Collapsed).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        bool boolValue;
        if (value is bool b)
            boolValue = b;
        else if (value is string str)
            boolValue = !string.IsNullOrEmpty(str);
        else
            boolValue = value is not null;
        if (invert) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts board square properties to the appropriate background brush.
/// </summary>
public class SquareBackgroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 5) return Brushes.Gray;

        bool isLight = values[0] is bool light && light;
        bool isSelected = values[1] is bool sel && sel;
        bool isLastMoveFrom = values[2] is bool lmf && lmf;
        bool isLastMoveTo = values[3] is bool lmt && lmt;
        bool isCheck = values[4] is bool chk && chk;

        if (isCheck)
            return new SolidColorBrush(Color.FromArgb(200, 229, 57, 53));

        if (isSelected)
            return new SolidColorBrush(Color.FromArgb(180, 255, 255, 0));

        if (isLastMoveFrom || isLastMoveTo)
        {
            var baseColor = isLight
                ? Color.FromRgb(232, 213, 181)
                : Color.FromRgb(181, 136, 99);

            // Blend with yellow highlight
            return new SolidColorBrush(Color.FromArgb(255,
                (byte)((baseColor.R * 0.6) + (205 * 0.4)),
                (byte)((baseColor.G * 0.6) + (210 * 0.4)),
                (byte)((baseColor.B * 0.6) + (100 * 0.4))));
        }

        return new SolidColorBrush(isLight
            ? Color.FromRgb(232, 213, 181)
            : Color.FromRgb(181, 136, 99));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a double percentage to a formatted string.
/// </summary>
public class PercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d) return $"{d:F0}%";
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a boolean correct/incorrect to an appropriate color brush.
/// </summary>
public class FeedbackColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isCorrect = value is bool b && b;
        return new SolidColorBrush(isCorrect
            ? Color.FromRgb(76, 175, 80)   // Green
            : Color.FromRgb(229, 57, 53));  // Red
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Resolves a resource key string to a frozen WPF Geometry.
/// </summary>
public class GeometryKeyConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current != null && Application.Current.TryFindResource(key) is Geometry geom)
        {
            return geom;
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Compares value and parameter for string equality and returns true if equal.
/// </summary>
public class EqualityToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null && parameter == null) return true;
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a numeric percentage to a proportional Star GridLength for ColumnDefinitions.
/// </summary>
public class DoubleToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double val = 1.0;
        if (value is double d) val = d;
        else if (value is int i) val = i;
        else if (value != null && double.TryParse(value.ToString(), out var parsed)) val = parsed;

        return new GridLength(Math.Max(val, 0.1), GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Compares two bound values for equality, returning true if they match.
/// Useful for ItemsControl RadioButton IsChecked bindings.
/// </summary>
public class EqualityMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is not null && values[1] is not null)
        {
            return string.Equals(values[0].ToString(), values[1].ToString(), StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

