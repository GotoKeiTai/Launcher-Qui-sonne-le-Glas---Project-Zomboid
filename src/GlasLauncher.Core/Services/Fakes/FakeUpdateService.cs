using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeUpdateService : IUpdateService
{
    // No update available: this Fake is used on non-Windows platforms only, where
    // VelopackUpdateService (the real IUpdateService) doesn't run — purely a dev-UI
    // convenience, not a placeholder awaiting real functionality.
    public Task<UpdateInfo?> CheckForUpdateAsync() => Task.FromResult<UpdateInfo?>(null);

    public async Task ApplyUpdateAsync()
    {
        await Task.Delay(500);
    }

    public string GetCurrentVersion() => "0.1.0-dev";
}
