using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GlasLauncher.App.Converters;

public class BoolToStatusBrushConverter : IValueConverter
{
    public static readonly BoolToStatusBrushConverter Instance = new();

    private static readonly IBrush OkBrush = new SolidColorBrush(Color.Parse("#5fae7c"));
    private static readonly IBrush WarnBrush = new SolidColorBrush(Color.Parse("#d3a24a"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool canPlay)
        {
            return WarnBrush;
        }

        return canPlay ? OkBrush : WarnBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
