# Écrans secondaires (Paramètres, Actualités, Premier lancement) — Spec

**Date:** 2026-07-28
**Statut:** Approuvé par l'utilisateur, prêt pour planification

## Contexte

Le plan "UI Foundation" a livré le shell de navigation (ViewLocator, MainWindow, overlay modale) et l'écran Dashboard, tous alignés sur les maquettes HTML validées plus tôt dans le projet. Ce spec couvre les trois écrans secondaires restants, dont les maquettes existent déjà :

- `glass-launcher-settings.html` — écran Paramètres
- `glass-launcher-news.html` — écran Actualités (avec onglet Changelog)
- `glass-launcher-firstrun.html` — écran Premier lancement

Les modales (Réparation en cours, Mise à jour disponible) et les services Windows réels (registre/VDF, agent Java, Velopack) restent des chantiers séparés, non couverts ici.

## Architecture

Trois nouvelles paires View/ViewModel s'ajoutent au shell de navigation existant :
- `SettingsViewModel` / `SettingsView`
- `NewsViewModel` / `NewsView`
- `FirstRunViewModel` / `FirstRunView`

`MainWindowViewModel` gagne une méthode de navigation (`NavigateTo(ViewModelBase)`) qui met à jour `CurrentPage`. Le "← Retour" de chaque écran secondaire navigue vers l'instance `DashboardViewModel` déjà détenue par `MainWindowViewModel` (pas de reconstruction).

Le composition root (`App.axaml.cs`) décide de l'écran initial en interrogeant `IFirstRunStore.HasCompletedFirstRunAsync()` avant de construire `MainWindowViewModel` : si le premier lancement n'a pas encore eu lieu, `CurrentPage` démarre sur `FirstRunViewModel` ; sinon, sur `DashboardViewModel` comme aujourd'hui.

## Nouveaux éléments `GlasLauncher.Core`

### Modèles
- `ChangelogEntry(string Version, DateOnly Date, IReadOnlyList<string> Changes)` — une entrée de changelog applicatif (distinct des actualités serveur).

### Services
- `IServerInfoService` gagne `Task<IReadOnlyList<ChangelogEntry>> GetChangelogAsync()`. `FakeServerInfoService` retourne 2-3 entrées historiques plausibles (ex: "v0.1.0 — Version initiale du launcher").
- Nouvelle interface `IFirstRunStore` :
  ```csharp
  public interface IFirstRunStore
  {
      Task<bool> HasCompletedFirstRunAsync();
      Task MarkFirstRunCompleteAsync();
  }
  ```
  Une seule implémentation réelle (`FirstRunStore`), pas de split Fake/Real : c'est de l'I/O fichier JSON pur, multiplateforme, sans dépendance Windows. Le fichier vit dans `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)/GlasLauncher/state.json`, format `{ "firstRunCompleted": true }` via `System.Text.Json`. Enregistrée directement dans `App.axaml.cs` comme singleton, sans branche `OperatingSystem.IsWindows()`.

## Écran Paramètres

Reprend la maquette : carte "Emplacement d'installation du jeu" (chemin en lecture seule + bouton Parcourir), carte "Support & diagnostics" (Rapport de diagnostic + Générer, liens dossiers logs, Discord + Rejoindre, Informations de version + Copier).

`SettingsViewModel` :
- `InstallPath` : string affiché (valeur Fake pour l'instant, ex. celle déjà utilisée dans les mockups — sera branché sur la vraie détection Steam dans le plan des services Windows)
- `BrowseCommand` : ouvre le sélecteur de dossier natif via `TopLevel.StorageProvider.OpenFolderPickerAsync` (Avalonia, cross-platform réel)
- `GenerateDiagnosticReportCommand` : reste simplifié — affiche un message de statut type "Rapport généré (simulation)" sans vraie création de zip, car les vrais chemins de logs Windows n'existent pas encore
- `OpenLauncherLogsCommand` / `OpenPzLogsCommand` : ouvrent un dossier réel via `Process.Start` avec `UseShellExecute = true` (fonctionne cross-platform pour ouvrir un chemin dans l'explorateur de fichiers)
- `JoinDiscordCommand` : `Process.Start` sur l'URL Discord réelle du serveur
- `CopyVersionInfoCommand` : copie réelle dans le presse-papiers via `TopLevel.Clipboard.SetTextAsync`
- `BackCommand` : navigue vers le Dashboard

## Écran Actualités

Deux onglets (`SelectedTab` : Actualités / Changelog), même patron de carte que le Dashboard mais avec le corps complet du texte (pas tronqué).

`NewsViewModel` :
- `NewsItems` : `ObservableCollection<NewsItem>`, peuplé via `GetNewsAsync()` (déjà existant)
- `ChangelogEntries` : `ObservableCollection<ChangelogEntry>`, peuplé via le nouveau `GetChangelogAsync()`
- Deux `bool` (`IsNewsTabActive`/`IsChangelogTabActive`) pour piloter l'affichage, cohérent avec le style de binding déjà utilisé ailleurs (ex. `ShowMessageBelow`)
- `BackCommand` : navigue vers le Dashboard
- Le lien "Tout voir · Changelog" du Dashboard navigue vers `NewsViewModel` avec l'onglet Changelog pré-sélectionné (paramètre au constructeur ou méthode d'init)

## Écran Premier lancement

Séquence à 4 étapes affichée dans une carte, dans l'ordre de la maquette :
1. Steam détecté (instantané, ✓)
2. Project Zomboid détecté (instantané, ✓)
3. Téléchargement du mod Java... (barre de progression simulée, quelques centaines de ms par palier via `Task.Delay`, réutilise le style de `RepairProgress` déjà défini dans Core pour le pourcentage)
4. Enregistrement de la configuration (dernier palier)

`FirstRunViewModel` :
- Séquence pilotée par une méthode `RunSequenceAsync()` lancée au constructeur (même pattern que `DashboardViewModel.RefreshAsync`), avec try/catch équivalent
- `Steps` : `ObservableCollection` de mini-VM (nom, état : Pending/InProgress/Done, pourcentage si InProgress)
- À la fin de la séquence : appelle `IFirstRunStore.MarkFirstRunCompleteAsync()` puis navigue vers le Dashboard (même mécanisme que "← Retour")

## Câblage navigation Dashboard

- Bouton **Paramètres** : `Command="{Binding NavigateToSettingsCommand}"` sur `MainWindowViewModel` (le Dashboard ne connaît pas les autres écrans directement — la navigation reste au niveau de `MainWindowViewModel`, `DashboardViewModel` expose juste les commandes qui délèguent)
- Lien **Tout voir · Changelog** : idem, navigue vers News/Changelog
- Bouton **Réparer** : reste sans `Command` pour cette passe (chantier modales séparé, pas encore fait)

## Tests

- `ChangelogEntry` : pas de logique, pas de test dédié (record simple)
- `IFirstRunStore` / `FirstRunStore` : tests d'intégration légers dans `GlasLauncher.Core.Tests` avec un chemin de fichier temporaire (pas de mock nécessaire, c'est une petite classe de vraie I/O testée directement)
- Pas de nouvelle logique pure côté `Logic/` pour cette passe (contrairement à `GameVersionEvaluator`/`WorkshopEvaluator`) — le contenu de ce plan est presque entièrement UI + I/O simple

## Hors scope (rappel)

- Modales (Réparation en cours, Mise à jour disponible)
- Services Windows réels (registre/VDF, agent Java, Velopack)
- Vraie génération de rapport de diagnostic (zip de logs réels)
