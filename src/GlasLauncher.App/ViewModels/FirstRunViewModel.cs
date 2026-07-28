using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GlasLauncher.Core.Services;

namespace GlasLauncher.App.ViewModels;

public partial class FirstRunViewModel : ViewModelBase
{
    private readonly IFirstRunStore _firstRunStore;

    public event Action? Completed;

    public FirstRunViewModel(IFirstRunStore firstRunStore)
    {
        _firstRunStore = firstRunStore;
        Steps = new ObservableCollection<FirstRunStepViewModel>
        {
            new("Steam détecté"),
            new("Project Zomboid détecté"),
            new("Téléchargement du mod Java…"),
            new("Enregistrement de la configuration")
        };
    }

    public ObservableCollection<FirstRunStepViewModel> Steps { get; }

    [ObservableProperty]
    private string? _statusMessage;

    public async Task RunSequenceAsync()
    {
        try
        {
            for (var i = 0; i < Steps.Count; i++)
            {
                var step = Steps[i];
                step.State = FirstRunStepState.InProgress;

                if (i == 2)
                {
                    for (var percent = 0; percent <= 100; percent += 20)
                    {
                        step.PercentComplete = percent;
                        await Task.Delay(150);
                    }
                }
                else
                {
                    await Task.Delay(300);
                }

                step.State = FirstRunStepState.Done;
            }

            await _firstRunStore.MarkFirstRunCompleteAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur lors de la préparation : " + ex.Message;
        }
        finally
        {
            Completed?.Invoke();
        }
    }
}
