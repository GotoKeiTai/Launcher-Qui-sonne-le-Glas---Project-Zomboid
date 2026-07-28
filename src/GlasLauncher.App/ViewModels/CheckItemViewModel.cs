using CommunityToolkit.Mvvm.ComponentModel;
using GlasLauncher.Core.Models;

namespace GlasLauncher.App.ViewModels;

public partial class CheckItemViewModel : ViewModelBase
{
    public CheckItemViewModel(CheckResult result)
    {
        Name = result.Name;
        Status = result.Status;
        Message = result.Message;
        InlineValue = Name == "Version conforme" && Status == CheckStatus.Passed ? Message : null;
        ShowMessageBelow = Status == CheckStatus.Failed;
    }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private CheckStatus _status;

    [ObservableProperty]
    private string _message;

    [ObservableProperty]
    private string? _inlineValue;

    [ObservableProperty]
    private bool _showMessageBelow;
}
