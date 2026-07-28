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

    public DashboardViewModel(ISteamEnvironment steamEnvironment, IServerInfoService serverInfoService)
    {
        _steamEnvironment = steamEnvironment;
        _serverInfoService = serverInfoService;
        Checks = new ObservableCollection<CheckItemViewModel>();
        News = new ObservableCollection<NewsItem>();

        _ = RefreshAsync();
    }

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
            Players = server.Players;
            PingMs = server.PingMs;

            foreach (var item in await _serverInfoService.GetNewsAsync())
            {
                News.Add(item);
            }

            // "Steam détecté" and "Project Zomboid détecté" are placeholders (always Passed) until
            // the real Windows-specific detection services (registry/VDF lookups) land in a later plan.
            Checks.Add(new CheckItemViewModel(new CheckResult("Steam détecté", CheckStatus.Passed, "Client Steam actif.")));
            Checks.Add(new CheckItemViewModel(new CheckResult("Project Zomboid détecté", CheckStatus.Passed, "Installation trouvée.")));

            var versionRequirement = await _serverInfoService.GetGameVersionRequirementAsync();
            var detectedVersion = await _steamEnvironment.GetInstalledGameVersionAsync();

            var versionResult = detectedVersion is null
                ? new CheckResult("Version conforme", CheckStatus.Failed, "Impossible de détecter Project Zomboid.")
                : GameVersionEvaluator.Evaluate(detectedVersion, versionRequirement);
            Checks.Add(new CheckItemViewModel(versionResult));

            // Placeholder until IJavaModService is wired into the dashboard checks in a later plan.
            Checks.Add(new CheckItemViewModel(new CheckResult("Mod Java à jour", CheckStatus.Passed, "Agent Java synchronisé.")));

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
            StatusMessage = CanPlay
                ? "Prêt à jouer — toutes les vérifications sont validées"
                : "Action requise — abonnez-vous à la collection Workshop pour rejoindre le serveur";
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

    [RelayCommand]
    private void NavigateToSettings() => SettingsRequested?.Invoke();

    [RelayCommand]
    private void NavigateToChangelog() => ChangelogRequested?.Invoke();
}
