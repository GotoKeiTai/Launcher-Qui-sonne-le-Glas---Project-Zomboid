using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlasLauncher.Core.Services;
using GlasLauncher.Core.Services.Fakes;

namespace GlasLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly FakeSteamEnvironment _fakeSteamEnvironment;
    private readonly IJavaModService _javaModService;
    private readonly IUpdateService _updateService;
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
        FakeSteamEnvironment fakeSteamEnvironment,
        IJavaModService javaModService,
        IUpdateService updateService)
    {
        _dashboard = dashboard;
        _firstRun = firstRun;
        _fakeSteamEnvironment = fakeSteamEnvironment;
        _javaModService = javaModService;
        _updateService = updateService;
        _currentPage = dashboard;

        dashboard.SettingsRequested += () => CurrentPage = settings;
        dashboard.ChangelogRequested += () =>
        {
            news.ShowChangelogTabCommand.Execute(null);
            CurrentPage = news;
        };
        dashboard.RepairRequested += OnRepairRequested;
        settings.BackRequested += () => CurrentPage = _dashboard;
        news.BackRequested += () => CurrentPage = _dashboard;
        firstRun.Completed += () => CurrentPage = _dashboard;
    }

    public void ShowFirstRun()
    {
        CurrentPage = _firstRun;
        _ = _firstRun.RunSequenceAsync();
    }

    public async Task CheckForUpdatesAsync()
    {
        var updateInfo = await _updateService.CheckForUpdateAsync();
        if (updateInfo is null)
        {
            return;
        }

        var modal = new UpdateModalViewModel(_updateService, updateInfo);
        modal.Completed += () => CurrentModal = null;
        CurrentModal = modal;
    }

    private void OnRepairRequested()
    {
        var modal = new RepairModalViewModel(_javaModService);
        modal.Completed += async () =>
        {
            CurrentModal = null;
            if (_dashboard.RefreshCommand.CanExecute(null))
            {
                await _dashboard.RefreshCommand.ExecuteAsync(null);
            }
        };
        CurrentModal = modal;
        _ = modal.RunRepairAsync();
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
