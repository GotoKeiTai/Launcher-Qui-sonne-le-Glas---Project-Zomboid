using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlasLauncher.Core.Models;
using GlasLauncher.Core.Services;

namespace GlasLauncher.App.ViewModels;

public partial class UpdateModalViewModel : ViewModelBase
{
    private readonly IUpdateService _updateService;

    public event Action? Completed;

    public UpdateModalViewModel(IUpdateService updateService, UpdateInfo updateInfo)
    {
        _updateService = updateService;
        UpdateInfo = updateInfo;
    }

    public UpdateInfo UpdateInfo { get; }

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isStatusSuccess;

    [RelayCommand]
    private void Dismiss() => Completed?.Invoke();

    [RelayCommand]
    private async Task ApplyAsync()
    {
        IsApplying = true;
        StatusMessage = null;

        try
        {
            await _updateService.ApplyUpdateAsync();
            StatusMessage = "Mise à jour installée — redémarrez le launcher pour l'appliquer.";
            IsStatusSuccess = true;
            await Task.Delay(1500);
            Completed?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur lors de la mise à jour : " + ex.Message;
            IsStatusSuccess = false;
        }
        finally
        {
            IsApplying = false;
        }
    }
}
