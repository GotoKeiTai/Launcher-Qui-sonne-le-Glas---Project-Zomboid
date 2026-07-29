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
    public void Evaluate_MissingMods_ReturnsFailedWithoutExposingCount()
    {
        var status = new WorkshopStatus(
            InstalledIds: new[] { "111" },
            RequiredIds: new[] { "111", "222", "333" },
            CollectionId: "3719763771");

        var result = WorkshopEvaluator.Evaluate(status);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.DoesNotContain("2", result.Message);
    }

    [Fact]
    public void Evaluate_AllRequiredModsMissing_ReturnsSameFailedMessageAsPartialMismatch()
    {
        var allMissing = WorkshopEvaluator.Evaluate(new WorkshopStatus(
            InstalledIds: Array.Empty<string>(),
            RequiredIds: new[] { "111", "222", "333" },
            CollectionId: "3719763771"));

        var oneMissing = WorkshopEvaluator.Evaluate(new WorkshopStatus(
            InstalledIds: new[] { "111", "222" },
            RequiredIds: new[] { "111", "222", "333" },
            CollectionId: "3719763771"));

        Assert.Equal(CheckStatus.Failed, allMissing.Status);
        Assert.Equal(oneMissing.Message, allMissing.Message);
    }

    [Fact]
    public void GetCollectionSubscribeUrl_ReturnsSteamProtocolLink()
    {
        var url = WorkshopEvaluator.GetCollectionSubscribeUrl("3719763771");

        Assert.Equal("steam://url/CommunityFilePage/3719763771", url);
    }
}
