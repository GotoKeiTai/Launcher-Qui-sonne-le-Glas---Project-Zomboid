using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using GlasLauncher.Core.Services;

namespace GlasLauncher.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly ISteamEnvironment _steamEnvironment;
    private readonly IServerInfoService _serverInfoService;
    private readonly IJavaModService _javaModService;
    private readonly IUpdateService _updateService;

    public DashboardViewModel(
        ISteamEnvironment steamEnvironment,
        IServerInfoService serverInfoService,
        IJavaModService javaModService,
        IUpdateService updateService)
    {
        _steamEnvironment = steamEnvironment;
        _serverInfoService = serverInfoService;
        _javaModService = javaModService;
        _updateService = updateService;
        Checks = new ObservableCollection<CheckItemViewModel>();
        News = new ObservableCollection<NewsItem>();
        LauncherVersionText = _updateService.GetCurrentVersion();

        _ = RefreshAsync();
    }

    [ObservableProperty]
    private bool _isServerOnline;

    [ObservableProperty]
    private string _serverStatusText = "Vérification en cours…";

    [ObservableProperty]
    private int _players;

    [ObservableProperty]
    private int _pingMs;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    private bool _canPlay;

    [ObservableProperty]
    private string _statusMessage = "Vérification en cours…";

    [ObservableProperty]
    private string? _workshopSubscribeUrl;

    [ObservableProperty]
    private string _launcherVersionText = "";

    public ObservableCollection<CheckItemViewModel> Checks { get; }

    public ObservableCollection<NewsItem> News { get; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            Checks.Clear();
            News.Clear();
            WorkshopSubscribeUrl = null;

            var server = await _serverInfoService.GetServerInfoAsync();
            IsServerOnline = server.Status == "online";
            ServerStatusText = IsServerOnline ? "Serveur en ligne" : "Serveur hors ligne";
            Players = server.Players;
            PingMs = server.PingMs;

            foreach (var item in await _serverInfoService.GetNewsAsync())
            {
                News.Add(item);
            }

            var steamInstalled = await _steamEnvironment.IsSteamInstalledAsync();
            var steamInstalledResult = steamInstalled
                ? new CheckResult("Steam détecté", CheckStatus.Passed, "Client Steam installé.")
                : new CheckResult("Steam détecté", CheckStatus.Failed, "Client Steam introuvable. Veuillez installer Steam.");
            Checks.Add(new CheckItemViewModel(steamInstalledResult));

            var steamRunning = await _steamEnvironment.IsSteamRunningAsync();
            var steamRunningResult = steamRunning
                ? new CheckResult("Project Zomboid détecté", CheckStatus.Passed, "Steam est en cours d'exécution.")
                : new CheckResult("Project Zomboid détecté", CheckStatus.Failed, "Steam n'est pas lancé. Veuillez démarrer Steam.");
            Checks.Add(new CheckItemViewModel(steamRunningResult));

            var versionRequirement = await _serverInfoService.GetGameVersionRequirementAsync();
            var detectedVersion = await _steamEnvironment.GetInstalledGameVersionAsync();

            var versionResult = detectedVersion is null
                ? new CheckResult("Version conforme", CheckStatus.Failed, "Impossible de détecter Project Zomboid.")
                : GameVersionEvaluator.Evaluate(detectedVersion, versionRequirement);
            Checks.Add(new CheckItemViewModel(versionResult));

            var javaModInfo = await _javaModService.GetStatusAsync();
            Checks.Add(new CheckItemViewModel(JavaModEvaluator.Evaluate(javaModInfo)));

            var workshopStatus = await _steamEnvironment.GetWorkshopStatusAsync(
                requiredIds: new[] { "111", "222", "333" },
                collectionId: "3719763771");
            var workshopResult = WorkshopEvaluator.Evaluate(workshopStatus);
            Checks.Add(new CheckItemViewModel(workshopResult));

            if (workshopResult.Status == CheckStatus.Failed)
            {
                WorkshopSubscribeUrl = WorkshopEvaluator.GetCollectionSubscribeUrl(workshopStatus.CollectionId);
            }

            CanPlay = Checks.All(c => c.Status == CheckStatus.Passed);
            var firstFailedCheck = Checks.FirstOrDefault(c => c.Status == CheckStatus.Failed);
            StatusMessage = CanPlay
                ? "Prêt à jouer — toutes les vérifications sont validées"
                : "Action requise — " + firstFailedCheck!.Message;
        }
        catch (Exception ex)
        {
            CanPlay = false;
            StatusMessage = "Erreur lors de la vérification : " + ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync()
    {
        await _steamEnvironment.LaunchGameAsync();
    }

    [RelayCommand]
    private void OpenWorkshopSubscribe()
    {
        if (WorkshopSubscribeUrl is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(WorkshopSubscribeUrl) { UseShellExecute = true });
    }

    public event Action? SettingsRequested;

    public event Action? ChangelogRequested;

    public event Action? RepairRequested;

    [RelayCommand]
    private void NavigateToSettings() => SettingsRequested?.Invoke();

    [RelayCommand]
    private void NavigateToChangelog() => ChangelogRequested?.Invoke();

    [RelayCommand]
    private void Repair() => RepairRequested?.Invoke();
}
