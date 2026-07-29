using System.Diagnostics;
using System.Runtime.Versioning;
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Microsoft.Win32;

namespace GlasLauncher.Core.Services;

public class SteamEnvironment : ISteamEnvironment
{
    private const string AppId = "108600";

    private readonly string? _steamPath;
    private readonly Lazy<SteamGameLocation?> _location;

    public SteamEnvironment(string? steamPath)
    {
        _steamPath = steamPath;
        _location = new Lazy<SteamGameLocation?>(() =>
            _steamPath is null ? null : SteamLibraryLocator.Locate(_steamPath));
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

    public Task<bool> IsSteamRunningAsync() =>
        Task.FromResult(Process.GetProcessesByName("steam").Length > 0);

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
        Process.Start(new ProcessStartInfo($"steam://run/{AppId}") { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
