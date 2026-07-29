using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class JavaFileInspectorTests
{
    // SHA-256("fake glasjavamod content")
    private const string MatchingContent = "fake glasjavamod content";
    private const string MatchingSha256 = "1b5cbd75fa450e543bdce0a9fa501a9ad4ad229d3fb07d16c8e1d6f76a761703";

    [Fact]
    public void GetFileStatuses_FileMatchesHash_ReturnsUpToDate()
    {
        var installPath = CreateTempDir();
        Directory.CreateDirectory(installPath);
        File.WriteAllText(Path.Combine(installPath, "GlasVoipMod.jar"), MatchingContent);

        var manifest = new JavaModManifest(
            new[]
            {
                new JavaFileEntry("GlasVoipMod.jar", "0.1.0", MatchingSha256, "https://example.com/GlasVoipMod.jar")
            },
            Array.Empty<string>());

        var result = JavaFileInspector.GetFileStatuses(installPath, manifest);

        Assert.Single(result);
        Assert.Equal("GlasVoipMod.jar", result[0].FileName);
        Assert.Equal("0.1.0", result[0].InstalledVersion);
        Assert.Equal("0.1.0", result[0].RequiredVersion);
        Assert.True(result[0].IsUpToDate);

        Directory.Delete(installPath, recursive: true);
    }

    [Fact]
    public void GetFileStatuses_FileMissing_ReturnsNotUpToDate()
    {
        var installPath = CreateTempDir();
        Directory.CreateDirectory(installPath);

        var manifest = new JavaModManifest(
            new[]
            {
                new JavaFileEntry("GlasVoipMod.jar", "0.1.0", MatchingSha256, "https://example.com/GlasVoipMod.jar")
            },
            Array.Empty<string>());

        var result = JavaFileInspector.GetFileStatuses(installPath, manifest);

        Assert.Single(result);
        Assert.Null(result[0].InstalledVersion);
        Assert.False(result[0].IsUpToDate);

        Directory.Delete(installPath, recursive: true);
    }

    [Fact]
    public void GetFileStatuses_FileHashMismatch_ReturnsNotUpToDate()
    {
        var installPath = CreateTempDir();
        Directory.CreateDirectory(installPath);
        File.WriteAllText(Path.Combine(installPath, "GlasVoipMod.jar"), "wrong content");

        var manifest = new JavaModManifest(
            new[]
            {
                new JavaFileEntry("GlasVoipMod.jar", "0.1.0", MatchingSha256, "https://example.com/GlasVoipMod.jar")
            },
            Array.Empty<string>());

        var result = JavaFileInspector.GetFileStatuses(installPath, manifest);

        Assert.Single(result);
        Assert.Null(result[0].InstalledVersion);
        Assert.False(result[0].IsUpToDate);

        Directory.Delete(installPath, recursive: true);
    }

    [Fact]
    public void GetFileStatuses_MultipleEntries_ReturnsOneStatusPerEntryInOrder()
    {
        var installPath = CreateTempDir();
        Directory.CreateDirectory(installPath);
        File.WriteAllText(Path.Combine(installPath, "GlasVoipMod.jar"), MatchingContent);
        // ZombieBuddy.jar intentionally left missing.

        var manifest = new JavaModManifest(
            new[]
            {
                new JavaFileEntry("GlasVoipMod.jar", "0.1.0", MatchingSha256, "https://example.com/GlasVoipMod.jar"),
                new JavaFileEntry("ZombieBuddy.jar", "1.0.0", "0000000000000000000000000000000000000000000000000000000000000000", "https://example.com/ZombieBuddy.jar")
            },
            Array.Empty<string>());

        var result = JavaFileInspector.GetFileStatuses(installPath, manifest);

        Assert.Equal(2, result.Count);
        Assert.Equal("GlasVoipMod.jar", result[0].FileName);
        Assert.True(result[0].IsUpToDate);
        Assert.Equal("ZombieBuddy.jar", result[1].FileName);
        Assert.False(result[1].IsUpToDate);

        Directory.Delete(installPath, recursive: true);
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
}
