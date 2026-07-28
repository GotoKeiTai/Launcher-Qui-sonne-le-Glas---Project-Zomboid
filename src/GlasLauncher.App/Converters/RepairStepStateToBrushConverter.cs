using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GlasLauncher.App.ViewModels;

namespace GlasLauncher.App.Converters;

public class RepairStepStateToBrushConverter : IValueConverter
{
    public static readonly RepairStepStateToBrushConverter Instance = new();

    private static readonly IBrush InProgressBrush = new SolidColorBrush(Color.Parse("#c6a35f"));
    private static readonly IBrush OtherBrush = new SolidColorBrush(Color.Parse("#6f7d70"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FirstRunStepState state)
        {
            return OtherBrush;
        }

        return state == FirstRunStepState.InProgress ? InProgressBrush : OtherBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
