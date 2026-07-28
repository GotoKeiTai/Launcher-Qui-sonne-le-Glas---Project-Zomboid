using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GlasLauncher.App.Converters;

/// <summary>
/// Active tab gets a filled panel background; inactive tabs stay transparent.
/// Color mirrors BgPanel2Color in Styles/Colors.axaml.
/// </summary>
public class BoolToTabBackgroundConverter : IValueConverter
{
    public static readonly BoolToTabBackgroundConverter Instance = new();

    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#16301f"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isActive)
        {
            return Brushes.Transparent;
        }

        return isActive ? ActiveBrush : Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
