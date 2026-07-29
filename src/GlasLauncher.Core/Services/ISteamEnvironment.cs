using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public interface ISteamEnvironment
{
    Task<bool> IsSteamInstalledAsync();
    Task<bool> IsSteamRunningAsync();
    Task<GameVersionInfo?> GetInstalledGameVersionAsync();
    Task<WorkshopStatus> GetWorkshopStatusAsync(IReadOnlyList<string> requiredIds, string collectionId);
    Task LaunchGameAsync();
    Task<string?> GetGameInstallPathAsync();
    Task<bool> IsJavaAgentLaunchOptionConfiguredAsync(IReadOnlyList<string> requiredOptions);
}
