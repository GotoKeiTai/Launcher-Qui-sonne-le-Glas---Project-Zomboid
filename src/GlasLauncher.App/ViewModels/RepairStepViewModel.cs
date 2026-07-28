using CommunityToolkit.Mvvm.ComponentModel;

namespace GlasLauncher.App.ViewModels;

public partial class RepairStepViewModel : ViewModelBase
{
    public RepairStepViewModel(string name)
    {
        Name = name;
    }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private FirstRunStepState _state = FirstRunStepState.Pending;
}
