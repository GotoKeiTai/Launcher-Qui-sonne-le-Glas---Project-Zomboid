using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlasLauncher.Core.Services.Fakes;

namespace GlasLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly FakeSteamEnvironment _fakeSteamEnvironment;
    private readonly DashboardViewModel _dashboard;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private ViewModelBase? _currentModal;

    public MainWindowViewModel(DashboardViewModel dashboard, FakeSteamEnvironment fakeSteamEnvironment)
    {
        _dashboard = dashboard;
        _fakeSteamEnvironment = fakeSteamEnvironment;
        _currentPage = dashboard;
    }

    [RelayCommand]
    private async Task ToggleWorkshopScenarioAsync()
    {
        _fakeSteamEnvironment.SimulateWorkshopMissing = !_fakeSteamEnvironment.SimulateWorkshopMissing;
        await _dashboard.RefreshCommand.ExecuteAsync(null);
    }
}
