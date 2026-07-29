# Mod Java / agent — Fondations — Design

**Statut :** approuvé, prêt pour planification
**Portée :** deuxième des trois sous-projets "vrais services Windows" listés dans `docs/session-notes.md` — implémentation réelle d'`IJavaModService` (actuellement `FakeJavaModService`, et entièrement non consommé par l'UI aujourd'hui). Dépend du sous-projet #1 (Fondations Steam & VDF, terminé) pour la détection du dossier d'installation du jeu.

## Contexte

`IJavaModService` existe déjà (`GetStatusAsync()`, `RepairAsync(IProgress<RepairProgress>)`) et est déjà câblé à la modale de réparation (`RepairModalViewModel`, 4 étapes fixes : "Ancienne version supprimée" → "Téléchargement du mod Java" → "Vérification de l'intégrité (SHA-256)" → "Installation"). Mais `GetStatusAsync()`/`JavaModInfo` ne sont consommés nulle part dans l'UI actuelle — le check Dashboard "Mod Java à jour" reste un `Passed` codé en dur (même situation que les checks Steam avant leur câblage réel).

**Découverte importante en cours de brainstorming** : `GlasJavaMod` (le "mod Java propriétaire" du cahier des charges §4.2) est en réalité un **mod VOIP roleplay** (portée de voix ajustable chuchoter/parler/hurler), développé dans un dépôt séparé (`GlasVoipMod`) via patch bytecode de `zombie.core.raknet.VoiceManager`. Voir `docs/superpowers/specs/2026-07-29-voip-mod-recherche-technique-design.md` et `2026-07-29-voip-mod-spike-bytecode-patch-design.md`. Ce travail n'est pas terminé côté mod (le spike de faisabilité bytecode n'est pas encore validé) — le format exact du jar, son schéma de version et son hébergement ne sont pas encore fixés.

**Conséquence directe sur ce design** : plutôt que de construire une détection spécifique à `GlasJavaMod.jar` (qui devinerait une forme encore instable), le launcher détecte **une liste extensible de mods Java** décrite par un manifeste distant. Le launcher ne connaît aucun nom de mod en dur dans sa logique — il boucle sur ce que le manifeste liste. Ça découple entièrement ce sous-projet de l'avancement du mod VOIP, et couvre nativement l'ajout de futurs mods Java sans toucher au launcher.

Référence produit : `docs/cahier-des-charges.md` §4.2 (mécanisme de l'agent ZombieBuddy), §5.4 (vérifications de conformité), §6.4 (bouton Réparer).

## Architecture

### Modèles (`Models/`) — remplacent `JavaModInfo` existant

Sans risque de rupture : `JavaModInfo` n'est consommé nulle part actuellement (vérifié — seuls `IJavaModService` et `FakeJavaModService` y font référence).

```csharp
public record JavaFileEntry(string FileName, string Version, string Sha256, string DownloadUrl);

public record JavaModManifest(IReadOnlyList<JavaFileEntry> Files);

public record JavaFileStatus(string FileName, string? InstalledVersion, string RequiredVersion, bool IsUpToDate);

public record JavaModInfo(bool LaunchOptionConfigured, IReadOnlyList<JavaFileStatus> Files);
```

`Files` couvre uniformément l'agent (`ZombieBuddy.jar`, `zbNative.dll`) et tout mod Java listé par le manifeste (le VOIP aujourd'hui, d'autres plus tard) — aucune distinction de traitement entre les deux catégories. `Version` est purement cosmétique ; le gate fonctionnel (`IsUpToDate`) se fait sur le SHA-256, même principe déjà établi pour `GameVersionRequirement.DisplayVersion` vs `buildid`.

Une liste `Files` vide sert de signal unifié "rien n'a pu être vérifié" (manifeste injoignable, dossier d'installation introuvable — peu importe la raison précise), pour éviter le piège du `All()` vacuously-true sur une liste vide.

### `ISteamEnvironment` étendu (2 nouvelles méthodes)

Toute interaction avec les fichiers/registre Steam reste encapsulée dans `SteamEnvironment` (principe déjà établi au sous-projet #1) — `JavaModService` ne touche jamais directement un chemin ou un fichier VDF Steam.

```csharp
Task<string?> GetGameInstallPathAsync();
Task<bool> IsJavaAgentLaunchOptionConfiguredAsync();
```

`SteamEnvironment` implémente les deux en réutilisant son cache `Lazy<SteamGameLocation?>` existant (pour `GetGameInstallPathAsync`) et en déléguant à `SteamLaunchOptionInspector` (nouveau, voir ci-dessous) pour la seconde. `FakeSteamEnvironment` reçoit des implémentations Fake correspondantes (chemin Fake existant, `true` pour l'option de lancement — cohérent avec son comportement "tout passe" par défaut).

### Nouveaux fichiers `Logic/` (purs, testables sans registre/réseau)

- **`SteamLaunchOptionInspector.cs`** — `IsLaunchOptionConfigured(string steamPath, string appId, string requiredOption)` : lit `<steamPath>/config/loginusers.vdf` pour trouver le compte Steam le plus récemment connecté (flag `MostRecent`), puis vérifie dans `<steamPath>/userdata/<SteamID>/config/localconfig.vdf` que l'option de lancement `-agentlib:zbNative --` est présente pour l'AppID 108600. Retour gracieux (`false`) si Steam absent, fichier illisible/corrompu, ou plusieurs comptes ambigus sans `MostRecent` exploitable.
- **`JavaFileInspector.cs`** — `GetFileStatuses(string installPath, JavaModManifest manifest) : IReadOnlyList<JavaFileStatus>` : pour chaque entrée du manifeste, vérifie la présence locale et calcule le SHA-256 pour déterminer `IsUpToDate`.
- **`JavaModEvaluator.cs`** — même patron que `GameVersionEvaluator`/`WorkshopEvaluator` : `Evaluate(JavaModInfo) : CheckResult`. `Failed` si `Files.Count == 0`, ou `!LaunchOptionConfigured`, ou au moins un fichier non à jour ; `Passed` sinon. Permet de brancher enfin le check Dashboard "Mod Java à jour" sur le vrai service.

### Nouveaux fichiers `Services/`

- **`JavaModManifestFetcher.cs`** — fetch HTTP réel du manifeste JSON (`System.Text.Json`), URL constante (placeholder, aucun hébergement réel en place — à mettre à jour une fois disponible, même statut que le reste de l'infra §8.3 du cahier des charges). Retourne `null` gracieusement sur toute erreur réseau/désérialisation. Constructeur prend un `HttpMessageHandler` injectable (testable sans réseau réel), factory statique pour le cas réel.
- **`JavaModService.cs`** (`IJavaModService`) — orchestrateur. `GetStatusAsync()` ne lève jamais (même contrat que `SteamEnvironment`). `RepairAsync()` peut lever (déjà géré par `RepairModalViewModel`).

## Flux de données

### `GetStatusAsync()`

1. `installPath = await _steamEnvironment.GetGameInstallPathAsync()` — si `null`, retourne `JavaModInfo(LaunchOptionConfigured: false, Files: [])` immédiatement.
2. `launchOptionConfigured = await _steamEnvironment.IsJavaAgentLaunchOptionConfiguredAsync()`.
3. `manifest = await _manifestFetcher.FetchAsync()` — si `null`, retourne `Files: []` (avec `LaunchOptionConfigured` déjà calculé à l'étape 2).
4. Sinon, `JavaFileInspector.GetFileStatuses(installPath, manifest)` → `Files`.

### `RepairAsync(IProgress<RepairProgress> progress)`

Réutilise les 4 étapes déjà câblées à l'UI (`RepairStepNames`), chacune bouclant sur l'ensemble des fichiers à traiter plutôt que sur un seul jar :

1. Récupère manifeste + dossier d'installation (échec → exception).
2. `JavaFileInspector` détermine quels fichiers sont manquants/obsolètes.
3. **"Ancienne version supprimée"** : supprime uniquement les fichiers obsolètes déjà présents localement (pas de suppression aveugle).
4. **"Téléchargement du mod Java"** : télécharge chaque fichier manquant/obsolète vers un chemin temporaire, Mo cumulés sur l'ensemble des téléchargements.
5. **"Vérification de l'intégrité"** : SHA-256 de chaque fichier téléchargé vs manifeste.
6. **"Installation"** : déplace chaque fichier vérifié vers le dossier du jeu (pattern fichier temporaire puis `File.Move`, déjà établi dans `FirstRunStore`).

**`RepairAsync` n'écrit jamais dans `localconfig.vdf`** — seule la lecture est autorisée (cohérent avec le choix déjà fait au sous-projet #1 : risque de corrompre un fichier partagé entre tous les jeux du compte Steam). Si l'option de lancement manque, le launcher continue d'afficher l'instruction à copier-coller manuellement ; `Repair` ne la configure jamais automatiquement.

## Gestion des erreurs

- `SteamLaunchOptionInspector`, `JavaFileInspector` : toute erreur de lecture/parsing encapsulée, retour gracieux (`false`/liste vide), jamais d'exception.
- `JavaModManifestFetcher` : toute erreur réseau/HTTP/désérialisation JSON → retourne `null`.
- `JavaModService.GetStatusAsync()` : ne lève jamais, hérite de la gracieuseté de ses dépendances.
- `JavaModService.RepairAsync()` : peut lever (action utilisateur explicite avec UI d'erreur dédiée déjà en place).

## DI (`App.axaml.cs`)

```csharp
services.AddSingleton<IJavaModService>(sp =>
    OperatingSystem.IsWindows()
        ? new JavaModService(sp.GetRequiredService<ISteamEnvironment>(), JavaModManifestFetcher.CreateDefault())
        : new FakeJavaModService());
```

## Câblage Dashboard

`DashboardViewModel.RefreshAsync()` remplace le check "Mod Java à jour" codé en dur par un appel réel à `_javaModService.GetStatusAsync()` + `JavaModEvaluator.Evaluate(...)`, même pattern que les checks Steam déjà câblés.

## Tests

- `SteamLaunchOptionInspectorTests`, `JavaFileInspectorTests`, `JavaModEvaluatorTests` : purs, dossiers/fichiers temporaires fabriqués à la main, même pattern que `SteamLibraryLocatorTests`.
- `JavaModManifestFetcherTests` : `HttpMessageHandler` injectable (réponses JSON/erreurs simulées), aucun vrai appel réseau.
- Pas de test dédié pour `JavaModService` lui-même (orchestrateur + accès réseau/Windows) — vérification manuelle sur la VM Windows, même convention que `SteamEnvironment`.

## Hors-scope (explicitement)

- Contenu réel du mod VOIP (`GlasVoipMod`, patch bytecode, Lua) — dépôt et sous-projet séparés.
- Hébergement réel du manifeste JSON — URL placeholder pour l'instant, à mettre à jour une fois l'infra en place (probablement lié au sous-projet #3, Velopack & CI).
- Écriture automatique de `localconfig.vdf` — jamais, décision déjà actée au sous-projet #1.
- Génération réelle du rapport de diagnostic consommant `JavaModInfo` — reste simulée (`SettingsViewModel`), hors-scope ici.
- Détection de compte Steam multi-utilisateurs au-delà du "plus récent" (`loginusers.vdf`) — dégrade proprement (`false`) plutôt que de deviner en cas d'ambiguïté réelle.
