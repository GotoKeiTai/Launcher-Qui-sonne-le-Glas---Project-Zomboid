using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GlasLauncher.Core.Models;

namespace GlasLauncher.App.Converters;

/// <summary>
/// Background for the small circular badge behind a check's status glyph. Passed checks
/// show a plain checkmark with no badge (Transparent); Failed checks get a filled amber
/// circle behind the "!" glyph. Colors mirror WarnDimBrush in Styles/Colors.axaml.
/// </summary>
public class CheckStatusToBadgeBrushConverter : IValueConverter
{
    public static readonly CheckStatusToBadgeBrushConverter Instance = new();

    private static readonly IBrush WarnDimBrush = new SolidColorBrush(Color.Parse("#4a3a20"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CheckStatus status)
        {
            return Brushes.Transparent;
        }

        return status == CheckStatus.Passed ? Brushes.Transparent : WarnDimBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
