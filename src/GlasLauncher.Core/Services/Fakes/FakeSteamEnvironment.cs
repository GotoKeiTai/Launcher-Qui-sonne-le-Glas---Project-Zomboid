using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeSteamEnvironment : ISteamEnvironment
{
    public bool SimulateWorkshopMissing { get; set; }

    public Task<bool> IsSteamInstalledAsync() => Task.FromResult(true);

    public Task<bool> IsSteamRunningAsync() => Task.FromResult(true);

    public Task<GameVersionInfo?> GetInstalledGameVersionAsync() =>
        Task.FromResult<GameVersionInfo?>(new GameVersionInfo(BuildId: "18234567", Branch: "public"));

    public Task<WorkshopStatus> GetWorkshopStatusAsync(IReadOnlyList<string> requiredIds, string collectionId)
    {
        var installed = SimulateWorkshopMissing
            ? requiredIds.Take(Math.Max(0, requiredIds.Count - 2)).ToList()
            : requiredIds.ToList();

        return Task.FromResult(new WorkshopStatus(installed, requiredIds, collectionId));
    }

    public Task LaunchGameAsync() => Task.CompletedTask;
}
