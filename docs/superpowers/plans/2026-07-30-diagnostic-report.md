# Rapport de diagnostic réel — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remplacer le bouton "Générer un rapport de diagnostic" (actuellement une simulation pure) par une vraie génération de rapport `.zip`, et corriger `SettingsViewModel.VersionInfoText` qui affiche encore des littéraux figés.

**Architecture:** Deux nouvelles classes pures/testables côté `Core/Logic` (`WorkshopRequirement`, `DiagnosticManifestBuilder`), deux nouveaux services côté `Core/Services` sans split Real/Fake (`ILauncherLogger`/`FileLauncherLogger`, `IDiagnosticReportService`/`DiagnosticReportService`), puis câblage côté `App` : `SettingsViewModel` (bouton + texte de version), `DashboardViewModel`/`MainWindowViewModel`/`RepairModalViewModel`/`UpdateModalViewModel` (points de journalisation).

**Tech Stack:** .NET 8, Avalonia/CommunityToolkit.Mvvm (App), xUnit (Core.Tests), `System.IO.Compression.ZipFile` pour le zip.

## Global Constraints

- Spec de référence : `docs/superpowers/specs/2026-07-30-diagnostic-report-design.md` — toute divergence avec ce plan doit être signalée, pas résolue silencieusement.
- Aucune classe créée dans cette spec n'a de split Real/Fake `OperatingSystem.IsWindows()` : `FileLauncherLogger` et `DiagnosticReportService` n'utilisent que des API portables (`File`, `Directory`, `ZipFile`, `RuntimeInformation.OSDescription`), contrairement à `SteamEnvironment`/`JavaModService`/`VelopackUpdateService` qui ont une vraie raison Windows-spécifique de brancher. Enregistrement DI direct, sans ternaire.
- Convention déjà établie dans ce projet : aucun test dédié pour les classes d'orchestration I/O (`SteamEnvironment`, `JavaModService`, et maintenant `FileLauncherLogger`/`DiagnosticReportService`) — seule la logique pure (`Core/Logic/*`) a des tests xUnit. `GlasLauncher.App` n'a aucun test dédié (aucun ViewModel n'est testé directement dans ce projet) — pour les tâches touchant `GlasLauncher.App`, la vérification se fait via `dotnet build` (compilation) + `dotnet test` (suite complète, doit rester verte), pas via de nouveaux tests.
- Vérification visuelle finale (build + lancement réel de l'app + captures d'écran) faite par l'orchestrateur après la Tâche 4, pas par les implémenteurs de chaque tâche.
- Toutes les chaînes affichées au joueur sont en français, cohérent avec le reste de l'app.
- `ImplicitUsings` est activé sur `GlasLauncher.Core` (System, System.Collections.Generic, System.IO, System.Linq, System.Threading.Tasks disponibles sans `using` explicite) — pas sur `GlasLauncher.App` (usings explicites requis). `System.Text` (pour `StringBuilder`) et `System.Security.Cryptography` (pour `SHA256`) ne sont PAS dans les implicites par défaut et nécessitent un `using` explicite même dans Core.

---

### Task 1: Modèles + `WorkshopRequirement` + `DiagnosticManifestBuilder`

**Files:**
- Create: `src/GlasLauncher.Core/Models/JavaModFileHash.cs`
- Create: `src/GlasLauncher.Core/Models/DiagnosticSnapshot.cs`
- Create: `src/GlasLauncher.Core/Logic/WorkshopRequirement.cs`
- Create: `src/GlasLauncher.Core/Logic/DiagnosticManifestBuilder.cs`
- Test: `tests/GlasLauncher.Core.Tests/DiagnosticManifestBuilderTests.cs`

**Interfaces:**
- Produces: `JavaModFileHash(string FileName, string? Sha256)`, `DiagnosticSnapshot(string LauncherVersion, string WindowsDescription, GameVersionInfo? DetectedGameVersion, GameVersionRequirement RequiredGameVersion, JavaModInfo JavaModInfo, IReadOnlyList<JavaModFileHash> JavaModFileHashes, WorkshopStatus WorkshopStatus, DateTime GeneratedAtLocal)`, `WorkshopRequirement.RequiredIds` (`IReadOnlyList<string>`), `WorkshopRequirement.CollectionId` (`string`), `DiagnosticManifestBuilder.Build(DiagnosticSnapshot snapshot)` → `string`. Ces quatre éléments sont consommés par la Tâche 2 (`DiagnosticReportService`) et la Tâche 3 (`DashboardViewModel` pour `WorkshopRequirement`).

- [ ] **Step 1: Créer les deux nouveaux modèles**

`src/GlasLauncher.Core/Models/JavaModFileHash.cs` :

```csharp
namespace GlasLauncher.Core.Models;

public record JavaModFileHash(string FileName, string? Sha256);
```

`src/GlasLauncher.Core/Models/DiagnosticSnapshot.cs` :

```csharp
namespace GlasLauncher.Core.Models;

public record DiagnosticSnapshot(
    string LauncherVersion,
    string WindowsDescription,
    GameVersionInfo? DetectedGameVersion,
    GameVersionRequirement RequiredGameVersion,
    JavaModInfo JavaModInfo,
    IReadOnlyList<JavaModFileHash> JavaModFileHashes,
    WorkshopStatus WorkshopStatus,
    DateTime GeneratedAtLocal);
```

- [ ] **Step 2: Créer `WorkshopRequirement`**

`src/GlasLauncher.Core/Logic/WorkshopRequirement.cs` :

```csharp
namespace GlasLauncher.Core.Logic;

public static class WorkshopRequirement
{
    public static readonly IReadOnlyList<string> RequiredIds = new[] { "111", "222", "333" };
    public const string CollectionId = "3719763771";
}
```

Ces valeurs sont la copie exacte de celles actuellement codées en dur dans `DashboardViewModel.RefreshAsync` (`src/GlasLauncher.App/ViewModels/DashboardViewModel.cs`) — la Tâche 3 les remplacera par une référence à cette classe.

- [ ] **Step 3: Écrire le test du cas nominal (échoue, `DiagnosticManifestBuilder` n'existe pas encore)**

`tests/GlasLauncher.Core.Tests/DiagnosticManifestBuilderTests.cs` :

```csharp
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
```

- [ ] **Step 4: Lancer les tests, vérifier qu'ils échouent avec "type or namespace 'DiagnosticManifestBuilder' could not be found"**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter "FullyQualifiedName~DiagnosticManifestBuilderTests"`
Expected: FAIL (compilation) — `DiagnosticManifestBuilder` n'existe pas encore.

- [ ] **Step 5: Implémenter `DiagnosticManifestBuilder`**

`src/GlasLauncher.Core/Logic/DiagnosticManifestBuilder.cs` :

```csharp
using System.Text;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class DiagnosticManifestBuilder
{
    public static string Build(DiagnosticSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Rapport de diagnostic — Glas Launcher ===");
        sb.AppendLine($"Généré le : {snapshot.GeneratedAtLocal:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"Launcher : {snapshot.LauncherVersion}");
        sb.AppendLine($"Windows : {snapshot.WindowsDescription}");
        sb.AppendLine();
        sb.AppendLine("--- Project Zomboid ---");
        sb.AppendLine($"Buildid détecté : {snapshot.DetectedGameVersion?.BuildId ?? "introuvable"}");
        sb.AppendLine($"Branche détectée : {snapshot.DetectedGameVersion?.Branch ?? "introuvable"}");
        sb.AppendLine($"Buildid requis : {snapshot.RequiredGameVersion.RequiredBuildId}");
        sb.AppendLine($"Branche requise : {snapshot.RequiredGameVersion.RequiredBranch}");
        sb.AppendLine($"Version affichée requise : {snapshot.RequiredGameVersion.DisplayVersion}");
        sb.AppendLine();
        sb.AppendLine("--- Mod Java ---");
        sb.AppendLine($"Option de lancement configurée : {(snapshot.JavaModInfo.LaunchOptionConfigured ? "oui" : "non")}");
        sb.AppendLine($"Option(s) requise(s) : {string.Join(" ", snapshot.JavaModInfo.RequiredLaunchOptions)}");
        foreach (var file in snapshot.JavaModInfo.Files)
        {
            var hash = snapshot.JavaModFileHashes.FirstOrDefault(h => h.FileName == file.FileName)?.Sha256 ?? "indisponible";
            sb.AppendLine($"{file.FileName} :");
            sb.AppendLine($"  Version installée : {file.InstalledVersion ?? "non installé"}");
            sb.AppendLine($"  Version requise : {file.RequiredVersion}");
            sb.AppendLine($"  À jour : {(file.IsUpToDate ? "oui" : "non")}");
            sb.AppendLine($"  SHA-256 : {hash}");
        }
        sb.AppendLine();
        sb.AppendLine("--- Mods Workshop ---");
        sb.AppendLine($"Requis : {string.Join(", ", snapshot.WorkshopStatus.RequiredIds)}");
        sb.AppendLine($"Détectés : {string.Join(", ", snapshot.WorkshopStatus.InstalledIds)}");
        var missing = snapshot.WorkshopStatus.RequiredIds.Except(snapshot.WorkshopStatus.InstalledIds).ToList();
        sb.AppendLine($"Manquants : {(missing.Count == 0 ? "aucun" : string.Join(", ", missing))}");

        return sb.ToString();
    }
}
```

- [ ] **Step 6: Lancer les tests, vérifier qu'ils passent**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter "FullyQualifiedName~DiagnosticManifestBuilderTests"`
Expected: PASS (4/4).

- [ ] **Step 7: Lancer toute la suite Core (non-régression) et commit**

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS (69/69 — 65 existants + 4 nouveaux).

```bash
git add src/GlasLauncher.Core/Models/JavaModFileHash.cs src/GlasLauncher.Core/Models/DiagnosticSnapshot.cs src/GlasLauncher.Core/Logic/WorkshopRequirement.cs src/GlasLauncher.Core/Logic/DiagnosticManifestBuilder.cs tests/GlasLauncher.Core.Tests/DiagnosticManifestBuilderTests.cs
git commit -m "feat(core): add diagnostic manifest builder and workshop requirement constants"
```

---

### Task 2: `ILauncherLogger` + `IDiagnosticReportService`

**Files:**
- Create: `src/GlasLauncher.Core/Services/ILauncherLogger.cs`
- Create: `src/GlasLauncher.Core/Services/FileLauncherLogger.cs`
- Create: `src/GlasLauncher.Core/Services/IDiagnosticReportService.cs`
- Create: `src/GlasLauncher.Core/Services/DiagnosticReportService.cs`

**Interfaces:**
- Consumes: `DiagnosticSnapshot`, `JavaModFileHash`, `WorkshopRequirement.RequiredIds`/`.CollectionId`, `DiagnosticManifestBuilder.Build(...)` (Tâche 1) ; `ISteamEnvironment`, `IServerInfoService`, `IJavaModService`, `IUpdateService` (déjà existants, signatures inchangées).
- Produces: `ILauncherLogger` (`string? CurrentLogFilePath { get; }`, `void Info(string message)`, `void Error(string message, Exception? exception = null)`), `IDiagnosticReportService` (`Task<string> GenerateAsync()`). Consommés par la Tâche 3 (`SettingsViewModel`, enregistrement DI) et la Tâche 4 (`DashboardViewModel`, `MainWindowViewModel`, `RepairModalViewModel`, `UpdateModalViewModel`).

- [ ] **Step 1: Créer `ILauncherLogger`**

`src/GlasLauncher.Core/Services/ILauncherLogger.cs` :

```csharp
namespace GlasLauncher.Core.Services;

public interface ILauncherLogger
{
    string? CurrentLogFilePath { get; }
    void Info(string message);
    void Error(string message, Exception? exception = null);
}
```

- [ ] **Step 2: Créer `FileLauncherLogger`**

`src/GlasLauncher.Core/Services/FileLauncherLogger.cs` :

```csharp
namespace GlasLauncher.Core.Services;

public class FileLauncherLogger : ILauncherLogger
{
    private readonly string? _logFilePath;

    public FileLauncherLogger()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlasLauncher", "logs");
            Directory.CreateDirectory(dir);
            _logFilePath = Path.Combine(dir, $"session-{DateTime.Now:yyyy-MM-dd-HHmmss}.log");
        }
        catch (Exception)
        {
            _logFilePath = null;
        }
    }

    public string? CurrentLogFilePath => _logFilePath;

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} — {exception}");

    private void Write(string level, string message)
    {
        if (_logFilePath is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(_logFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Best-effort — jamais d'exception propagée pour un simple log.
        }
    }
}
```

- [ ] **Step 3: Créer `IDiagnosticReportService`**

`src/GlasLauncher.Core/Services/IDiagnosticReportService.cs` :

```csharp
namespace GlasLauncher.Core.Services;

public interface IDiagnosticReportService
{
    Task<string> GenerateAsync();
}
```

- [ ] **Step 4: Implémenter `DiagnosticReportService`**

`src/GlasLauncher.Core/Services/DiagnosticReportService.cs` :

```csharp
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public class DiagnosticReportService : IDiagnosticReportService
{
    private readonly ISteamEnvironment _steamEnvironment;
    private readonly IServerInfoService _serverInfoService;
    private readonly IJavaModService _javaModService;
    private readonly IUpdateService _updateService;
    private readonly ILauncherLogger _logger;

    public DiagnosticReportService(
        ISteamEnvironment steamEnvironment,
        IServerInfoService serverInfoService,
        IJavaModService javaModService,
        IUpdateService updateService,
        ILauncherLogger logger)
    {
        _steamEnvironment = steamEnvironment;
        _serverInfoService = serverInfoService;
        _javaModService = javaModService;
        _updateService = updateService;
        _logger = logger;
    }

    public async Task<string> GenerateAsync()
    {
        try
        {
            var detectedVersion = await _steamEnvironment.GetInstalledGameVersionAsync();
            var requiredVersion = await _serverInfoService.GetGameVersionRequirementAsync();
            var javaModInfo = await _javaModService.GetStatusAsync();
            var installPath = await _steamEnvironment.GetGameInstallPathAsync();
            var workshopStatus = await _steamEnvironment.GetWorkshopStatusAsync(
                WorkshopRequirement.RequiredIds, WorkshopRequirement.CollectionId);

            var fileHashes = javaModInfo.Files
                .Select(f => new JavaModFileHash(f.FileName, TryComputeSha256(installPath, f.FileName)))
                .ToList();

            var snapshot = new DiagnosticSnapshot(
                LauncherVersion: _updateService.GetCurrentVersion(),
                WindowsDescription: RuntimeInformation.OSDescription,
                DetectedGameVersion: detectedVersion,
                RequiredGameVersion: requiredVersion,
                JavaModInfo: javaModInfo,
                JavaModFileHashes: fileHashes,
                WorkshopStatus: workshopStatus,
                GeneratedAtLocal: DateTime.Now);

            var manifestText = DiagnosticManifestBuilder.Build(snapshot);

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var zipPath = Path.Combine(desktopPath, $"GlasLauncher-diagnostic-{DateTime.Now:yyyy-MM-dd-HHmm}.zip");

            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var manifestEntry = zip.CreateEntry("manifest.txt");
                await using (var writer = new StreamWriter(manifestEntry.Open()))
                {
                    await writer.WriteAsync(manifestText);
                }

                if (_logger.CurrentLogFilePath is not null && File.Exists(_logger.CurrentLogFilePath))
                {
                    zip.CreateEntryFromFile(_logger.CurrentLogFilePath, "launcher.log");
                }

                var pzLogsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid", "Logs");
                if (Directory.Exists(pzLogsPath))
                {
                    var cutoff = DateTime.UtcNow.AddDays(-3);
                    foreach (var file in Directory.GetFiles(pzLogsPath))
                    {
                        if (File.GetLastWriteTimeUtc(file) >= cutoff)
                        {
                            zip.CreateEntryFromFile(file, $"projectzomboid-logs/{Path.GetFileName(file)}");
                        }
                    }
                }
            }

            _logger.Info($"Rapport de diagnostic généré : {zipPath}");
            return zipPath;
        }
        catch (Exception ex)
        {
            _logger.Error("Échec de la génération du rapport de diagnostic", ex);
            throw new InvalidOperationException("Impossible de générer le rapport de diagnostic.", ex);
        }
    }

    private static string? TryComputeSha256(string? installPath, string fileName)
    {
        if (installPath is null)
        {
            return null;
        }

        try
        {
            var filePath = Path.Combine(installPath, fileName);
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
```

- [ ] **Step 5: Vérifier que tout compile et que la suite Core reste verte**

Run: `dotnet build src/GlasLauncher.Core`
Expected: 0 erreur.

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS (69/69 — inchangé depuis la Tâche 1, ces deux nouvelles classes n'ont pas de test dédié par convention établie, voir Global Constraints).

- [ ] **Step 6: Commit**

```bash
git add src/GlasLauncher.Core/Services/ILauncherLogger.cs src/GlasLauncher.Core/Services/FileLauncherLogger.cs src/GlasLauncher.Core/Services/IDiagnosticReportService.cs src/GlasLauncher.Core/Services/DiagnosticReportService.cs
git commit -m "feat(core): add launcher logging and real diagnostic report generation"
```

---

### Task 3: Câblage DI + `SettingsViewModel` + `DashboardViewModel`

**Files:**
- Modify: `src/GlasLauncher.App/App.axaml.cs`
- Modify: `src/GlasLauncher.App/ViewModels/SettingsViewModel.cs`
- Modify: `src/GlasLauncher.App/ViewModels/DashboardViewModel.cs`

**Interfaces:**
- Consumes: `ILauncherLogger`, `IDiagnosticReportService` (Tâche 2), `WorkshopRequirement` (Tâche 1).
- Produces: rien de nouveau consommé par une tâche suivante — la Tâche 4 câble `ILauncherLogger` dans d'autres fichiers indépendamment (déjà enregistré en DI par cette tâche-ci).

- [ ] **Step 1: Enregistrer les deux nouveaux services dans `App.axaml.cs`**

Dans `RegisterServices`, juste après le bloc `services.AddSingleton<IServerInfoService, FakeServerInfoService>();` / avant `services.AddSingleton<IFirstRunStore>(...)` (voir fichier actuel), ajouter :

```csharp
        services.AddSingleton<ILauncherLogger>(_ => new FileLauncherLogger());
        services.AddSingleton<IDiagnosticReportService, DiagnosticReportService>();
```

Pas de nouveau `using` nécessaire — `GlasLauncher.Core.Services` est déjà importé dans ce fichier.

- [ ] **Step 2: Remplacer les IDs Workshop codés en dur dans `DashboardViewModel`**

Dans `src/GlasLauncher.App/ViewModels/DashboardViewModel.cs`, remplacer :

```csharp
            var workshopStatus = await _steamEnvironment.GetWorkshopStatusAsync(
                requiredIds: new[] { "111", "222", "333" },
                collectionId: "3719763771");
```

par :

```csharp
            var workshopStatus = await _steamEnvironment.GetWorkshopStatusAsync(
                requiredIds: WorkshopRequirement.RequiredIds,
                collectionId: WorkshopRequirement.CollectionId);
```

Pas de nouveau `using` nécessaire — `GlasLauncher.Core.Logic` est déjà importé dans ce fichier (utilisé par `GameVersionEvaluator`/`JavaModEvaluator`/`WorkshopEvaluator`).

- [ ] **Step 3: Réécrire `SettingsViewModel` — constructeur, `VersionInfoText`, bouton rapport**

Dans `src/GlasLauncher.App/ViewModels/SettingsViewModel.cs`, remplacer le haut du fichier (usings, champs, constructeur, `VersionInfoText`) :

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlasLauncher.Core.Services;

namespace GlasLauncher.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private const string DiscordInviteUrl = "https://discord.gg/UmKM25QUhY";

    private readonly IUpdateService _updateService;
    private readonly ISteamEnvironment _steamEnvironment;
    private readonly IJavaModService _javaModService;
    private readonly IDiagnosticReportService _diagnosticReportService;

    public SettingsViewModel(
        IUpdateService updateService,
        ISteamEnvironment steamEnvironment,
        IJavaModService javaModService,
        IDiagnosticReportService diagnosticReportService)
    {
        _updateService = updateService;
        _steamEnvironment = steamEnvironment;
        _javaModService = javaModService;
        _diagnosticReportService = diagnosticReportService;
        _versionInfoText = $"Launcher {_updateService.GetCurrentVersion()} · Chargement…";
        _ = RefreshVersionInfoAsync();
    }

    [ObservableProperty]
    private string _versionInfoText;

    private async Task RefreshVersionInfoAsync()
    {
        var detectedVersion = await _steamEnvironment.GetInstalledGameVersionAsync();
        var javaModInfo = await _javaModService.GetStatusAsync();
        var javaModFile = javaModInfo.Files.FirstOrDefault();
        var javaModVersionText = javaModFile switch
        {
            null => "non installé",
            { IsUpToDate: true } => $"v{javaModFile.InstalledVersion}",
            _ => "non installé"
        };

        VersionInfoText =
            $"Launcher {_updateService.GetCurrentVersion()} · Project Zomboid {detectedVersion?.BuildId ?? "introuvable"} · Mod Java {javaModVersionText}";
    }

    public event Action? BackRequested;
```

(le reste du fichier — `InstallPath`, `StatusMessage`, `IsStatusSuccess`, `BrowseAsync`, `OpenLauncherLogs`, `OpenPzLogs`, `JoinDiscord`, `Back`, `GetMainWindow`, `GetLauncherLogsPath`, `GetPzLogsPath` — reste identique, seule `VersionInfoText` change de forme : propriété calculée → `[ObservableProperty]` rafraîchie de façon asynchrone).

Remplacer ensuite :

```csharp
    [RelayCommand]
    private void GenerateDiagnosticReport()
    {
        StatusMessage = "Rapport généré (simulation).";
        IsStatusSuccess = true;
    }
```

par :

```csharp
    [RelayCommand]
    private async Task GenerateDiagnosticReportAsync()
    {
        try
        {
            var zipPath = await _diagnosticReportService.GenerateAsync();
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zipPath}\"") { UseShellExecute = true });
            StatusMessage = "Rapport généré et Explorateur ouvert.";
            IsStatusSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de générer le rapport : " + ex.Message;
            IsStatusSuccess = false;
        }
    }
```

`CopyVersionInfoAsync` (méthode existante, inchangée) continue de fonctionner tel quel : elle lit `VersionInfoText`, qui est maintenant une propriété observable au lieu d'un getter calculé — aucun changement de son propre corps nécessaire.

- [ ] **Step 4: Vérifier que tout compile**

Run: `dotnet build src/GlasLauncher.App -c Debug`
Expected: 0 erreur. (`SettingsViewModel` est enregistré via `services.AddSingleton<SettingsViewModel>();` dans `App.axaml.cs` — le conteneur DI résout automatiquement les nouveaux paramètres de constructeur du moment que `ISteamEnvironment`, `IJavaModService` et `IDiagnosticReportService` sont déjà enregistrés, ce qui est déjà le cas.)

- [ ] **Step 5: Suite complète + commit**

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS (69/69, inchangé — `GlasLauncher.App` n'a pas de tests dédiés, voir Global Constraints).

```bash
git add src/GlasLauncher.App/App.axaml.cs src/GlasLauncher.App/ViewModels/SettingsViewModel.cs src/GlasLauncher.App/ViewModels/DashboardViewModel.cs
git commit -m "feat(app): wire real diagnostic report generation and fix VersionInfoText"
```

---

### Task 4: Journalisation — `DashboardViewModel`, `MainWindowViewModel`, `RepairModalViewModel`, `UpdateModalViewModel`

**Files:**
- Modify: `src/GlasLauncher.App/ViewModels/DashboardViewModel.cs`
- Modify: `src/GlasLauncher.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/GlasLauncher.App/ViewModels/RepairModalViewModel.cs`
- Modify: `src/GlasLauncher.App/ViewModels/UpdateModalViewModel.cs`

**Interfaces:**
- Consumes: `ILauncherLogger` (Tâche 2, déjà enregistré en DI par la Tâche 3).
- Produces: rien de nouveau — dernière tâche du plan.

**Contexte important pour cette tâche** : `RepairModalViewModel` et `UpdateModalViewModel` ne sont PAS enregistrés dans le conteneur DI (pas de `services.AddSingleton<...>()` pour eux) — ils sont construits directement via `new` à l'intérieur de `MainWindowViewModel` (`OnRepairRequested` et `CheckForUpdatesAsync`). Ajouter un paramètre à leur constructeur EXIGE de mettre à jour ces deux call sites dans le MÊME commit, sinon `GlasLauncher.App` ne compile plus entre les deux changements — c'est pourquoi les quatre fichiers sont dans une seule tâche plutôt que répartis.

- [ ] **Step 1: `DashboardViewModel` — logger le résultat de `RefreshAsync`**

Ajouter le champ et le paramètre de constructeur :

```csharp
    private readonly ILauncherLogger _logger;

    public DashboardViewModel(
        ISteamEnvironment steamEnvironment,
        IServerInfoService serverInfoService,
        IJavaModService javaModService,
        IUpdateService updateService,
        ILauncherLogger logger)
    {
        _steamEnvironment = steamEnvironment;
        _serverInfoService = serverInfoService;
        _javaModService = javaModService;
        _updateService = updateService;
        _logger = logger;
        Checks = new ObservableCollection<CheckItemViewModel>();
        News = new ObservableCollection<NewsItem>();
        LauncherVersionText = _updateService.GetCurrentVersion();

        _ = RefreshAsync();
    }
```

Dans `RefreshAsync()`, remplacer exactement (fin du bloc `try` et `catch`) :

```csharp
            CanPlay = blockingChecks.All(c => c.Status == CheckStatus.Passed);
            var firstFailedBlockingCheck = blockingChecks.FirstOrDefault(c => c.Status == CheckStatus.Failed);
            StatusMessage = firstFailedBlockingCheck is not null
                ? "Action requise — " + firstFailedBlockingCheck.Message
                : workshopResult.Status == CheckStatus.Failed
                    ? "Prêt à jouer — Project Zomboid téléchargera les mods Workshop manquants en rejoignant le serveur"
                    : "Prêt à jouer — toutes les vérifications sont validées";
        }
        catch (Exception ex)
        {
            CanPlay = false;
            StatusMessage = "Erreur lors de la vérification : " + ex.Message;
        }
```

par :

```csharp
            CanPlay = blockingChecks.All(c => c.Status == CheckStatus.Passed);
            var firstFailedBlockingCheck = blockingChecks.FirstOrDefault(c => c.Status == CheckStatus.Failed);
            StatusMessage = firstFailedBlockingCheck is not null
                ? "Action requise — " + firstFailedBlockingCheck.Message
                : workshopResult.Status == CheckStatus.Failed
                    ? "Prêt à jouer — Project Zomboid téléchargera les mods Workshop manquants en rejoignant le serveur"
                    : "Prêt à jouer — toutes les vérifications sont validées";
            _logger.Info($"Vérifications terminées — CanPlay={CanPlay}, échecs={Checks.Count(c => c.Status == CheckStatus.Failed)}.");
        }
        catch (Exception ex)
        {
            _logger.Error("Erreur lors de la vérification du Dashboard", ex);
            CanPlay = false;
            StatusMessage = "Erreur lors de la vérification : " + ex.Message;
        }
```

- [ ] **Step 2: `MainWindowViewModel` — injecter le logger, le transmettre aux deux modales**

Ajouter le champ et le paramètre de constructeur :

```csharp
    private readonly ILauncherLogger _logger;

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        SettingsViewModel settings,
        NewsViewModel news,
        FirstRunViewModel firstRun,
        IJavaModService javaModService,
        IUpdateService updateService,
        ILauncherLogger logger)
    {
        _dashboard = dashboard;
        _firstRun = firstRun;
        _javaModService = javaModService;
        _updateService = updateService;
        _logger = logger;
        _currentPage = dashboard;
```

(le reste du constructeur — abonnements aux événements — inchangé.)

Dans `CheckForUpdatesAsync()`, remplacer :

```csharp
        var modal = new UpdateModalViewModel(_updateService, updateInfo);
        modal.Completed += () => CurrentModal = null;
        CurrentModal = modal;
```

par :

```csharp
        _logger.Info($"Mise à jour disponible : {updateInfo.CurrentVersion} → {updateInfo.LatestVersion}");
        var modal = new UpdateModalViewModel(_updateService, updateInfo, _logger);
        modal.Completed += () => CurrentModal = null;
        CurrentModal = modal;
```

Dans `OnRepairRequested()`, remplacer :

```csharp
        var modal = new RepairModalViewModel(_javaModService);
```

par :

```csharp
        _logger.Info("Réparation du mod Java demandée.");
        var modal = new RepairModalViewModel(_javaModService, _logger);
```

(le reste de `OnRepairRequested` — abonnement à `Completed`, `_ = modal.RunRepairAsync();` — inchangé.)

- [ ] **Step 3: `RepairModalViewModel` — logger succès/échec**

Ajouter le champ et le paramètre de constructeur :

```csharp
    private readonly IJavaModService _javaModService;
    private readonly ILauncherLogger _logger;

    public event Action? Completed;

    public RepairModalViewModel(IJavaModService javaModService, ILauncherLogger logger)
    {
        _javaModService = javaModService;
        _logger = logger;
        Steps = new ObservableCollection<RepairStepViewModel>();
        foreach (var name in StepOrder)
        {
            Steps.Add(new RepairStepViewModel(name));
        }
    }
```

Dans `RunRepairAsync()`, remplacer :

```csharp
            await Task.Delay(400);
            Completed?.Invoke();
        }
        catch (Exception ex)
        {
            foreach (var step in Steps)
            {
                if (step.State == FirstRunStepState.InProgress)
                {
                    step.State = FirstRunStepState.Pending;
                }
            }

            HasError = true;
            StatusMessage = "Erreur lors de la réparation : " + ex.Message;
        }
```

par :

```csharp
            _logger.Info("Réparation du mod Java terminée avec succès.");
            await Task.Delay(400);
            Completed?.Invoke();
        }
        catch (Exception ex)
        {
            foreach (var step in Steps)
            {
                if (step.State == FirstRunStepState.InProgress)
                {
                    step.State = FirstRunStepState.Pending;
                }
            }

            _logger.Error("Échec de la réparation du mod Java", ex);
            HasError = true;
            StatusMessage = "Erreur lors de la réparation : " + ex.Message;
        }
```

- [ ] **Step 4: `UpdateModalViewModel` — logger tentative/échec**

Ajouter le champ et le paramètre de constructeur :

```csharp
    private readonly IUpdateService _updateService;
    private readonly ILauncherLogger _logger;

    public event Action? Completed;

    public UpdateModalViewModel(IUpdateService updateService, UpdateInfo updateInfo, ILauncherLogger logger)
    {
        _updateService = updateService;
        _logger = logger;
        UpdateInfo = updateInfo;
    }
```

Dans `ApplyAsync()`, remplacer :

```csharp
        StatusMessage = "Téléchargement en cours… le launcher va redémarrer automatiquement.";
        IsStatusSuccess = true;
        var succeeded = false;

        try
        {
            await _updateService.ApplyUpdateAsync();
            // Reached only when ApplyUpdateAsync returns normally instead of restarting
            // the process — i.e. FakeUpdateService in dev. The real Velopack path exits
            // during the call above and never comes back here.
            StatusMessage = "Mise à jour installée — redémarrez le launcher pour l'appliquer.";
            succeeded = true;
            await Task.Delay(1500);
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur lors de la mise à jour : " + ex.Message;
            IsStatusSuccess = false;
        }
```

par :

```csharp
        StatusMessage = "Téléchargement en cours… le launcher va redémarrer automatiquement.";
        IsStatusSuccess = true;
        var succeeded = false;
        _logger.Info($"Application de la mise à jour {UpdateInfo.LatestVersion}…");

        try
        {
            await _updateService.ApplyUpdateAsync();
            // Reached only when ApplyUpdateAsync returns normally instead of restarting
            // the process — i.e. FakeUpdateService in dev. The real Velopack path exits
            // during the call above and never comes back here.
            _logger.Info("Mise à jour appliquée (chemin de développement).");
            StatusMessage = "Mise à jour installée — redémarrez le launcher pour l'appliquer.";
            succeeded = true;
            await Task.Delay(1500);
        }
        catch (Exception ex)
        {
            _logger.Error("Échec de l'application de la mise à jour", ex);
            StatusMessage = "Erreur lors de la mise à jour : " + ex.Message;
            IsStatusSuccess = false;
        }
```

- [ ] **Step 5: Vérifier que tout compile**

Run: `dotnet build src/GlasLauncher.App -c Debug`
Expected: 0 erreur.

- [ ] **Step 6: Suite complète + commit**

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS (69/69, inchangé).

```bash
git add src/GlasLauncher.App/ViewModels/DashboardViewModel.cs src/GlasLauncher.App/ViewModels/MainWindowViewModel.cs src/GlasLauncher.App/ViewModels/RepairModalViewModel.cs src/GlasLauncher.App/ViewModels/UpdateModalViewModel.cs
git commit -m "feat(app): log key launcher actions (checks, repair, update)"
```

---

## Vérification finale (faite par l'orchestrateur, pas un implémenteur de tâche)

Après la Tâche 4 : build + lancement réel de `GlasLauncher.App.exe` sur la VM, clic sur "Générer un rapport de diagnostic" dans Paramètres, vérifier :
- Le `.zip` apparaît sur le Bureau avec le nom horodaté attendu.
- L'Explorateur s'ouvre dessus, sélectionné.
- Le zip contient `manifest.txt` (contenu cohérent avec les vraies données de la VM), `launcher.log` (contient au moins la ligne du `RefreshAsync` du Dashboard), et `projectzomboid-logs/` si `%UserProfile%\Zomboid\Logs\` existe.
- `VersionInfoText` en haut de Paramètres affiche le vrai buildid et la vraie version du mod Java (pas "41.78.16"/"v1.0.0" en dur).
