using System.Text;
using GlasLauncher.Core.Logic;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class SteamWorkshopReaderTests
{
    [Fact]
    public void GetInstalledItemIds_FileMissing_ReturnsEmpty()
    {
        var libraryPath = CreateTempDir();
        Directory.CreateDirectory(libraryPath);

        var result = SteamWorkshopReader.GetInstalledItemIds(libraryPath);

        Assert.Empty(result);

        Directory.Delete(libraryPath, recursive: true);
    }

    [Fact]
    public void GetInstalledItemIds_ItemsPresent_ReturnsIds()
    {
        var libraryPath = CreateTempDir();
        WriteAppWorkshop(libraryPath, "111", "222", "333");

        var result = SteamWorkshopReader.GetInstalledItemIds(libraryPath);

        Assert.Equal(new[] { "111", "222", "333" }, result);

        Directory.Delete(libraryPath, recursive: true);
    }

    [Fact]
    public void GetInstalledItemIds_NoItemsInstalledSection_ReturnsEmpty()
    {
        var libraryPath = CreateTempDir();
        WriteAppWorkshop(libraryPath);

        var result = SteamWorkshopReader.GetInstalledItemIds(libraryPath);

        Assert.Empty(result);

        Directory.Delete(libraryPath, recursive: true);
    }

    [Fact]
    public void GetInstalledItemIds_CorruptedFile_ReturnsEmpty()
    {
        var libraryPath = CreateTempDir();
        var workshopDir = Path.Combine(libraryPath, "steamapps", "workshop");
        Directory.CreateDirectory(workshopDir);
        File.WriteAllText(Path.Combine(workshopDir, "appworkshop_108600.acf"), "{not valid vdf");

        var result = SteamWorkshopReader.GetInstalledItemIds(libraryPath);

        Assert.Empty(result);

        Directory.Delete(libraryPath, recursive: true);
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private static void WriteAppWorkshop(string libraryPath, params string[] itemIds)
    {
        var workshopDir = Path.Combine(libraryPath, "steamapps", "workshop");
        Directory.CreateDirectory(workshopDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"AppWorkshop\"");
        sb.AppendLine("{");
        sb.AppendLine("\t\"appid\"\t\t\"108600\"");
        sb.AppendLine("\t\"WorkshopItemsInstalled\"");
        sb.AppendLine("\t{");
        foreach (var id in itemIds)
        {
            sb.AppendLine($"\t\t\"{id}\"");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t\"size\"\t\t\"1000\"");
            sb.AppendLine("\t\t\t\"manifest\"\t\t\"1\"");
            sb.AppendLine("\t\t}");
        }
        sb.AppendLine("\t}");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(workshopDir, "appworkshop_108600.acf"), sb.ToString());
    }
}
