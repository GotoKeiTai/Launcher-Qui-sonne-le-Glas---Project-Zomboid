using Gameloop.Vdf;
using Gameloop.Vdf.Linq;

namespace GlasLauncher.Core.Logic;

public static class SteamLaunchOptionInspector
{
    private const ulong AccountIdOffset = 76561197960265728;

    public static bool IsLaunchOptionConfigured(string steamPath, string appId, string requiredOption)
    {
        var accountId = FindMostRecentAccountId(steamPath);
        if (accountId is null)
        {
            return false;
        }

        var localConfigPath = Path.Combine(steamPath, "userdata", accountId, "config", "localconfig.vdf");
        var launchOptions = ReadLaunchOptions(localConfigPath, appId);
        return launchOptions is not null && launchOptions.Contains(requiredOption);
    }

    private static string? FindMostRecentAccountId(string steamPath)
    {
        var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
        if (!File.Exists(loginUsersPath))
        {
            return null;
        }

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(loginUsersPath));
            if (root.Value is not VObject users)
            {
                return null;
            }

            foreach (var user in users.Properties())
            {
                if (user.Value is VObject entry
                    && entry["MostRecent"]?.ToString() == "1"
                    && ulong.TryParse(user.Key, out var steamId64))
                {
                    return (steamId64 - AccountIdOffset).ToString();
                }
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ReadLaunchOptions(string localConfigPath, string appId)
    {
        if (!File.Exists(localConfigPath))
        {
            return null;
        }

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(localConfigPath));
            if (root.Value is not VObject store
                || store["Software"] is not VObject software
                || software["Valve"] is not VObject valve
                || valve["Steam"] is not VObject steam
                || steam["apps"] is not VObject apps
                || apps[appId] is not VObject app)
            {
                return null;
            }

            return app["LaunchOptions"]?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
