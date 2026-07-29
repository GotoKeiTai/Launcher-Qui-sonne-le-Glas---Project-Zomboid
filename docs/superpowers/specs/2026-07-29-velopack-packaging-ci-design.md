# Packaging Velopack & CI — Design

**Statut :** approuvé, prêt pour planification
**Portée :** troisième des trois sous-projets "vrais services Windows/infra" listés dans `docs/session-notes.md` — packaging de l'app via Velopack, pipeline CI/CD GitHub Actions, et vraie implémentation d'`IUpdateService` (actuellement `FakeUpdateService`). Contrairement aux deux sous-projets précédents, celui-ci ne dépend d'aucun autre — c'est de l'infrastructure de build/release, indépendante des services runtime déjà réels.

## Contexte

Référence produit : `docs/cahier-des-charges.md` §8.2 (distribution & mise à jour), §8.3 (hébergement sans VPS), §8.4 (sécurité de la chaîne de mise à jour), §9 (CI).

Déjà décidé par le cahier des charges (repris tel quel ici, pas rediscuté) :

- **Velopack** (successeur de Squirrel.Windows) pour packaging + auto-update, support natif GitHub Releases.
- Installeur `GlasLauncher-win-Setup.exe`, installation dans `%LocalAppData%` sans élévation UAC. Pas de MSI ni MSIX.
- Distribution via URL stable GitHub Releases, épinglée sur le Discord du serveur.
- **Pas de signature de code en phase bêta** — hash + HTTPS uniquement, avertissement SmartScreen documenté pour les testeurs. SignPath.io (gratuit, open source) prévu à l'ouverture publique, hors-scope ici.
- Pipeline GitHub Actions, runners `windows-latest`.

Décidé pendant ce brainstorming :

- Le vrai `IUpdateService` (basé sur l'API `UpdateManager` de Velopack) fait partie de ce même chantier plutôt que d'être reporté — il est trop couplé techniquement au packaging pour les séparer utilement.
- Le dépôt GitHub, actuellement **privé**, doit passer **public** pour que le lien GitHub Releases soit utilisable sans authentification (prérequis pour le lien stable épinglé sur Discord). Étape manuelle, proposée explicitement au moment de l'implémentation plutôt que faite en passant.
- Déclenchement d'une release : **tag Git** (`vX.Y.Z`) poussé manuellement — pas de release automatique à chaque push sur `main`, pas de déclenchement manuel via formulaire GitHub Actions.

## API Velopack vérifiée (docs.velopack.io, juillet 2026)

Pas de supposition ici — signatures confirmées via la documentation officielle :

```csharp
// Hook de démarrage — Program.cs, tout premier appel de Main, avant tout le reste
VelopackApp.Build().Run();
```

```csharp
// Source de mise à jour : dépôt GitHub public, pas de token (public), pas de pre-release
var source = new GithubSource(RepoUrl, accessToken: null, prerelease: false);
var manager = new UpdateManager(source);

// Vérification — retourne null si aucune mise à jour (jamais d'exception documentée pour ce cas,
// mais une panne réseau peut lever — à encapsuler comme le reste du projet)
Velopack.UpdateInfo? info = await manager.CheckForUpdatesAsync();

// info.TargetFullRelease : VelopackAsset — Version (SemanticVersion), NotesMarkdown (string),
// NotesHTML (string), FileName, SHA256, Size.
// manager.CurrentVersion : SemanticVersion? — version actuellement installée (null si non installé,
// ex. lancé directement en debug depuis l'IDE plutôt que via l'installeur).

// Téléchargement + application (redémarre l'app dans la nouvelle version)
await manager.DownloadUpdatesAsync(info, progress: null, cancelToken: default);
manager.ApplyUpdatesAndRestart(info.TargetFullRelease, restartArgs: null);
```

CLI `vpk` (outil dotnet global, `dotnet tool install --global vpk`) :

```bash
# 1. Publier l'app en self-contained win-x64 (aucune dépendance .NET requise côté joueur)
dotnet publish src/GlasLauncher.App -c Release -r win-x64 --self-contained -o publish

# 2. Télécharger la release précédente pour permettre un delta (pas d'échec bloquant si aucune
# release n'existe encore — premier tag du projet)
vpk download github --repoUrl https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid \
  --token <GITHUB_TOKEN>

# 3. Empaqueter — packId "GlasLauncher" produit bien GlasLauncher-win-Setup.exe (convention Velopack :
# <packId>Setup.exe), icône réutilisée depuis Tâche 1 du sous-projet précédent
vpk pack --packId GlasLauncher --packVersion <version> --packDir publish \
  --mainExe GlasLauncher.App.exe --icon src/GlasLauncher.App/Assets/shield.ico \
  --releaseNotes release-notes.md

# 4. Publier sur GitHub Releases (--publish = publication immédiate, pas de brouillon ;
# --outputDir doit correspondre à celui produit par `vpk pack`, par défaut "Releases")
vpk upload github --repoUrl https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid \
  --token <GITHUB_TOKEN> --publish --outputDir Releases --releaseName "Glas Launcher <version>" --tag v<version>
```

## Architecture

### `VelopackUpdateService` (`Services/`) — remplace `FakeUpdateService`

```csharp
public class VelopackUpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
    private Velopack.UpdateInfo? _pendingUpdate;

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            _pendingUpdate = await _manager.CheckForUpdatesAsync();
        }
        catch (Exception)
        {
            return null; // même contrat que FakeUpdateService/JavaModManifestFetcher : jamais d'exception
        }

        if (_pendingUpdate is null)
        {
            return null;
        }

        return new UpdateInfo(
            CurrentVersion: _manager.CurrentVersion?.ToString() ?? "?",
            LatestVersion: _pendingUpdate.TargetFullRelease.Version.ToString(),
            ChangelogEntries: ParseNotesIntoEntries(_pendingUpdate.TargetFullRelease.NotesMarkdown));
    }

    public async Task ApplyUpdateAsync()
    {
        if (_pendingUpdate is null)
        {
            throw new InvalidOperationException("Aucune mise à jour en attente.");
        }

        await _manager.DownloadUpdatesAsync(_pendingUpdate);
        _manager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
    }

    private static IReadOnlyList<string> ParseNotesIntoEntries(string notesMarkdown) =>
        notesMarkdown
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('-', '*', ' '))
            .Where(line => line.Length > 0)
            .ToList();
}
```

`UpdateModalViewModel`/`UpdateModalView.axaml` ne changent pas : `ChangelogEntries` reste une `IReadOnlyList<string>` rendue en liste à puces, seule la source change. Ça implique que les notes de release doivent être écrites en liste markdown (une puce par ligne, `- ...`) au moment du tag — voir "Flux de données" ci-dessous.

Comme `_pendingUpdate` est un état mutable tenant entre l'appel `CheckForUpdateAsync()` et `ApplyUpdateAsync()`, `VelopackUpdateService` doit être enregistré `AddSingleton` (déjà la convention DI pour tout ce qui a un état de session — cohérent avec `DashboardViewModel` etc.), jamais recréé entre les deux appels.

### DI (`App.axaml.cs`)

Même pattern que `ISteamEnvironment`/`IJavaModService` — réel sur Windows, fake ailleurs (macOS reste utile en dev pour builder/tester l'UI sans VM) :

```csharp
services.AddSingleton<IUpdateService>(sp =>
    OperatingSystem.IsWindows()
        ? new VelopackUpdateService()
        : new FakeUpdateService());
```

### `Program.cs` — hook Velopack

```csharp
public static void Main(string[] args)
{
    VelopackApp.Build().Run(); // tout premier appel, avant AppBuilder/DI/tout le reste
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}
```

### Affichage de version réelle (nettoyage lié)

`SettingsViewModel.cs:110` et le pied de page de `DashboardView.axaml:29` affichent aujourd'hui un `"v0.1.0"` codé en dur. Une fois `VelopackUpdateService` en place, ces deux endroits doivent utiliser la vraie version installée (`UpdateManager.CurrentVersion?.ToString() ?? "dev"` côté Windows réel, valeur fixe raisonnable côté Fake) plutôt qu'un texte figé — conséquence directe de l'introduction d'une vraie source de vérité pour la version, pas un chantier séparé.

### Workflows GitHub Actions (`.github/workflows/`)

**`ci.yml`** — build + tests à chaque push sur `main` et chaque pull request :

```yaml
name: CI
on:
  push:
    branches: [main]
  pull_request:
jobs:
  build-and-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build
      - run: dotnet test tests/GlasLauncher.Core.Tests
```

**`release.yml`** — déclenché par un tag `v*.*.*` :

```yaml
name: Release
on:
  push:
    tags: ['v*.*.*']
permissions:
  contents: write   # requis pour que le GITHUB_TOKEN intégré puisse créer une Release
jobs:
  package-and-release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }   # historique complet requis pour lire le message du tag annoté
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet test tests/GlasLauncher.Core.Tests
      - run: dotnet publish src/GlasLauncher.App -c Release -r win-x64 --self-contained -o publish
      - run: dotnet tool install --global vpk
      - name: Extraire version et notes depuis le tag
        run: |
          $version = "${{ github.ref_name }}".TrimStart('v')
          "VERSION=$version" >> $env:GITHUB_ENV
          git tag -l --format='%(contents)' ${{ github.ref_name }} > release-notes.md
      - name: Télécharger la release précédente (requis pour générer les deltas de mise à jour)
        run: vpk download github --repoUrl https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid --token ${{ secrets.GITHUB_TOKEN }}
        continue-on-error: true   # rien à télécharger lors de la toute première release — comportement attendu
      - run: vpk pack --packId GlasLauncher --packVersion $env:VERSION --packDir publish --mainExe GlasLauncher.App.exe --icon src/GlasLauncher.App/Assets/shield.ico --releaseNotes release-notes.md
      - run: vpk upload github --repoUrl https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid --token ${{ secrets.GITHUB_TOKEN }} --publish --outputDir Releases --releaseName "Glas Launcher $env:VERSION" --tag ${{ github.ref_name }}
```

`vpk download` récupère la release précédente pour que `vpk pack` puisse générer une mise à jour **delta** (le joueur ne retélécharge que ce qui a changé) plutôt qu'un paquet complet à chaque fois — omis par erreur dans la première version de cette spec, corrigé après vérification de l'exemple officiel GitHub Actions de Velopack (`docs.velopack.io/distributing/github-actions`). Sans release précédente (premier tag du projet), l'étape échoue silencieusement (`continue-on-error`) et `vpk pack` produit simplement un paquet complet, sans delta — comportement attendu, pas une erreur à corriger.

Le message du tag annoté sert directement de notes de release — une seule action déclenche la release **et** rédige son changelog, plutôt que de maintenir un fichier séparé. C'est aussi directement ce que `VelopackUpdateService.ParseNotesIntoEntries` consomme côté client. Sur PowerShell (shell de dev par défaut), `\n` dans une chaîne entre guillemets n'est **pas** interprété comme un saut de ligne (contrairement à bash) — utiliser plusieurs flags `-m`, chacun devenant une ligne du message :

```powershell
git tag -a v0.2.0 -m "- Correction du crash au lancement" -m "- Amélioration de la détection Steam"
git push --tags
```

## Flux de données

**Publier une version :**

1. Poser un tag annoté multi-lignes (voir commande PowerShell ci-dessus) et le pousser.
2. `release.yml` se déclenche : tests → publish self-contained → `vpk pack` → `vpk upload github --publish`.
3. La Release GitHub apparaît avec `GlasLauncher-win-Setup.exe` en asset ; le lien stable (`.../releases/latest/download/GlasLauncher-win-Setup.exe`) pointe automatiquement dessus.

**Vérifier/appliquer côté joueur :**

1. `MainWindowViewModel.CheckForUpdatesAsync()` (déjà en place) appelle `IUpdateService.CheckForUpdateAsync()`.
2. `VelopackUpdateService` interroge `GithubSource` → si mise à jour dispo, retourne `UpdateInfo` avec les notes parsées.
3. Modale existante (`UpdateModalViewModel`) → confirmation joueur → `ApplyUpdateAsync()` télécharge et redémarre dans la nouvelle version.
4. Pas de connexion / pas de mise à jour / dépôt injoignable → `null` silencieux, aucune interruption du flux normal.

## Gestion des erreurs

- `VelopackUpdateService.CheckForUpdateAsync()` : jamais d'exception (try/catch autour de l'appel réseau Velopack, même contrat que tous les autres services `GetStatusAsync`-like du projet).
- `VelopackUpdateService.ApplyUpdateAsync()` : peut lever (téléchargement échoué, `_pendingUpdate` null si appelé hors séquence) — `UpdateModalViewModel` a déjà l'UI d'erreur pour ce cas, aucun changement nécessaire côté UI.
- Intégrité des paquets : gérée nativement par Velopack (hashes SHA256 intégrés au format de delta, cf. `VelopackAsset.SHA256`) — pas de vérification manuelle à réimplémenter, contrairement au mod Java.
- `ci.yml`/`release.yml` : un échec de `dotnet test` bloque `release.yml` avant tout packaging — aucune release ne peut sortir avec une suite de tests rouge.

## Tests

- Pas de test unitaire dédié pour `VelopackUpdateService` (orchestration réseau/Windows, dépendance à un vrai dépôt GitHub) — même convention que `SteamEnvironment`/`JavaModService`.
- `ci.yml` constitue le filet de sécurité automatisé pour toute la base de code existante (`dotnet test tests/GlasLauncher.Core.Tests`, inchangé).
- Vérification manuelle sur la VM Windows, en 4 étapes :
  1. Tag `v0.1.0` → confirmer que `release.yml` produit une Release avec `GlasLauncher-win-Setup.exe`.
  2. Installer via cet exe → confirmer l'installation dans `%LocalAppData%` sans élévation, raccourci Menu Démarrer.
  3. Tag `v0.1.1` → confirmer que l'app déjà installée détecte la mise à jour, l'applique, redémarre correctement dans la nouvelle version.
  4. Confirmer l'avertissement SmartScreen attendu (pas de signature) et le documenter pour les testeurs bêta.

## Étape manuelle préalable (hors code)

Rendre le dépôt GitHub public avant le premier tag — sera proposée explicitement au moment de l'implémentation, pas faite en passant dans un commit.

## Hors-scope (explicitement)

- Signature de code (SignPath.io) — prévue à l'ouverture publique, pas en bêta (§8.4 du cahier des charges).
- Hébergement séparé pour `server.json`/`version.json`/manifeste du mod Java (Cloudflare Pages/R2 ou GitHub raw, §8.3) — sujet du futur sous-projet #4 (`IServerInfoService` réel), pas de recoupement technique avec Velopack au-delà du même dépôt GitHub public.
- macOS/Linux packaging — l'app cible exclusivement Windows (§9 du cahier des charges) ; le Fake reste la seule voie non-Windows, pour le développement UI uniquement.
- Rollback/versions multiples installées simultanément — Velopack gère nativement le remplacement de version, aucune logique custom nécessaire.
