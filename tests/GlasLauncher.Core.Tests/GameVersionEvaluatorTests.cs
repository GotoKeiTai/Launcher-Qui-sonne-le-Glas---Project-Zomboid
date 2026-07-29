using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class GameVersionEvaluatorTests
{
    private static readonly GameVersionRequirement Requirement =
        new(RequiredBuildId: "18234567", RequiredBranch: "public", DisplayVersion: "41.78.16");

    [Fact]
    public void Evaluate_MatchingBuildIdAndBranch_ReturnsPassed()
    {
        var detected = new GameVersionInfo(BuildId: "18234567", Branch: "public");

        var result = GameVersionEvaluator.Evaluate(detected, Requirement);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Equal("41.78.16", result.Message);
    }

    [Fact]
    public void Evaluate_WrongBranch_ReturnsFailedWithBranchMessage()
    {
        var detected = new GameVersionInfo(BuildId: "18234567", Branch: "unstable");

        var result = GameVersionEvaluator.Evaluate(detected, Requirement);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("branche", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WrongBuildId_ReturnsFailedWithVersionMessage()
    {
        var detected = new GameVersionInfo(BuildId: "17000000", Branch: "public");

        var result = GameVersionEvaluator.Evaluate(detected, Requirement);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.DoesNotContain("17000000", result.Message);
        Assert.DoesNotContain("18234567", result.Message);
        Assert.Contains("41.78.16", result.Message);
    }
}
