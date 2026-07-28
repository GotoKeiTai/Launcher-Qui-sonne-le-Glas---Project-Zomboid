using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GlasLauncher.App.ViewModels;

namespace GlasLauncher.App.Converters;

public class RepairStepStateToFontWeightConverter : IValueConverter
{
    public static readonly RepairStepStateToFontWeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is FirstRunStepState.InProgress ? FontWeight.SemiBold : FontWeight.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
