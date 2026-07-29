using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
    Task ApplyUpdateAsync();
    string GetCurrentVersion();
}
