using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class WorkshopEvaluatorTests
{
    [Fact]
    public void Evaluate_AllRequiredModsInstalled_ReturnsPassed()
    {
        var status = new WorkshopStatus(
            InstalledIds: new[] { "111", "222", "333" },
            RequiredIds: new[] { "111", "222", "333" },
            CollectionId: "3719763771");

        var result = WorkshopEvaluator.Evaluate(status);

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [Fact]
    public void Evaluate_MissingMods_ReturnsFailedWithCount()
    {
        var status = new WorkshopStatus(
            InstalledIds: new[] { "111" },
            RequiredIds: new[] { "111", "222", "333" },
            CollectionId: "3719763771");

        var result = WorkshopEvaluator.Evaluate(status);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("2", result.Message);
    }

    [Fact]
    public void GetCollectionSubscribeUrl_ReturnsSteamProtocolLink()
    {
        var url = WorkshopEvaluator.GetCollectionSubscribeUrl("3719763771");

        Assert.Equal("steam://url/CommunityFilePage/3719763771", url);
    }
}
