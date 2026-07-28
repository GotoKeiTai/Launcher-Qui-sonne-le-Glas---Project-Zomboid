using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlasLauncher.Core.Models;
using GlasLauncher.Core.Services;

namespace GlasLauncher.App.ViewModels;

public partial class RepairModalViewModel : ViewModelBase
{
    private static readonly string[] StepOrder =
    {
        "Ancienne version supprimée",
        "Téléchargement du mod Java",
        "Vérification de l'intégrité (SHA-256)",
        "Installation"
    };

    private static readonly Dictionary<string, string> StepDescriptions = new()
    {
        ["Ancienne version supprimée"] = "Suppression de l'ancienne version du mod Java…",
        ["Téléchargement du mod Java"] = "Téléchargement de la dernière version du mod Java depuis le serveur Glas Launcher…",
        ["Vérification de l'intégrité (SHA-256)"] = "Vérification de l'intégrité du fichier téléchargé…",
        ["Installation"] = "Installation du mod Java…"
    };

    private readonly IJavaModService _javaModService;

    public event Action? Completed;

    public RepairModalViewModel(IJavaModService javaModService)
    {
        _javaModService = javaModService;
        Steps = new ObservableCollection<RepairStepViewModel>();
        foreach (var name in StepOrder)
        {
            Steps.Add(new RepairStepViewModel(name));
        }
    }

    public ObservableCollection<RepairStepViewModel> Steps { get; }

    [ObservableProperty]
    private int _percentComplete;

    [ObservableProperty]
    private string _subtitle = "Préparation…";

    [ObservableProperty]
    private string? _megabytesLabel;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasError;

    public async Task RunRepairAsync()
    {
        try
        {
            var progress = new Progress<RepairProgress>(OnProgress);
            await _javaModService.RepairAsync(progress);

            foreach (var step in Steps)
            {
                step.State = FirstRunStepState.Done;
            }
            PercentComplete = 100;

            await Task.Delay(400);
            Completed?.Invoke();
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = "Erreur lors de la réparation : " + ex.Message;
        }
    }

    private void OnProgress(RepairProgress progress)
    {
        var stepIndex = Array.IndexOf(StepOrder, progress.StepName);
        if (stepIndex < 0)
        {
            return;
        }

        for (var i = 0; i < Steps.Count; i++)
        {
            Steps[i].State = i < stepIndex
                ? FirstRunStepState.Done
                : i == stepIndex
                    ? FirstRunStepState.InProgress
                    : FirstRunStepState.Pending;
        }

        PercentComplete = progress.PercentComplete;
        Subtitle = StepDescriptions.TryGetValue(progress.StepName, out var description)
            ? description
            : progress.StepName;

        MegabytesLabel = progress is { MegabytesDownloaded: not null, MegabytesTotal: not null }
            ? string.Format(
                CultureInfo.GetCultureInfo("fr-FR"),
                "{0:0.0} Mo / {1:0.0} Mo",
                progress.MegabytesDownloaded,
                progress.MegabytesTotal)
            : null;
    }

    [RelayCommand]
    private void Close() => Completed?.Invoke();
}
