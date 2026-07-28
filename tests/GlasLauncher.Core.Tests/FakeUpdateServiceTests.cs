using GlasLauncher.Core.Services.Fakes;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class FakeUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_ReturnsUpdateInfo_WithChangelogEntries()
    {
        var service = new FakeUpdateService();

        var result = await service.CheckForUpdateAsync();

        Assert.NotNull(result);
        Assert.Equal("v0.1.0", result!.CurrentVersion);
        Assert.Equal("v0.2.0", result.LatestVersion);
        Assert.NotEmpty(result.ChangelogEntries);
    }

    [Fact]
    public async Task ApplyUpdateAsync_CompletesWithoutException()
    {
        var service = new FakeUpdateService();

        await service.ApplyUpdateAsync();
    }
}
