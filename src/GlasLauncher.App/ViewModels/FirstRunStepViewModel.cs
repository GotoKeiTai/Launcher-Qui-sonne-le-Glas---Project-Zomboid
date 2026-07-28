using CommunityToolkit.Mvvm.ComponentModel;

namespace GlasLauncher.App.ViewModels;

public partial class FirstRunStepViewModel : ViewModelBase
{
    public FirstRunStepViewModel(string name)
    {
        Name = name;
    }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInProgress))]
    private FirstRunStepState _state = FirstRunStepState.Pending;

    [ObservableProperty]
    private int _percentComplete;

    public bool IsInProgress => State == FirstRunStepState.InProgress;
}
