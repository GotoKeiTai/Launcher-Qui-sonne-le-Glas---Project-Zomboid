using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GlasLauncher.App.Converters;

public class DateOnlyToFullFrenchLabelConverter : IValueConverter
{
    public static readonly DateOnlyToFullFrenchLabelConverter Instance = new();

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateOnly date)
        {
            return string.Empty;
        }

        return date.ToString("d MMMM yyyy", French).ToUpperInvariant();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
