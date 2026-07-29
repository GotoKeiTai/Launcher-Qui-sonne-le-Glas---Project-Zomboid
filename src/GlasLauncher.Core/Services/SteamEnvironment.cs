using System.Diagnostics;
using System.Runtime.Versioning;
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Microsoft.Win32;

namespace GlasLauncher.Core.Services;

public class SteamEnvironment : ISteamEnvironment
{
    private const string AppId = "108600";

    // Placeholder — the VOIP mod (GlasVoipMod) has no stable jar name/release yet. Used only
    // as a fallback when the remote manifest doesn't supply RequiredLaunchOptions (i.e. no real
    // hosting exists yet, same situation as JavaModManifestFetcher's placeholder URL). Once the
    // manifest is hosted for real, its RequiredLaunchOptions takes over automatically and this
    // default is never consulted.
    private const string DefaultRequiredLaunchOption = "-javaagent:GlasVoipMod.jar";

    private readonly string? _steamPath;
    private readonly Lazy<SteamGameLocation?> _location;

    public SteamEnvironment(string? steamPath)
    {
        _steamPath = steamPath;
        _location = new Lazy<SteamGameLocation?>(() =>
        {
            try
            {
                return _steamPath is null ? null : SteamLibraryLocator.Locate(_steamPath);
            }
            catch
            {
                return null;
            }
        });
    }

    [SupportedOSPlatform("windows")]
    public static SteamEnvironment CreateForCurrentUser()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var steamPath = key?.GetValue("SteamPath") as string;
        return new SteamEnvironment(steamPath);
    }

    public Task<bool> IsSteamInstalledAsync() =>
        Task.FromResult(_steamPath is not null && Directory.Exists(_steamPath));

    public Task<bool> IsSteamRunningAsync()
    {
        try
        {
            var processes = Process.GetProcessesByName("steam");
            try
            {
                return Task.FromResult(processes.Length > 0);
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<GameVersionInfo?> GetInstalledGameVersionAsync()
    {
        var location = _location.Value;
        return Task.FromResult(location is null ? null : new GameVersionInfo(location.BuildId, location.Branch));
    }

    public Task<WorkshopStatus> GetWorkshopStatusAsync(IReadOnlyList<string> requiredIds, string collectionId)
    {
        var location = _location.Value;
        if (location is null)
        {
            return Task.FromResult(new WorkshopStatus(Array.Empty<string>(), requiredIds, collectionId));
        }

        var installedIds = SteamWorkshopReader.GetInstalledItemIds(location.LibraryPath);
        return Task.FromResult(new WorkshopStatus(installedIds, requiredIds, collectionId));
    }

    public Task LaunchGameAsync()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo($"steam://run/{AppId}") { UseShellExecute = true });
        }
        catch
        {
            // No meaningful way to surface a launch failure through this method's signature;
            // swallow and no-op, consistent with the rest of this class returning graceful negatives.
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetGameInstallPathAsync() =>
        Task.FromResult(_location.Value?.InstallPath);

    public Task<bool> IsJavaAgentLaunchOptionConfiguredAsync(IReadOnlyList<string> requiredOptions)
    {
        var effectiveOptions = requiredOptions.Count > 0 ? requiredOptions : new[] { DefaultRequiredLaunchOption };
        return Task.FromResult(_steamPath is not null
            && SteamLaunchOptionInspector.AreLaunchOptionsConfigured(_steamPath, AppId, effectiveOptions));
    }
}
