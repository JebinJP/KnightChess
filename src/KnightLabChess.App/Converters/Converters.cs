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
    private static readonly SolidColorBrush LightSquareBrush = new(Color.FromRgb(240, 217, 181));
    private static readonly SolidColorBrush DarkSquareBrush = new(Color.FromRgb(181, 136, 99));
    private static readonly SolidColorBrush SelectedBrush = new(Color.FromArgb(200, 247, 236, 105));
    private static readonly SolidColorBrush LastMoveLightBrush = new(Color.FromRgb(205, 210, 106));
    private static readonly SolidColorBrush LastMoveDarkBrush = new(Color.FromRgb(170, 162, 58));
    private static readonly SolidColorBrush CheckBrush = new(Color.FromArgb(215, 229, 57, 53));

    static SquareBackgroundConverter()
    {
        LightSquareBrush.Freeze();
        DarkSquareBrush.Freeze();
        SelectedBrush.Freeze();
        LastMoveLightBrush.Freeze();
        LastMoveDarkBrush.Freeze();
        CheckBrush.Freeze();
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 5) return LightSquareBrush;

        bool isLight = values[0] is bool light && light;
        bool isSelected = values[1] is bool sel && sel;
        bool isLastMoveFrom = values[2] is bool lmf && lmf;
        bool isLastMoveTo = values[3] is bool lmt && lmt;
        bool isCheck = values[4] is bool chk && chk;

        if (isCheck)
            return CheckBrush;

        if (isSelected)
            return SelectedBrush;

        if (isLastMoveFrom || isLastMoveTo)
            return isLight ? LastMoveLightBrush : LastMoveDarkBrush;

        return isLight ? LightSquareBrush : DarkSquareBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Resolves a resource key string to a DrawingImage or ImageSource.
/// </summary>
public class DrawingKeyConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current != null)
        {
            var res = Application.Current.TryFindResource(key);
            if (res is DrawingImage drawingImage) return drawingImage;
            if (res is Drawing drawing) return new DrawingImage(drawing);
            if (res is ImageSource imageSource) return imageSource;
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Provides contrasting text brush for rank/file board coordinates.
/// </summary>
public class BoardCoordinateBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush DarkCoordBrush = new(Color.FromRgb(181, 136, 99));
    private static readonly SolidColorBrush LightCoordBrush = new(Color.FromRgb(240, 217, 181));

    static BoardCoordinateBrushConverter()
    {
        DarkCoordBrush.Freeze();
        LightCoordBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isLight = value is bool b && b;
        return isLight ? DarkCoordBrush : LightCoordBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
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

