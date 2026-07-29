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

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private static void WriteLoginUsers(string steamPath, string steamId64, bool mostRecent)
    {
        var configDir = Path.Combine(steamPath, "config");
        Directory.CreateDirectory(configDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"users\"");
        sb.AppendLine("{");
        sb.AppendLine($"\t\"{steamId64}\"");
        sb.AppendLine("\t{");
        sb.AppendLine("\t\t\"AccountName\"\t\t\"testuser\"");
        sb.AppendLine("\t\t\"PersonaName\"\t\t\"Test User\"");
        sb.AppendLine($"\t\t\"MostRecent\"\t\t\"{(mostRecent ? "1" : "0")}\"");
        sb.AppendLine("\t\t\"Timestamp\"\t\t\"1700000000\"");
        sb.AppendLine("\t}");
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
