using Gameloop.Vdf;
using Gameloop.Vdf.Linq;

namespace GlasLauncher.Core.Logic;

public static class SteamWorkshopReader
{
    private const string AppId = "108600";

    public static IReadOnlyList<string> GetInstalledItemIds(string libraryPath)
    {
        var manifestPath = Path.Combine(libraryPath, "steamapps", "workshop", $"appworkshop_{AppId}.acf");
        if (!File.Exists(manifestPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(manifestPath));
            if (root.Value is not VObject appWorkshop)
            {
                return Array.Empty<string>();
            }

            if (appWorkshop["WorkshopItemsInstalled"] is not VObject installed)
            {
                return Array.Empty<string>();
            }

            return installed.Properties().Select(p => p.Key).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }
}
