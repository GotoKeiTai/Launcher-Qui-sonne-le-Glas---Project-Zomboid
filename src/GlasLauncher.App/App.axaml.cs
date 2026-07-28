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
            var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();

            var firstRunStore = Services.GetRequiredService<IFirstRunStore>();
            var hasCompletedFirstRun = firstRunStore.HasCompletedFirstRunAsync().GetAwaiter().GetResult();
            if (!hasCompletedFirstRun)
            {
                mainWindowViewModel.ShowFirstRun();
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterServices(ServiceCollection services)
    {
        // Real Windows-specific implementations are registered here in a later
        // plan, guarded by OperatingSystem.IsWindows(). Until then, every
        // platform (including macOS during development) uses the fakes.
        // MainWindowViewModel depends on the concrete FakeSteamEnvironment (not ISteamEnvironment)
        // for its dev-only scenario-switcher toggle. When real Windows services are added here,
        // this registration and MainWindowViewModel's constructor will need to be revisited —
        // either keep FakeSteamEnvironment registered everywhere (toggle becomes a no-op on
        // real builds) or give the switcher its own dev-mode gate.
        services.AddSingleton<FakeSteamEnvironment>();
        services.AddSingleton<ISteamEnvironment>(sp => sp.GetRequiredService<FakeSteamEnvironment>());
        services.AddSingleton<IJavaModService, FakeJavaModService>();
        services.AddSingleton<IUpdateService, FakeUpdateService>();
        services.AddSingleton<IServerInfoService, FakeServerInfoService>();
        services.AddSingleton<IFirstRunStore>(_ => FirstRunStore.CreateDefault());

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<NewsViewModel>();
        services.AddTransient<FirstRunViewModel>();
        services.AddTransient<MainWindowViewModel>();
    }
}
