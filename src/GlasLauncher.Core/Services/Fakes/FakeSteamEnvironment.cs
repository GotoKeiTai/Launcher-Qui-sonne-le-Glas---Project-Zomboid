using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeSteamEnvironment : ISteamEnvironment
{
    public bool SimulateWorkshopMissing { get; set; }

    public Task<bool> IsSteamInstalledAsync() => Task.FromResult(true);

    public Task<bool> IsSteamRunningAsync() => Task.FromResult(true);

    public Task<GameVersionInfo?> GetInstalledGameVersionAsync() =>
        Task.FromResult<GameVersionInfo?>(new GameVersionInfo(BuildId: "24432948", Branch: "legacy41"));

    public Task<WorkshopStatus> GetWorkshopStatusAsync(IReadOnlyList<string> requiredIds, string collectionId)
    {
        var installed = SimulateWorkshopMissing
            ? requiredIds.Take(Math.Max(0, requiredIds.Count - 2)).ToList()
            : requiredIds.ToList();

        return Task.FromResult(new WorkshopStatus(installed, requiredIds, collectionId));
    }

    public Task LaunchGameAsync() => Task.CompletedTask;

    public Task<string?> GetGameInstallPathAsync() =>
        Task.FromResult<string?>("/fake/steam/library/steamapps/common/ProjectZomboid");

    public Task<bool> IsJavaAgentLaunchOptionConfiguredAsync() => Task.FromResult(true);
}
