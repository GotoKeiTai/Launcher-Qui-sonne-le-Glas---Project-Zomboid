using System;
using System.Threading.Tasks;
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
            var hasCompletedFirstRun = Task.Run(() => firstRunStore.HasCompletedFirstRunAsync()).GetAwaiter().GetResult();
            if (!hasCompletedFirstRun)
            {
                mainWindowViewModel.ShowFirstRun();
            }
            else
            {
                _ = mainWindowViewModel.CheckForUpdatesAsync();
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
        // Real Windows-specific implementations land here as each "vrais services
        // Windows" sub-project ships (docs/session-notes.md). Steam & VDF is the
        // first: on Windows, SteamEnvironment reads the registry and parses VDF/ACF
        // files for real. Every other platform (macOS during development) keeps
        // using FakeSteamEnvironment.
        services.AddSingleton<ISteamEnvironment>(_ =>
            OperatingSystem.IsWindows()
                ? SteamEnvironment.CreateForCurrentUser()
                : new FakeSteamEnvironment());
        services.AddSingleton<IJavaModService, FakeJavaModService>();
        services.AddSingleton<IUpdateService, FakeUpdateService>();
        services.AddSingleton<IServerInfoService, FakeServerInfoService>();
        services.AddSingleton<IFirstRunStore>(_ => FirstRunStore.CreateDefault());

        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<NewsViewModel>();
        services.AddSingleton<FirstRunViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
