using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GlasLauncher.App.ViewModels;

namespace GlasLauncher.App.Converters;

public class FirstRunStepToBadgeBrushConverter : IValueConverter
{
    public static readonly FirstRunStepToBadgeBrushConverter Instance = new();

    private static readonly IBrush DoneBrush = new SolidColorBrush(Color.Parse("#2c4a37"));
    private static readonly IBrush InProgressBrush = new SolidColorBrush(Color.Parse("#4a3a20"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FirstRunStepState state)
        {
            return Brushes.Transparent;
        }

        return state switch
        {
            FirstRunStepState.Done => DoneBrush,
            FirstRunStepState.InProgress => InProgressBrush,
            _ => Brushes.Transparent
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
