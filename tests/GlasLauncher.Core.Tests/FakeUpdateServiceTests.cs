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
}
