using GlasLauncher.Core.Logic;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class SafeFilePathTests
{
    private const string InstallPath = @"C:\Games\ProjectZomboid";

    [Fact]
    public void TryResolve_PlainFileName_ReturnsTrueWithPathInsideInstallDir()
    {
        var result = SafeFilePath.TryResolve(InstallPath, "GlasVoipMod.jar", out var resolvedPath);

        Assert.True(result);
        Assert.Equal(Path.Combine(InstallPath, "GlasVoipMod.jar"), resolvedPath);
    }

    [Theory]
    [InlineData(@"..\..\..\evil.exe")]
    [InlineData("../../evil.exe")]
    [InlineData(@"..\Startup\evil.exe")]
    public void TryResolve_ParentDirectoryTraversal_ReturnsFalse(string maliciousFileName)
    {
        var result = SafeFilePath.TryResolve(InstallPath, maliciousFileName, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\evil.dll")]
    [InlineData(@"\\attacker-share\evil.exe")]
    public void TryResolve_AbsolutePath_ReturnsFalse(string maliciousFileName)
    {
        var result = SafeFilePath.TryResolve(InstallPath, maliciousFileName, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryResolve_NullOrEmptyFileName_ReturnsFalse()
    {
        Assert.False(SafeFilePath.TryResolve(InstallPath, "", out _));
        Assert.False(SafeFilePath.TryResolve(InstallPath, null!, out _));
    }

    [Fact]
    public void TryResolve_SubdirectoryFileName_ReturnsTrueWhenStillInsideInstallDir()
    {
        var result = SafeFilePath.TryResolve(InstallPath, @"lib\GlasVoipMod.jar", out var resolvedPath);

        Assert.True(result);
        Assert.Equal(Path.Combine(InstallPath, "lib", "GlasVoipMod.jar"), resolvedPath);
    }
}
