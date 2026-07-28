using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlasLauncher.Core.Services.Fakes;

namespace GlasLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly FakeSteamEnvironment _fakeSteamEnvironment;
    private readonly DashboardViewModel _dashboard;
    private readonly FirstRunViewModel _firstRun;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private ViewModelBase? _currentModal;

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        SettingsViewModel settings,
        NewsViewModel news,
        FirstRunViewModel firstRun,
        FakeSteamEnvironment fakeSteamEnvironment)
    {
        _dashboard = dashboard;
        _firstRun = firstRun;
        _fakeSteamEnvironment = fakeSteamEnvironment;
        _currentPage = dashboard;

        dashboard.SettingsRequested += () => CurrentPage = settings;
        dashboard.ChangelogRequested += () =>
        {
            news.ShowChangelogTabCommand.Execute(null);
            CurrentPage = news;
        };
        settings.BackRequested += () => CurrentPage = _dashboard;
        news.BackRequested += () => CurrentPage = _dashboard;
        firstRun.Completed += () => CurrentPage = _dashboard;
    }

    public void ShowFirstRun()
    {
        CurrentPage = _firstRun;
        _ = _firstRun.RunSequenceAsync();
    }

    [RelayCommand]
    private async Task ToggleWorkshopScenarioAsync()
    {
        _fakeSteamEnvironment.SimulateWorkshopMissing = !_fakeSteamEnvironment.SimulateWorkshopMissing;
        if (_dashboard.RefreshCommand.CanExecute(null))
        {
            await _dashboard.RefreshCommand.ExecuteAsync(null);
        }
    }
}
