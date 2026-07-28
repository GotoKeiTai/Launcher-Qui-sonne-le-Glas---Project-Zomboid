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
    }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private CheckStatus _status;

    [ObservableProperty]
    private string _message;
}
