using GlasLauncher.Core.Services.Fakes;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class FakeUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNull_NoUpdateAvailable()
    {
        var service = new FakeUpdateService();

        var result = await service.CheckForUpdateAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ApplyUpdateAsync_CompletesWithoutException()
    {
        var service = new FakeUpdateService();

        await service.ApplyUpdateAsync();
    }

    [Fact]
    public void GetCurrentVersion_ReturnsDevPlaceholder()
    {
        var service = new FakeUpdateService();

        var result = service.GetCurrentVersion();

        Assert.Equal("0.1.0-dev", result);
    }
}
