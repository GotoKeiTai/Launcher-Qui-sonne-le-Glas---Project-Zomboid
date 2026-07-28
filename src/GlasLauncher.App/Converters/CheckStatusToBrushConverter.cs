using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GlasLauncher.Core.Models;

namespace GlasLauncher.App.Converters;

/// <summary>
/// Maps a <see cref="CheckStatus"/> to a brush. Colors mirror OkBrush/WarnBrush in
/// Styles/Colors.axaml; kept as literal brushes here so this converter has no
/// dependency on the live Application resource tree.
/// </summary>
public class CheckStatusToBrushConverter : IValueConverter
{
    public static readonly CheckStatusToBrushConverter Instance = new();

    private static readonly IBrush OkBrush = new SolidColorBrush(Color.Parse("#5fae7c"));
    private static readonly IBrush WarnBrush = new SolidColorBrush(Color.Parse("#d3a24a"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CheckStatus status)
        {
            return WarnBrush;
        }

        return status == CheckStatus.Passed ? OkBrush : WarnBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
