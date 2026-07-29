# Fondations Steam & VDF — Design

**Statut :** approuvé, prêt pour planification
**Portée :** premier des trois sous-projets "vrais services Windows" listés dans `docs/session-notes.md` — implémentation réelle d'`ISteamEnvironment` (actuellement `FakeSteamEnvironment` uniquement). Les deux autres sous-projets (mod Java / agent, packaging Velopack & CI) restent hors-périmètre et dépendent de celui-ci.

## Contexte

`ISteamEnvironment` existe déjà (`src/GlasLauncher.Core/Services/ISteamEnvironment.cs`) avec cinq méthodes, toutes servies aujourd'hui par `FakeSteamEnvironment`. `DashboardViewModel` les consomme déjà (`GetInstalledGameVersionAsync`, `GetWorkshopStatusAsync`, `LaunchGameAsync`) via l'interface — aucun changement de contrat côté consommateur n'est nécessaire, seule l'implémentation change.

Point notable repéré dans le code existant : `MainWindowViewModel` dépend aujourd'hui du concret `FakeSteamEnvironment` (pas de l'interface) pour un bouton dev (`ToggleWorkshopScenarioAsync`) qui bascule `SimulateWorkshopMissing` afin de prévisualiser l'état "bloqué" du Dashboard. Ce toggle est supprimé dans le cadre de ce sous-projet (voir section DI).

Référence produit complète : `docs/cahier-des-charges.md` §5.1 (Steam), §5.2 (installation du jeu, bibliothèques multiples), §5.3 (version du jeu), §4.1 (Workshop).

## Architecture

### Nouveaux fichiers

- `src/GlasLauncher.Core/Logic/SteamLibraryLocator.cs` — logique pure et statique, testable sur toute plateforme (aucune dépendance registre/process). Étant donné un `steamPath` (racine d'installation Steam) et l'AppId fixe du jeu (`108600`, constante privée dans cette classe), parse `steamapps/libraryfolders.vdf` pour lister les bibliothèques enregistrées, puis cherche `steamapps/appmanifest_108600.acf` dans chacune (premier match retenu). Retourne un record `SteamGameLocation(string LibraryPath, string InstallPath, string BuildId, string Branch)?` — `null` si Steam ou le jeu ne sont pas trouvés. `Branch` vaut `UserConfig.BetaKey` si présent dans l'ACF, sinon `"public"`.
- `src/GlasLauncher.Core/Services/SteamEnvironment.cs` — implémentation réelle d'`ISteamEnvironment`.
  - Constructeur `SteamEnvironment(string? steamPath)` — même pattern d'injection de chemin que `FirstRunStore(string filePath)`, pour rester testable sans toucher au vrai registre.
  - Factory statique `SteamEnvironment.CreateForCurrentUser()` — lit `HKEY_CURRENT_USER\Software\Valve\Steam\SteamPath` via `Microsoft.Win32.Registry` ; retourne un `steamPath` `null` si la clé est absente, puis construit l'instance. Cette factory n'est appelée que sous Windows (voir DI) ; elle n'a pas besoin de gérer elle-même le cas hors-Windows.
  - Met en cache (lazy, une fois par instance) le résultat de `SteamLibraryLocator.Locate(steamPath)` — réutilisé par `GetInstalledGameVersionAsync` et `GetWorkshopStatusAsync` pour éviter de reparser `libraryfolders.vdf` deux fois par cycle `RefreshAsync`. Le jeu ne change pas de bibliothèque pendant qu'une instance de l'app tourne, donc ce cache n'a pas besoin d'invalidation.
- `tests/GlasLauncher.Core.Tests/SteamLibraryLocatorTests.cs`

### Fichiers modifiés

- `src/GlasLauncher.Core/GlasLauncher.Core.csproj` — ajout du package NuGet `Gameloop.Vdf`.
- `src/GlasLauncher.App/App.axaml.cs` — voir section DI ci-dessous.
- `src/GlasLauncher.App/ViewModels/MainWindowViewModel.cs` — suppression du paramètre constructeur `FakeSteamEnvironment`, de la méthode `ToggleWorkshopScenarioAsync` et de la Command associée.
- Vue XAML exposant le bouton du toggle dev (à localiser en implémentation, probablement `MainWindow.axaml`) — suppression du bouton correspondant.

## Flux de données

1. **Résolution du chemin Steam** (`CreateForCurrentUser`, Windows uniquement) : lecture registre → `string? steamPath`.
2. **`IsSteamInstalledAsync()`** → `steamPath is not null && Directory.Exists(steamPath)`.
3. **`IsSteamRunningAsync()`** → `Process.GetProcessesByName("steam").Length > 0`. Indépendant de `steamPath` — reste vrai/faux même si `steamPath` est `null`.
4. **`GetInstalledGameVersionAsync()`** → si `steamPath` est `null`, retourne `null` immédiatement (pas d'appel au locator). Sinon, utilise le résultat mis en cache de `SteamLibraryLocator.Locate(steamPath)` : retourne `new GameVersionInfo(location.BuildId, location.Branch)` ou `null` si `location` est `null`.
5. **`GetWorkshopStatusAsync(requiredIds, collectionId)`** → réutilise le même résultat de locate mis en cache. Si `location` est `null` (jeu non trouvé), retourne `new WorkshopStatus(InstalledIds: [], requiredIds, collectionId)` — fail-closed, tous les mods requis apparaissent manquants plutôt que de faire planter le flux. Sinon, parse `<location.LibraryPath>/steamapps/workshop/appworkshop_108600.acf` et extrait les IDs d'objets Workshop installés.
6. **`LaunchGameAsync()`** → `Process.Start(new ProcessStartInfo("steam://run/108600") { UseShellExecute = true })`, fire-and-forget, retourne `Task.CompletedTask` après le `Process.Start` (même pattern que `OpenWorkshopSubscribeAsync`/`JoinDiscordCommand` déjà dans le code).

## Gestion des erreurs

Aucune exception ne doit remonter au consommateur (`DashboardViewModel`) pour un état "normal" (Steam non installé, jeu non installé, fichier corrompu) :

- `SteamLibraryLocator.Locate()` encapsule le parsing VDF/ACF dans un try/catch et retourne `null` sur toute erreur de lecture ou de désérialisation.
- Toute méthode d'`ISteamEnvironment` qui dépend d'un fichier absent/corrompu retourne son équivalent "négatif" typé (`null`, `false`, liste vide) plutôt que de lever une exception.
- La lecture registre dans `CreateForCurrentUser()` ne lève pas si la clé est absente — elle retourne un `steamPath` null, propageant naturellement l'état "Steam non installé" à travers toutes les méthodes.

## DI (`App.axaml.cs`)

```csharp
services.AddSingleton<ISteamEnvironment>(_ =>
    OperatingSystem.IsWindows()
        ? SteamEnvironment.CreateForCurrentUser()
        : new FakeSteamEnvironment());
```

`FakeSteamEnvironment` n'est plus enregistrée séparément comme type concret (elle servait uniquement au toggle dev, supprimé). Elle reste utilisée telle quelle comme fallback pour le développement hors-Windows (macOS), sans modification de son code.

## Tests

- `SteamLibraryLocatorTests` (`GlasLauncher.Core.Tests`), avec des dossiers temporaires contenant des fichiers VDF/ACF fabriqués à la main (même pattern que `FirstRunStoreTests` — `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())`, nettoyage en fin de test) :
  - Bibliothèque unique, jeu présent → retourne la bonne `SteamGameLocation`.
  - Bibliothèques multiples, jeu dans la deuxième → le trouve quand même.
  - `libraryfolders.vdf` absent → retourne `null`.
  - `appmanifest_108600.acf` absent dans toutes les bibliothèques → retourne `null`.
  - `UserConfig.BetaKey` présent → `Branch` reflète la beta ; absent → `Branch` vaut `"public"`.
  - Fichier VDF corrompu (contenu invalide) → retourne `null`, pas d'exception.
- Pas de test dédié pour `SteamEnvironment` (les deux primitives réellement Windows-only — lecture registre, `Process.GetProcessesByName` — sont des enrobages triviaux d'API .NET) ni pour le câblage DI. Vérification manuelle sur la VM Windows, cohérent avec la convention déjà établie pour la couche I/O/UI du projet (pas de tests dédiés sur les ViewModels/Views).

## Hors-scope (explicitement)

- Lecture de `localconfig.vdf` (vérification de l'option de lancement de l'agent Java, §4.2 du cahier des charges) — appartient au sous-projet #2 (mod Java / agent), qui dépend de ce sous-projet uniquement pour le dossier d'installation détecté.
- Lecture `FileVersionInfo` de `ProjectZomboid64.exe` / table de correspondance buildid→texte pour l'affichage cosmétique (§5.3) — rien dans l'UI actuelle ne consomme une version détectée affichable (`GameVersionEvaluator` affiche déjà `required.DisplayVersion`, fourni par le serveur, en cas de succès). Pourra être ajouté plus tard si un besoin UI concret apparaît.
- Mod Java / agent ZombieBuddy, packaging Velopack & CI — sous-projets séparés listés dans `docs/session-notes.md`.
