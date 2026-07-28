using CommunityToolkit.Mvvm.ComponentModel;

namespace GlasLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private ViewModelBase? _currentModal;

    public MainWindowViewModel(DashboardViewModel dashboard)
    {
        _currentPage = dashboard;
    }
}
