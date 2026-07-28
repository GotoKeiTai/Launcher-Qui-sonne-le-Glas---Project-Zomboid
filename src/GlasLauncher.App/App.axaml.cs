using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GlasLauncher.App.ViewModels;
using GlasLauncher.App.Views;
using GlasLauncher.Core.Services;
using GlasLauncher.Core.Services.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace GlasLauncher.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        RegisterServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterServices(ServiceCollection services)
    {
        // Real Windows-specific implementations are registered here in a later
        // plan, guarded by OperatingSystem.IsWindows(). Until then, every
        // platform (including macOS during development) uses the fakes.
        services.AddSingleton<FakeSteamEnvironment>();
        services.AddSingleton<ISteamEnvironment>(sp => sp.GetRequiredService<FakeSteamEnvironment>());
        services.AddSingleton<IJavaModService, FakeJavaModService>();
        services.AddSingleton<IUpdateService, FakeUpdateService>();
        services.AddSingleton<IServerInfoService, FakeServerInfoService>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<MainWindowViewModel>();
    }
}
