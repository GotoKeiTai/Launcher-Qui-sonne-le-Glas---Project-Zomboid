using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GlasLauncher.App.Converters;

/// <summary>
/// The "Réparer" button is a quiet outline when everything's fine (CanPlay true) and a
/// filled, more prominent panel when a repair is actually relevant (CanPlay false).
/// Color mirrors BgPanel2Color in Styles/Colors.axaml.
/// </summary>
public class BoolToRepairBackgroundConverter : IValueConverter
{
    public static readonly BoolToRepairBackgroundConverter Instance = new();

    private static readonly IBrush FilledBrush = new SolidColorBrush(Color.Parse("#16301f"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool canPlay)
        {
            return FilledBrush;
        }

        return canPlay ? Brushes.Transparent : FilledBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
