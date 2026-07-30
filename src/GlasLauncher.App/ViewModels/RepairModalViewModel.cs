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
        RepairStepNames.OldVersionRemoved,
        RepairStepNames.DownloadingJavaMod,
        RepairStepNames.VerifyingIntegrity,
        RepairStepNames.Installing
    };

    private static readonly Dictionary<string, string> StepDescriptions = new()
    {
        [RepairStepNames.OldVersionRemoved] = "Suppression de l'ancienne version du mod Java…",
        [RepairStepNames.DownloadingJavaMod] = "Téléchargement de la dernière version du mod Java depuis le serveur Glas Launcher…",
        [RepairStepNames.VerifyingIntegrity] = "Vérification de l'intégrité du fichier téléchargé…",
        [RepairStepNames.Installing] = "Installation du mod Java…"
    };

    private readonly IJavaModService _javaModService;
    private readonly ILauncherLogger _logger;

    public event Action? Completed;

    public RepairModalViewModel(IJavaModService javaModService, ILauncherLogger logger)
    {
        _javaModService = javaModService;
        _logger = logger;
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

            _logger.Info("Réparation du mod Java terminée avec succès.");
            await Task.Delay(400);
            Completed?.Invoke();
        }
        catch (Exception ex)
        {
            foreach (var step in Steps)
            {
                if (step.State == FirstRunStepState.InProgress)
                {
                    step.State = FirstRunStepState.Pending;
                }
            }

            _logger.Error("Échec de la réparation du mod Java", ex);
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
