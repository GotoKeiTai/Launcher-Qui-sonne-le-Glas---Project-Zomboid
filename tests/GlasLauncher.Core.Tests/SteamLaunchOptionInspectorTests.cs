using System.Text;
using GlasLauncher.Core.Logic;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class SteamLaunchOptionInspectorTests
{
    private const string SteamId64 = "76561197960265729";
    private const string AccountId = "1";
    private const string AppId = "108600";
    private static readonly string[] RequiredOptions = { "-agentlib:zbNative --" };

    [Fact]
    public void AreLaunchOptionsConfigured_OptionPresent_ReturnsTrue()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-agentlib:zbNative --");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_OptionPresentAmongOthers_ReturnsTrue()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-high -agentlib:zbNative --");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_OptionAbsent_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-high");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_AppEntryMissing_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, launchOptions: null);

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_LoginUsersVdfMissing_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        Directory.CreateDirectory(steamPath);

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_NoMostRecentAccount_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: false);
        WriteLocalConfig(steamPath, AccountId, AppId, "-agentlib:zbNative --");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_LocalConfigVdfMissing_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_CorruptedLoginUsersVdf_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(steamPath, "config"));
        File.WriteAllText(Path.Combine(steamPath, "config", "loginusers.vdf"), "{not valid vdf");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_MultipleOptionsAllPresent_ReturnsTrue()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-javaagent:GlasVoipMod.jar -agentlib:zbNative --");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(
            steamPath, AppId, new[] { "-javaagent:GlasVoipMod.jar", "-agentlib:zbNative --" });

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_OneOfMultipleOptionsMissing_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-javaagent:GlasVoipMod.jar");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(
            steamPath, AppId, new[] { "-javaagent:GlasVoipMod.jar", "-agentlib:zbNative --" });

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_EmptyRequiredList_ReturnsTrue()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-high");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, Array.Empty<string>());

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_MostRecentFieldAbsent_FallsBackToTimestamp_ReturnsTrue()
    {
        // Reproduces a real Steam client's loginusers.vdf (observed on a live install): a single
        // account with a "Timestamp" field but no "MostRecent" field at all.
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, new[] { (SteamId64, MostRecent: (bool?)null, Timestamp: "1700000000") });
        WriteLocalConfig(steamPath, AccountId, AppId, "-agentlib:zbNative --");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_UnrelatedLongEscapedJsonElsewhereInFile_StillReadsLaunchOptions_ReturnsTrue()
    {
        // Reproduces a real Steam client's localconfig.vdf: Steam's "WebStorage" section stores
        // long JSON blobs as escaped-quote strings (e.g. cached tag/preference lists running to
        // tens of thousands of characters). Gameloop.Vdf's tokenizer throws IndexOutOfRangeException
        // on long enough escaped strings (github.com/shravan2x/Gameloop.Vdf issues #4/#10/#16/#28;
        // confirmed empirically against a real localconfig.vdf and with a synthetic repro — failure
        // starts around 309 escaped "key":"value" pairs in one string). A full-file deserialize
        // throws; the check must still find LaunchOptions, which lives in a different, unaffected
        // top-level section ("Software").
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);

        var configDir = Path.Combine(steamPath, "userdata", AccountId, "config");
        Directory.CreateDirectory(configDir);

        var longEscapedJson = new StringBuilder();
        for (var i = 0; i < 500; i++)
        {
            longEscapedJson.Append($"\\\"k{i}\\\":\\\"v{i}\\\",");
        }

        var vdf = "\"UserLocalConfigStore\"\n{\n"
            + "\t\"WebStorage\"\n\t{\n"
            + $"\t\t\"CachedTagNames\"\t\t\"{{{longEscapedJson}}}\"\n"
            + "\t}\n"
            + "\t\"Software\"\n\t{\n"
            + "\t\t\"Valve\"\n\t\t{\n"
            + "\t\t\t\"Steam\"\n\t\t\t{\n"
            + "\t\t\t\t\"apps\"\n\t\t\t\t{\n"
            + $"\t\t\t\t\t\"{AppId}\"\n\t\t\t\t\t{{\n"
            + "\t\t\t\t\t\t\"LaunchOptions\"\t\t\"-agentlib:zbNative --\"\n"
            + "\t\t\t\t\t}\n"
            + "\t\t\t\t}\n"
            + "\t\t\t}\n"
            + "\t\t}\n"
            + "\t}\n"
            + "}\n";
        File.WriteAllText(Path.Combine(configDir, "localconfig.vdf"), vdf);

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_MostRecentFieldAbsentOnAllAccounts_PicksHighestTimestamp_ReturnsTrue()
    {
        var steamPath = CreateTempDir();
        const string olderSteamId64 = "76561197960265729"; // AccountId "1"
        const string newerSteamId64 = "76561197960265730"; // AccountId "2"
        WriteLoginUsers(steamPath, new[]
        {
            (SteamId64: olderSteamId64, MostRecent: (bool?)null, Timestamp: "1000"),
            (SteamId64: newerSteamId64, MostRecent: (bool?)null, Timestamp: "2000"),
        });
        WriteLocalConfig(steamPath, accountId: "2", AppId, "-agentlib:zbNative --");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private static void WriteLoginUsers(string steamPath, string steamId64, bool mostRecent) =>
        WriteLoginUsers(steamPath, new[] { (SteamId64: steamId64, MostRecent: (bool?)mostRecent, Timestamp: "1700000000") });

    private static void WriteLoginUsers(string steamPath, IEnumerable<(string SteamId64, bool? MostRecent, string Timestamp)> accounts)
    {
        var configDir = Path.Combine(steamPath, "config");
        Directory.CreateDirectory(configDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"users\"");
        sb.AppendLine("{");
        foreach (var account in accounts)
        {
            sb.AppendLine($"\t\"{account.SteamId64}\"");
            sb.AppendLine("\t{");
            sb.AppendLine("\t\t\"AccountName\"\t\t\"testuser\"");
            sb.AppendLine("\t\t\"PersonaName\"\t\t\"Test User\"");
            if (account.MostRecent.HasValue)
            {
                sb.AppendLine($"\t\t\"MostRecent\"\t\t\"{(account.MostRecent.Value ? "1" : "0")}\"");
            }
            sb.AppendLine($"\t\t\"Timestamp\"\t\t\"{account.Timestamp}\"");
            sb.AppendLine("\t}");
        }
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(configDir, "loginusers.vdf"), sb.ToString());
    }

    private static void WriteLocalConfig(string steamPath, string accountId, string appId, string? launchOptions)
    {
        var configDir = Path.Combine(steamPath, "userdata", accountId, "config");
        Directory.CreateDirectory(configDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"UserLocalConfigStore\"");
        sb.AppendLine("{");
        sb.AppendLine("\t\"Software\"");
        sb.AppendLine("\t{");
        sb.AppendLine("\t\t\"Valve\"");
        sb.AppendLine("\t\t{");
        sb.AppendLine("\t\t\t\"Steam\"");
        sb.AppendLine("\t\t\t{");
        sb.AppendLine("\t\t\t\t\"apps\"");
        sb.AppendLine("\t\t\t\t{");
        if (launchOptions is not null)
        {
            sb.AppendLine($"\t\t\t\t\t\"{appId}\"");
            sb.AppendLine("\t\t\t\t\t{");
            sb.AppendLine($"\t\t\t\t\t\t\"LaunchOptions\"\t\t\"{launchOptions}\"");
            sb.AppendLine("\t\t\t\t\t}");
        }
        sb.AppendLine("\t\t\t\t}");
        sb.AppendLine("\t\t\t}");
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t}");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(configDir, "localconfig.vdf"), sb.ToString());
    }
}
