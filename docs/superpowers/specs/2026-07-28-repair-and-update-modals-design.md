# Modales "Réparation en cours" et "Mise à jour disponible" — Design

**Statut :** approuvé, prêt pour planification
**Portée :** les deux dernières modales listées au §6.1 du cahier des charges (`docs/cahier-des-charges.md`). Tout le reste de l'UI (Dashboard, Paramètres, Actualités, Premier lancement) est déjà implémenté.

## Contexte

Les interfaces backend et leurs Fakes existent déjà, scaffoldées lors du plan "UI Foundation" mais jamais branchées à une ViewModel :

- `IJavaModService` (`GetStatusAsync`, `RepairAsync(IProgress<RepairProgress>)`) + `FakeJavaModService`
- `IUpdateService` (`CheckForUpdateAsync`) + `FakeUpdateService`
- Modèles : `JavaModInfo`, `RepairProgress(string StepName, int PercentComplete)`, `UpdateInfo(string CurrentVersion, string LatestVersion, IReadOnlyList<string> ChangelogEntries)`

Les deux services sont déjà enregistrés dans le DI (`App.axaml.cs`). Le plomberie de modale existe dans `MainWindow.axaml` (`CurrentModal` + overlay semi-transparent centré) mais rien ne l'alimente. Le bouton "Réparer" du Dashboard n'a aujourd'hui aucune `Command`.

Deux maquettes fournies par l'utilisateur font foi pour le visuel exact (disposition, texte, couleurs).

## Architecture

Deux nouvelles paires ViewModel/View, suivant les conventions déjà établies dans le projet (ViewLocator par convention de nom `XxxViewModel` → `XxxView`, `[ObservableProperty]`, converters avec `Instance` statique et brushes hex littéraux, pattern `StatusMessage`).

**Différence clé avec les ViewModels de page existants** : `DashboardViewModel`, `SettingsViewModel`, etc. sont enregistrés `AddSingleton` car leur identité doit être stable pour la navigation. Une modale est un état jetable, recréé à chaque déclenchement (une réparation repart de 0%, une vérification de mise à jour peut renvoyer des infos différentes). `RepairModalViewModel` et `UpdateModalViewModel` sont donc construits manuellement au moment du déclenchement, jamais enregistrés dans le DI comme singletons.

### Nouveaux fichiers

- `src/GlasLauncher.App/ViewModels/RepairModalViewModel.cs`
- `src/GlasLauncher.App/ViewModels/RepairStepViewModel.cs`
- `src/GlasLauncher.App/ViewModels/UpdateModalViewModel.cs`
- `src/GlasLauncher.App/Views/RepairModalView.axaml` (+ `.axaml.cs`)
- `src/GlasLauncher.App/Views/UpdateModalView.axaml` (+ `.axaml.cs`)
- `src/GlasLauncher.App/Converters/RepairStepStateToBrushConverter.cs` (puce colorée : gris pour Pending, doré pour InProgress, gris atténué pour Done — visuel différent du badge à coche circulaire de `FirstRunStepToBadgeBrushConverter`, donc pas de réutilisation directe de ce converter-là)

### Fichiers modifiés

- `src/GlasLauncher.Core/Models/RepairProgress.cs` — ajout de deux champs optionnels
- `src/GlasLauncher.Core/Services/IUpdateService.cs` — ajout de `ApplyUpdateAsync()`
- `src/GlasLauncher.Core/Services/Fakes/FakeJavaModService.cs` — reporte les nouveaux champs pendant l'étape de téléchargement
- `src/GlasLauncher.Core/Services/Fakes/FakeUpdateService.cs` — implémente `ApplyUpdateAsync()` (délai simulé) ; `CheckForUpdateAsync()` doit désormais retourner un `UpdateInfo` non-null pour que la modale soit visible en dev (actuellement retourne toujours `null`)
- `src/GlasLauncher.App/ViewModels/DashboardViewModel.cs` — Command sur "Réparer", événement `RepairRequested`, `StatusMessage`/succès déjà en place réutilisé pour le message post-mise à jour
- `src/GlasLauncher.App/Views/DashboardView.axaml` — `Command` sur le bouton "Réparer"
- `src/GlasLauncher.App/ViewModels/MainWindowViewModel.cs` — injection de `IJavaModService` et `IUpdateService`, abonnement à `RepairRequested`, méthode publique `CheckForUpdatesAsync()`
- `src/GlasLauncher.App/App.axaml.cs` — appel de `CheckForUpdatesAsync()` au démarrage, uniquement quand l'app atterrit directement sur le Dashboard (pas pendant le Premier Lancement)

## Flux : Réparation en cours

1. L'utilisateur clique "Réparer" sur le Dashboard → `DashboardViewModel` lève `RepairRequested` (Action simple, même pattern que `SettingsRequested`/`ChangelogRequested`).
2. `MainWindowViewModel` (déjà abonné) construit `new RepairModalViewModel(_javaModService)`, s'abonne à son événement `Completed`, l'assigne à `CurrentModal`, puis démarre `_ = modal.RunRepairAsync()` (même pattern fire-and-forget que `ShowFirstRun()` avec `FirstRunViewModel`).
3. `RepairModalViewModel` maintient une liste fixe de 4 `RepairStepViewModel` (Name figés, correspondant exactement aux `StepName` que `FakeJavaModService.RepairAsync` reporte dans l'ordre : "Ancienne version supprimée", "Téléchargement du mod Java", "Vérification de l'intégrité (SHA-256)", "Installation"). Chaque `IProgress<RepairProgress>.Report` reçu :
   - marque l'étape correspondante (recherche par nom) `InProgress`, les précédentes `Done`
   - met à jour `PercentComplete` global (barre de progression) et le sous-titre (mappé depuis le nom d'étape vers une phrase descriptive complète, ex. "Téléchargement du mod Java" → "Téléchargement de la dernière version du mod Java depuis le serveur Glas Launcher…")
   - si `BytesDownloaded`/`BytesTotal` sont renseignés (uniquement pendant l'étape de téléchargement), affiche "X,X Mo / Y,Y Mo" en bas à gauche ; sinon cette ligne est masquée, seul le pourcentage reste visible en bas à droite
4. À la fin (`RepairAsync` retourne), la dernière étape passe `Done`, une courte pause (~400ms, cohérent avec les délais déjà utilisés dans `FirstRunViewModel`) puis `Completed?.Invoke()`.
5. `MainWindowViewModel` reçoit `Completed` : `CurrentModal = null` et relance `dashboard.RefreshCommand` (remet à jour les infos serveur/actualités ; ne change pas la vérification "Mod Java à jour" qui reste un placeholder — non réintégré dans cette passe, hors-scope).
6. En cas d'exception dans `RunRepairAsync` : capturée, `StatusMessage` affiché dans la modale avec un bouton "Fermer" (seul cas où l'utilisateur peut quitter manuellement — pas de bouton Annuler/Fermer pendant une réparation en cours, fidèle à la maquette).

Pas de `CanExecute` sur la Command "Réparer" : toujours cliquable, y compris quand `CanPlay` est vrai (réparation manuelle proactive autorisée), comme aujourd'hui avec `BoolToRepairBackgroundConverter` qui ne fait que changer son style visuel.

## Flux : Mise à jour disponible

1. Au démarrage, dans `App.axaml.cs`, juste après la vérification `IFirstRunStore` : si l'app va directement au Dashboard (premier lancement déjà complété), appel fire-and-forget `_ = mainWindowViewModel.CheckForUpdatesAsync();`. Si c'est un premier lancement, la vérification est sautée cette fois (elle se déclenchera normalement au lancement suivant, une fois `FirstRunViewModel.Completed` levé et le Dashboard affiché).
2. `CheckForUpdatesAsync()` appelle `IUpdateService.CheckForUpdateAsync()`. Si le résultat est non-null, construit `new UpdateModalViewModel(_updateService, updateInfo)` et l'assigne à `CurrentModal`.
3. La modale affiche : version actuelle → nouvelle version, liste à puces du changelog (`UpdateInfo.ChangelogEntries`), boutons "Plus tard" et "METTRE À JOUR".
4. "Plus tard" : `CurrentModal = null`, aucune autre action. Pas de re-vérification avant le prochain démarrage complet de l'app.
5. "METTRE À JOUR" : appelle la nouvelle méthode `IUpdateService.ApplyUpdateAsync()` (le Fake simule un délai puis "réussit" — pas de vrai téléchargement/installation, ce sera le rôle de Velopack lors du chantier "services Windows réels", hors-scope ici). Pendant l'appel, le bouton passe en état désactivé/chargement. Au succès : `CurrentModal = null` et `dashboard.StatusMessage`/`IsStatusSuccess` posé à un message de confirmation ("Mise à jour installée — redémarrez le launcher pour l'appliquer." ou équivalent, réutilisant le pattern `StatusMessage`/`IsStatusSuccess` déjà présent sur `DashboardViewModel`). En cas d'exception : message d'erreur affiché dans la modale elle-même (pas de fermeture automatique), avec possibilité de réessayer ou fermer.

`FakeUpdateService.CheckForUpdateAsync()` doit être modifié pour retourner un `UpdateInfo` non-null par défaut (actuellement toujours `null`), sinon la modale ne serait jamais visible en développement. Contenu réutilisant les valeurs de la maquette (v0.1.0 → v0.2.0, les 3 entrées de changelog visibles sur la capture).

## Modèle `RepairProgress` — champs ajoutés

```csharp
public record RepairProgress(
    string StepName,
    int PercentComplete,
    double? MegabytesDownloaded = null,
    double? MegabytesTotal = null);
```

Champs optionnels pour ne pas casser la sémantique existante ; seul `FakeJavaModService` les renseigne, uniquement pendant l'étape "Téléchargement du mod Java".

## Tests

- Tests unitaires Core (`GlasLauncher.Core.Tests`) pour tout changement de comportement observable dans `FakeJavaModService`/`FakeUpdateService` (ex. `ApplyUpdateAsync` complète sans exception, `CheckForUpdateAsync` retourne un `UpdateInfo` non-null avec les champs attendus).
- Pas de tests unitaires sur les nouvelles ViewModels/Views App — cohérent avec le reste du projet (`DashboardViewModel`, `SettingsViewModel`, etc. n'ont pas de tests dédiés ; la correction est vérifiée via le workflow de review spec/qualité en deux temps du plan d'implémentation, plus une vérification visuelle manuelle par l'utilisateur contre les deux maquettes).

## Hors-scope (explicitement)

- Vraie logique de téléchargement/installation (Velopack, SHA-256 réel) — chantier "services Windows réels" séparé.
- Réintégration de la vérification "Mod Java à jour" du Dashboard vers `IJavaModService.GetStatusAsync()` (reste un placeholder `Passed` codé en dur).
- Annulation d'une réparation en cours.
- Nouvelle vérification de mise à jour pendant la session (seulement au démarrage).
