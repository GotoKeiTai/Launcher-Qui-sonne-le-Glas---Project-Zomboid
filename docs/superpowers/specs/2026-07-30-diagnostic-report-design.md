# Rapport de diagnostic réel — Design

**Statut :** approuvé, prêt pour planification
**Portée :** rendre réel le bouton "Générer un rapport de diagnostic" (`SettingsViewModel.GenerateDiagnosticReport`), actuellement une simulation pure (`StatusMessage = "Rapport généré (simulation)."`, aucun fichier produit). La spec fonctionnelle existe déjà : `docs/cahier-des-charges.md` §7.1. Corrige aussi `VersionInfoText` (même écran), qui contient encore des littéraux figés du même genre que ceux déjà corrigés dans le pied de page du Dashboard.

## Contexte

`docs/cahier-des-charges.md` §7.1 décrit le contenu attendu du rapport (zip prêt à partager sur Discord) :

1. Les logs du launcher (session en cours).
2. Les logs Project Zomboid récents, depuis `%UserProfile%\Zomboid\Logs\`.
3. Un manifeste texte généré à la volée (version du launcher, buildid/branche détectée, version+hash du mod Java, mods Workshop requis vs détectés, version Windows).

Deux constats faits en explorant le code existant, qui élargissent légèrement le périmètre :

- **Aucune infrastructure de logs n'existe** dans le launcher — `OpenLauncherLogsCommand` ouvre déjà `%AppData%\GlasLauncher\logs`, mais rien n'y écrit jamais. Le point (1) de la spec est donc vide tant qu'on n'ajoute pas une vraie journalisation.
- **`SettingsViewModel.VersionInfoText`** (affiché en haut de Paramètres, copié via le bouton "Copier") contient encore `"Project Zomboid 41.78.16 · Mod Java v1.0.0"` en dur — même catégorie de bug que celui déjà corrigé dans le pied de page du Dashboard (`GameVersionText`/`JavaModVersionText`), mais oublié à l'époque car fichier différent.

Décidé pendant ce brainstorming :

- **Ajouter une vraie journalisation** (fichier par session, actions clés + erreurs) pour donner un contenu réel au point (1).
- **Données du manifeste récupérées à la volée**, au moment du clic sur "Générer" (nouvel appel aux services, indépendant de l'état déjà affiché sur le Dashboard) — garantit un instantané à jour.
- **Emplacement du zip : le Bureau**, nom horodaté, Explorateur Windows ouvert dessus ensuite (`explorer.exe /select,<chemin>`) — pas de boîte de dialogue "Enregistrer sous".
- **`VersionInfoText` corrigé dans la foulée** — les mêmes données réelles (buildid, version du mod Java) sont de toute façon calculées pour le rapport.

## Architecture

### `ILauncherLogger` (`Core/Services/`) — pas de split Real/Fake

```csharp
public interface ILauncherLogger
{
    string? CurrentLogFilePath { get; }
    void Info(string message);
    void Error(string message, Exception? exception = null);
}
```

Implémentation unique `FileLauncherLogger` (`Core/Services/`), **enregistrée directement sans branchement `OperatingSystem.IsWindows()`** — contrairement à `SteamEnvironment`/`JavaModService`/`VelopackUpdateService`, cette classe n'a aucune dépendance Windows (juste `File`/`Directory`, portables), donc pas besoin d'un Fake pour permettre le développement sur macOS. Écrit dans `%AppData%\GlasLauncher\logs\session-yyyy-MM-dd-HHmmss.log` (un fichier par lancement de l'app), format `[yyyy-MM-dd HH:mm:ss] NIVEAU message`. Jamais d'exception propagée (écriture protégée par `try/catch`, dégrade silencieusement — cohérent avec la convention "jamais de throw" déjà établie) :

```csharp
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
        if (_logFilePath is null) return;
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

**Points d'appel** (App, injecté dans les ViewModels concernés) :
- `DashboardViewModel.RefreshAsync()` : `Info` au résultat (`CanPlay`, nombre de checks échoués) ; `Error` dans le `catch` existant.
- `MainWindowViewModel.OnRepairRequested` / `RepairModalViewModel` : `Info` au lancement et à la fin de la réparation ; `Error` si `RepairAsync` lève.
- `MainWindowViewModel.CheckForUpdatesAsync` / `UpdateModalViewModel.ApplyAsync` : `Info` quand une mise à jour est détectée/appliquée ; `Error` sur échec.

Pas de journalisation ajoutée à l'intérieur des services Core (`JavaModService`, `SteamEnvironment`, etc.) — ils gardent leur dégradation silencieuse actuelle vers des valeurs sûres ; les enrichir de logs est un chantier séparé, hors périmètre ici.

### `WorkshopRequirement` (`Core/Logic/`) — évite une deuxième copie des IDs en dur

`DashboardViewModel.RefreshAsync` a déjà `new[] { "111", "222", "333" }` / `"3719763771"` codés en dur inline. Le rapport de diagnostic a besoin exactement des mêmes valeurs pour calculer "Workshop requis vs détectés" ; plutôt que de les dupliquer une deuxième fois, extraction minimale :

```csharp
namespace GlasLauncher.Core.Logic;

public static class WorkshopRequirement
{
    public static readonly IReadOnlyList<string> RequiredIds = new[] { "111", "222", "333" };
    public const string CollectionId = "3719763771";
}
```

`DashboardViewModel` et le nouveau `DiagnosticReportService` référencent tous les deux `WorkshopRequirement.RequiredIds`/`.CollectionId`.

### Modèles (`Core/Models/`)

```csharp
public record DiagnosticSnapshot(
    string LauncherVersion,
    string WindowsDescription,
    GameVersionInfo? DetectedGameVersion,
    GameVersionRequirement RequiredGameVersion,
    JavaModInfo JavaModInfo,
    IReadOnlyList<JavaModFileHash> JavaModFileHashes,
    WorkshopStatus WorkshopStatus,
    DateTime GeneratedAtLocal);

public record JavaModFileHash(string FileName, string? Sha256);
```

`JavaModFileHash.Sha256` est calculé directement depuis le fichier installé (pas de changement à `JavaFileStatus`, qui n'a pas ce champ) — `null` si le fichier est absent ou illisible.

### `DiagnosticManifestBuilder` (`Core/Logic/`) — pur, testable

```csharp
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

### `IDiagnosticReportService` (`Core/Services/`) — pas de split Real/Fake, même raisonnement que `ILauncherLogger`

```csharp
public interface IDiagnosticReportService
{
    Task<string> GenerateAsync();
}
```

Implémentation unique `DiagnosticReportService`, construite avec `ISteamEnvironment`, `IServerInfoService`, `IJavaModService`, `IUpdateService`, `ILauncherLogger` (toutes déjà enregistrées dans `App.axaml.cs`, déjà Real/Fake-branchées elles-mêmes — le rapport hérite automatiquement du bon comportement selon l'OS sans avoir à re-brancher).

`GenerateAsync()` :

1. Récupère en parallèle logique (séquentiel, pas besoin de `Task.WhenAll` pour un enchaînement aussi court) : version launcher, version détectée, exigence serveur, statut mod Java, chemin d'installation, statut Workshop (`WorkshopRequirement.RequiredIds`/`.CollectionId`).
2. Pour chaque fichier de `JavaModInfo.Files`, calcule le SHA-256 du fichier réel sous `installPath` (méthode privée, `try/catch` → `null` si échec/absent — même logique que la vérification existante dans `JavaFileInspector`, dupliquée ici volontairement : trop petite et trop spécifique à ce contexte pour justifier un partage).
3. Construit le `DiagnosticSnapshot`, appelle `DiagnosticManifestBuilder.Build(...)`.
4. Crée `%UserProfile%\Desktop\GlasLauncher-diagnostic-yyyy-MM-dd-HHmm.zip` via `System.IO.Compression.ZipArchive` (`ZipArchiveMode.Create`) :
   - `manifest.txt` — le texte généré à l'étape 3.
   - `launcher.log` — copie de `logger.CurrentLogFilePath`, si non `null` et le fichier existe.
   - `projectzomboid-logs/<nom-du-fichier>` — chaque fichier de `%UserProfile%\Zomboid\Logs\` dont `LastWriteTimeUtc >= DateTime.UtcNow.AddDays(-3)`, si le dossier existe (sinon section simplement absente du zip, pas d'erreur — même philosophie que `OpenPzLogsCommand` actuel qui affiche déjà un message honnête si le dossier n'existe pas).
5. `logger.Info(...)` sur succès, `logger.Error(...)` + relance d'une `InvalidOperationException` au message clair sur échec (le `catch` de la méthode ne doit pas avaler l'erreur : `SettingsViewModel` a besoin de savoir que ça a échoué pour afficher `StatusMessage`).
6. Retourne le chemin complet du zip créé.

### `SettingsViewModel` — changements

Constructeur gagne trois paramètres : `ISteamEnvironment`, `IJavaModService`, `IDiagnosticReportService` (tous déjà enregistrés dans le conteneur DI).

`VersionInfoText` devient `[ObservableProperty]` (au lieu d'un getter calculé figé), initialisé à une valeur de repli le temps du premier chargement, puis rafraîchi une fois via un appel fire-and-forget dans le constructeur (`_ = RefreshVersionInfoAsync();`, même style que `DashboardViewModel`) :

```csharp
[ObservableProperty]
private string _versionInfoText;

// dans le constructeur :
_versionInfoText = $"Launcher {_updateService.GetCurrentVersion()} · Chargement…";
_ = RefreshVersionInfoAsync();

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
    VersionInfoText = $"Launcher {_updateService.GetCurrentVersion()} · Project Zomboid {detectedVersion?.BuildId ?? "introuvable"} · Mod Java {javaModVersionText}";
}
```

`GenerateDiagnosticReportCommand` devient asynchrone :

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

(La liaison XAML existante `Command="{Binding GenerateDiagnosticReportCommand}"` n'a pas besoin de changer — `[RelayCommand]` génère la même commande que la méthode soit `void` ou `async Task`.)

### Enregistrement DI (`App.axaml.cs`)

```csharp
services.AddSingleton<ILauncherLogger>(_ => new FileLauncherLogger());
services.AddSingleton<IDiagnosticReportService, DiagnosticReportService>();
```

Ajoutés sans branchement `OperatingSystem.IsWindows()` (voir justification ci-dessus), à la suite des autres `AddSingleton` de services.

## Flux de données

`SettingsViewModel` → clic "Générer" → `IDiagnosticReportService.GenerateAsync()` → interroge `ISteamEnvironment`/`IServerInfoService`/`IJavaModService`/`IUpdateService` en direct (pas l'état du Dashboard) → `DiagnosticManifestBuilder.Build(snapshot)` (pur) → zip écrit sur le Bureau → chemin retourné → `SettingsViewModel` ouvre l'Explorateur dessus.

## Gestion des erreurs

- `FileLauncherLogger` : jamais d'exception propagée (écriture best-effort).
- `DiagnosticReportService.GenerateAsync()` : toute exception est journalisée puis relancée avec un message clair ; `SettingsViewModel` l'attrape et affiche `StatusMessage`/`IsStatusSuccess = false` (pattern déjà utilisé pour `OpenLauncherLogsCommand`/`OpenPzLogsCommand`).
- Dossier `Zomboid/Logs` absent : section simplement omise du zip, pas d'erreur.
- Fichier de log launcher illisible/absent : entrée `launcher.log` simplement omise du zip.

## Tests

- `DiagnosticManifestBuilderTests` (nouveau) : construit un `DiagnosticSnapshot` à la main, vérifie le texte produit — cas nominal (tout à jour, tout détecté), buildid non détecté (`null`), mod Java non installé, mods Workshop manquants.
- `WorkshopRequirement` : constantes, pas de test dédié nécessaire.
- Pas de test dédié pour `FileLauncherLogger`/`DiagnosticReportService` (I/O disque/zip réel) — même convention déjà établie pour `SteamEnvironment`/`JavaModService`.

## Hors-scope (explicitement)

- Journalisation à l'intérieur des services Core (`JavaModService`, `SteamEnvironment`, etc.) — reste un chantier séparé.
- Rotation/purge des anciens fichiers de logs launcher (`%AppData%\GlasLauncher\logs`) — accumulation illimitée acceptée pour cette itération (YAGNI ; à revisiter si ça devient un problème réel).
- Boîte de dialogue "Enregistrer sous" pour le zip — toujours le Bureau.
- Extraction de `WorkshopRequirement` vers `IServerInfoService` (ce qui serait plus "correct" architecturalement, ces IDs représentant une exigence serveur) — resterait un littéral codé en dur de toute façon tant que `FakeServerInfoService` est la seule implémentation ; pas justifié ici.
