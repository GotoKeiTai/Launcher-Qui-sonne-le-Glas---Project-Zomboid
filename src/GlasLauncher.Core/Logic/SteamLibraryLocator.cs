using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class SteamLibraryLocator
{
    private const string AppId = "108600";

    public static SteamGameLocation? Locate(string steamPath)
    {
        foreach (var libraryPath in GetLibraryPaths(steamPath))
        {
            var manifestPath = Path.Combine(libraryPath, "steamapps", $"appmanifest_{AppId}.acf");
            var location = TryReadManifest(libraryPath, manifestPath);
            if (location is not null)
            {
                return location;
            }
        }

        return null;
    }

    private static List<string> GetLibraryPaths(string steamPath)
    {
        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
        {
            return new List<string>();
        }

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(libraryFoldersPath));
            if (root.Value is not VObject libraries)
            {
                return new List<string>();
            }

            var paths = new List<string>();
            foreach (var library in libraries.Properties())
            {
                if (library.Value is VObject entry && entry["path"] is { } path)
                {
                    paths.Add(path.ToString());
                }
            }

            return paths;
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }

    private static SteamGameLocation? TryReadManifest(string libraryPath, string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(manifestPath));
            if (root.Value is not VObject appState)
            {
                return null;
            }

            var installDir = appState["installdir"]?.ToString();
            var buildId = appState["buildid"]?.ToString();
            if (string.IsNullOrEmpty(installDir) || string.IsNullOrEmpty(buildId))
            {
                return null;
            }

            var branch = "public";
            if (appState["UserConfig"] is VObject userConfig)
            {
                var betaKeyProperty = userConfig.Properties()
                    .FirstOrDefault(p => string.Equals(p.Key, "BetaKey", StringComparison.OrdinalIgnoreCase));
                var betaKeyValue = betaKeyProperty?.Value.ToString();
                if (!string.IsNullOrEmpty(betaKeyValue))
                {
                    branch = betaKeyValue;
                }
            }

            var installPath = Path.Combine(libraryPath, "steamapps", "common", installDir);
            return new SteamGameLocation(libraryPath, installPath, buildId, branch);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
