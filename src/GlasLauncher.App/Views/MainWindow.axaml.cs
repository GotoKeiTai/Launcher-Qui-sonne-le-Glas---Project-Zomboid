using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace GlasLauncher.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnResizeGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: string edgeName } || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var edge = edgeName switch
        {
            "North" => WindowEdge.North,
            "NorthEast" => WindowEdge.NorthEast,
            "East" => WindowEdge.East,
            "SouthEast" => WindowEdge.SouthEast,
            "South" => WindowEdge.South,
            "SouthWest" => WindowEdge.SouthWest,
            "West" => WindowEdge.West,
            "NorthWest" => WindowEdge.NorthWest,
            _ => (WindowEdge?)null
        };

        if (edge is not null)
        {
            BeginResizeDrag(edge.Value, e);
        }
    }
}
