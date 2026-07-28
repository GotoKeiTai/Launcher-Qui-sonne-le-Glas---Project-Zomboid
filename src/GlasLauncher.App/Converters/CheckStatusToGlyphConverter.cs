using System;
using System.Globalization;
using Avalonia.Data.Converters;
using GlasLauncher.Core.Models;

namespace GlasLauncher.App.Converters;

public class CheckStatusToGlyphConverter : IValueConverter
{
    public static readonly CheckStatusToGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CheckStatus status)
        {
            return string.Empty;
        }

        return status == CheckStatus.Passed ? "✓" : "!";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
