using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class JavaModEvaluatorTests
{
    [Fact]
    public void Evaluate_NoFilesVerified_ReturnsFailed()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: true,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: Array.Empty<JavaFileStatus>());

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public void Evaluate_LaunchOptionNotConfigured_ReturnsFailed()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: false,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: new[] { new JavaFileStatus("GlasVoipMod.jar", "0.1.0", "0.1.0", IsUpToDate: true) });

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public void Evaluate_FileOutdated_ReturnsFailed()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: true,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: new[] { new JavaFileStatus("GlasVoipMod.jar", null, "0.1.0", IsUpToDate: false) });

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public void Evaluate_LaunchOptionNotConfiguredAndNoFiles_ReturnsFailedWithLaunchOptionMessage()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: false,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: Array.Empty<JavaFileStatus>());

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("-javaagent:GlasVoipMod.jar", result.Message);
    }

    [Fact]
    public void Evaluate_LaunchOptionNotConfiguredWithMultipleRequiredOptions_MessageContainsAllOfThem()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: false,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar", "-agentlib:zbNative --" },
            Files: Array.Empty<JavaFileStatus>());

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Contains("-javaagent:GlasVoipMod.jar", result.Message);
        Assert.Contains("-agentlib:zbNative --", result.Message);
    }

    [Fact]
    public void Evaluate_LaunchOptionConfiguredAndAllFilesUpToDate_ReturnsPassed()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: true,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: new[]
            {
                new JavaFileStatus("ZombieBuddy.jar", "1.0.0", "1.0.0", IsUpToDate: true),
                new JavaFileStatus("GlasVoipMod.jar", "0.1.0", "0.1.0", IsUpToDate: true)
            });

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Passed, result.Status);
    }
}
