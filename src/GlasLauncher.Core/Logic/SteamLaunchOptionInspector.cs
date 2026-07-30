using Gameloop.Vdf;
using Gameloop.Vdf.Linq;

namespace GlasLauncher.Core.Logic;

public static class SteamLaunchOptionInspector
{
    private const ulong AccountIdOffset = 76561197960265728;

    public static bool AreLaunchOptionsConfigured(string steamPath, string appId, IReadOnlyList<string> requiredOptions)
    {
        var accountId = FindMostRecentAccountId(steamPath);
        if (accountId is null)
        {
            return false;
        }

        var localConfigPath = Path.Combine(steamPath, "userdata", accountId, "config", "localconfig.vdf");
        var launchOptions = ReadLaunchOptions(localConfigPath, appId);
        return launchOptions is not null && requiredOptions.All(launchOptions.Contains);
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

            var entries = users.Properties()
                .Where(user => user.Value is VObject && ulong.TryParse(user.Key, out _))
                .Select(user => (Key: user.Key, Entry: (VObject)user.Value!))
                .ToList();

            // Some real Steam clients (observed on a single-account live install) never write
            // "MostRecent" at all. When the field is present anywhere, trust it exclusively
            // (existing multi-account behavior); otherwise fall back to the highest "Timestamp".
            if (entries.Any(e => e.Entry["MostRecent"] is not null))
            {
                foreach (var (key, entry) in entries)
                {
                    if (entry["MostRecent"]?.ToString() == "1" && ulong.TryParse(key, out var steamId64))
                    {
                        return (steamId64 - AccountIdOffset).ToString();
                    }
                }

                return null;
            }

            string? mostRecentId = null;
            var highestTimestamp = long.MinValue;
            foreach (var (key, entry) in entries)
            {
                if (long.TryParse(entry["Timestamp"]?.ToString(), out var timestamp)
                    && timestamp > highestTimestamp
                    && ulong.TryParse(key, out var steamId64))
                {
                    highestTimestamp = timestamp;
                    mostRecentId = (steamId64 - AccountIdOffset).ToString();
                }
            }

            return mostRecentId;
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
            // Real localconfig.vdf files carry sibling sections (e.g. "WebStorage") with long
            // escaped-JSON strings that Gameloop.Vdf's tokenizer cannot parse — it throws
            // IndexOutOfRangeException on long enough escaped values (see
            // github.com/shravan2x/Gameloop.Vdf issues #4/#10/#16/#28, confirmed against a real
            // Steam profile). Launch options live under "Software" only, so scope parsing to
            // that block and avoid the rest of the file entirely.
            var softwareBlock = ExtractTopLevelObjectBlock(File.ReadAllText(localConfigPath), "Software");
            if (softwareBlock is null)
            {
                return null;
            }

            var root = VdfConvert.Deserialize(softwareBlock);
            if (root.Value is not VObject software
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

    /// Finds a top-level "key" { ... } block by name via a quote-aware brace scan, without
    /// deserializing the whole (potentially unparseable) document. Returns the block including
    /// its key, e.g. "Software"\n{ ... }, or null if not found.
    private static string? ExtractTopLevelObjectBlock(string text, string keyName)
    {
        var quotedKey = $"\"{keyName}\"";
        var searchFrom = 0;

        while (true)
        {
            var keyIndex = text.IndexOf(quotedKey, searchFrom, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return null;
            }

            var braceIndex = keyIndex + quotedKey.Length;
            while (braceIndex < text.Length && char.IsWhiteSpace(text[braceIndex]))
            {
                braceIndex++;
            }

            if (braceIndex >= text.Length || text[braceIndex] != '{')
            {
                searchFrom = keyIndex + quotedKey.Length;
                continue;
            }

            var depth = 0;
            var inQuotes = false;
            for (var i = braceIndex; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"' && text[i - 1] != '\\')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (inQuotes)
                {
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text[keyIndex..(i + 1)];
                    }
                }
            }

            return null;
        }
    }
}
