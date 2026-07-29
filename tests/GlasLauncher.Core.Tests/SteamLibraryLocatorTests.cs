using System.Text;
using GlasLauncher.Core.Logic;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class SteamLibraryLocatorTests
{
    [Fact]
    public void Locate_SingleLibraryGameFound_ReturnsLocation()
    {
        var steamRoot = CreateTempDir();
        WriteLibraryFolders(steamRoot, steamRoot);
        WriteAppManifest(steamRoot, buildId: "18234567", betaKey: null);

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.NotNull(result);
        Assert.Equal(steamRoot, result!.LibraryPath);
        Assert.Equal(Path.Combine(steamRoot, "steamapps", "common", "ProjectZomboid"), result.InstallPath);
        Assert.Equal("18234567", result.BuildId);
        Assert.Equal("public", result.Branch);

        Directory.Delete(steamRoot, recursive: true);
    }

    [Fact]
    public void Locate_MultipleLibrariesGameInSecondLibrary_ReturnsLocation()
    {
        var steamRoot = CreateTempDir();
        var secondLibrary = CreateTempDir();
        WriteLibraryFolders(steamRoot, steamRoot, secondLibrary);
        WriteAppManifest(secondLibrary, buildId: "18234567", betaKey: null);

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.NotNull(result);
        Assert.Equal(secondLibrary, result!.LibraryPath);

        Directory.Delete(steamRoot, recursive: true);
        Directory.Delete(secondLibrary, recursive: true);
    }

    [Fact]
    public void Locate_LibraryFoldersVdfMissing_ReturnsNull()
    {
        var steamRoot = CreateTempDir();
        Directory.CreateDirectory(steamRoot);

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.Null(result);

        Directory.Delete(steamRoot, recursive: true);
    }

    [Fact]
    public void Locate_AppManifestMissingInAllLibraries_ReturnsNull()
    {
        var steamRoot = CreateTempDir();
        WriteLibraryFolders(steamRoot, steamRoot);

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.Null(result);

        Directory.Delete(steamRoot, recursive: true);
    }

    [Fact]
    public void Locate_BetaKeyPresent_ReturnsBetaBranch()
    {
        var steamRoot = CreateTempDir();
        WriteLibraryFolders(steamRoot, steamRoot);
        WriteAppManifest(steamRoot, buildId: "18234567", betaKey: "unstable");

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.NotNull(result);
        Assert.Equal("unstable", result!.Branch);

        Directory.Delete(steamRoot, recursive: true);
    }

    [Fact]
    public void Locate_NoBetaKey_ReturnsPublicBranch()
    {
        var steamRoot = CreateTempDir();
        WriteLibraryFolders(steamRoot, steamRoot);
        WriteAppManifest(steamRoot, buildId: "18234567", betaKey: null);

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.NotNull(result);
        Assert.Equal("public", result!.Branch);

        Directory.Delete(steamRoot, recursive: true);
    }

    [Fact]
    public void Locate_CorruptedLibraryFoldersVdf_ReturnsNull()
    {
        var steamRoot = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"), "{not valid vdf");

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.Null(result);

        Directory.Delete(steamRoot, recursive: true);
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private static void WriteLibraryFolders(string steamRoot, params string[] libraryPaths)
    {
        var steamappsDir = Path.Combine(steamRoot, "steamapps");
        Directory.CreateDirectory(steamappsDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"libraryfolders\"");
        sb.AppendLine("{");
        for (var i = 0; i < libraryPaths.Length; i++)
        {
            sb.AppendLine($"\t\"{i}\"");
            sb.AppendLine("\t{");
            sb.AppendLine($"\t\t\"path\"\t\t\"{libraryPaths[i].Replace(@"\", @"\\")}\"");
            sb.AppendLine("\t\t\"apps\"");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t}");
            sb.AppendLine("\t}");
        }
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(steamappsDir, "libraryfolders.vdf"), sb.ToString());
    }

    private static void WriteAppManifest(string libraryPath, string buildId, string? betaKey)
    {
        var steamappsDir = Path.Combine(libraryPath, "steamapps");
        Directory.CreateDirectory(steamappsDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"AppState\"");
        sb.AppendLine("{");
        sb.AppendLine("\t\"appid\"\t\t\"108600\"");
        sb.AppendLine("\t\"Universe\"\t\t\"1\"");
        sb.AppendLine("\t\"name\"\t\t\"Project Zomboid\"");
        sb.AppendLine("\t\"StateFlags\"\t\t\"4\"");
        sb.AppendLine("\t\"installdir\"\t\t\"ProjectZomboid\"");
        sb.AppendLine($"\t\"buildid\"\t\t\"{buildId}\"");
        sb.AppendLine("\t\"UserConfig\"");
        sb.AppendLine("\t{");
        sb.AppendLine("\t\t\"language\"\t\t\"french\"");
        if (betaKey is not null)
        {
            sb.AppendLine($"\t\t\"BetaKey\"\t\t\"{betaKey}\"");
        }
        sb.AppendLine("\t}");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(steamappsDir, "appmanifest_108600.acf"), sb.ToString());
    }
}
