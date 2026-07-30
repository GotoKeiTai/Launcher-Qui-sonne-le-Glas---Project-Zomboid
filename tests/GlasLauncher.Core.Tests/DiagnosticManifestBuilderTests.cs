using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class DiagnosticManifestBuilderTests
{
    private static DiagnosticSnapshot CreateNominalSnapshot() => new(
        LauncherVersion: "0.1.6",
        WindowsDescription: "Microsoft Windows 10.0.26200",
        DetectedGameVersion: new GameVersionInfo("24432948", "legacy41"),
        RequiredGameVersion: new GameVersionRequirement("24432948", "legacy41", "41.78.20"),
        JavaModInfo: new JavaModInfo(
            LaunchOptionConfigured: true,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: new[] { new JavaFileStatus("GlasVoipMod.jar", "1.0.0", "1.0.0", true) }),
        JavaModFileHashes: new[] { new JavaModFileHash("GlasVoipMod.jar", "ABCDEF1234567890") },
        WorkshopStatus: new WorkshopStatus(
            InstalledIds: new[] { "111", "222", "333" },
            RequiredIds: new[] { "111", "222", "333" },
            CollectionId: "3719763771"),
        GeneratedAtLocal: new DateTime(2026, 7, 30, 14, 32, 10));

    [Fact]
    public void Build_NominalSnapshot_ProducesExactExpectedText()
    {
        var result = DiagnosticManifestBuilder.Build(CreateNominalSnapshot());

        var expected =
            "=== Rapport de diagnostic — Glas Launcher ===" + Environment.NewLine +
            "Généré le : 2026-07-30 14:32:10" + Environment.NewLine +
            Environment.NewLine +
            "Launcher : 0.1.6" + Environment.NewLine +
            "Windows : Microsoft Windows 10.0.26200" + Environment.NewLine +
            Environment.NewLine +
            "--- Project Zomboid ---" + Environment.NewLine +
            "Buildid détecté : 24432948" + Environment.NewLine +
            "Branche détectée : legacy41" + Environment.NewLine +
            "Buildid requis : 24432948" + Environment.NewLine +
            "Branche requise : legacy41" + Environment.NewLine +
            "Version affichée requise : 41.78.20" + Environment.NewLine +
            Environment.NewLine +
            "--- Mod Java ---" + Environment.NewLine +
            "Option de lancement configurée : oui" + Environment.NewLine +
            "Option(s) requise(s) : -javaagent:GlasVoipMod.jar" + Environment.NewLine +
            "GlasVoipMod.jar :" + Environment.NewLine +
            "  Version installée : 1.0.0" + Environment.NewLine +
            "  Version requise : 1.0.0" + Environment.NewLine +
            "  À jour : oui" + Environment.NewLine +
            "  SHA-256 : ABCDEF1234567890" + Environment.NewLine +
            Environment.NewLine +
            "--- Mods Workshop ---" + Environment.NewLine +
            "Requis : 111, 222, 333" + Environment.NewLine +
            "Détectés : 111, 222, 333" + Environment.NewLine +
            "Manquants : aucun" + Environment.NewLine;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Build_GameVersionNotDetected_ShowsIntrouvable()
    {
        var snapshot = CreateNominalSnapshot() with { DetectedGameVersion = null };

        var result = DiagnosticManifestBuilder.Build(snapshot);

        Assert.Contains("Buildid détecté : introuvable", result);
        Assert.Contains("Branche détectée : introuvable", result);
    }

    [Fact]
    public void Build_JavaModNotInstalled_ShowsNonInstalleAndHashIndisponible()
    {
        var snapshot = CreateNominalSnapshot() with
        {
            JavaModInfo = new JavaModInfo(
                LaunchOptionConfigured: true,
                RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
                Files: new[] { new JavaFileStatus("GlasVoipMod.jar", null, "1.0.0", false) }),
            JavaModFileHashes = new[] { new JavaModFileHash("GlasVoipMod.jar", null) }
        };

        var result = DiagnosticManifestBuilder.Build(snapshot);

        Assert.Contains("Version installée : non installé", result);
        Assert.Contains("  À jour : non", result);
        Assert.Contains("SHA-256 : indisponible", result);
    }

    [Fact]
    public void Build_MissingWorkshopMods_ListsMissingIds()
    {
        var snapshot = CreateNominalSnapshot() with
        {
            WorkshopStatus = new WorkshopStatus(
                InstalledIds: new[] { "111" },
                RequiredIds: new[] { "111", "222", "333" },
                CollectionId: "3719763771")
        };

        var result = DiagnosticManifestBuilder.Build(snapshot);

        Assert.Contains("Manquants : 222, 333", result);
    }
}
