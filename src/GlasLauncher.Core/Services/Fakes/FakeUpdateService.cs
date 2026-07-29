using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeUpdateService : IUpdateService
{
    // No update available: kept as a no-update fake until a real Velopack-backed
    // IUpdateService exists (see docs/session-notes.md, sub-project #3).
    public Task<UpdateInfo?> CheckForUpdateAsync() => Task.FromResult<UpdateInfo?>(null);

    public async Task ApplyUpdateAsync()
    {
        await Task.Delay(500);
    }

    public string GetCurrentVersion() => "0.1.0-dev";
}
