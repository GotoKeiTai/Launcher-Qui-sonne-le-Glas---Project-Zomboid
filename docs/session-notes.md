# Notes de session — reprise sur la VM Windows

Ce fichier résume ce qu'une session Claude Code sans historique (ex: sur la VM Windows) doit savoir pour reprendre le développement sans repartir de zéro. Le cahier des charges (`docs/cahier-des-charges.md`) reste la référence pour la vision produit et les specs fonctionnelles complètes — ce document-ci couvre le processus de travail et l'état d'avancement.

## Où on en est

- Toute l'UI (Dashboard prêt/bloqué, Paramètres, Actualités/Changelog, Premier lancement, modales Réparation/Mise à jour) est implémentée et tourne sur des services Fake — voir `docs/cahier-des-charges.md` §6.1 pour le détail écran par écran.
- Dernier commit sur `origin/main` : `391c96b` — "fix(app): center update-modal buttons and wrap long changelog entries".
- **Prochain chantier convenu** : remplacer les Fakes par de vrais services Windows. Décomposé ainsi (dans cet ordre, chacun avec son propre cycle spec → plan → implémentation) :
  1. **Fondations Steam & VDF** — détection du registre Steam (`HKEY_CURRENT_USER\Software\Valve\Steam\SteamPath`), parsing VDF/ACF (`libraryfolders.vdf`, `appmanifest_108600.acf`, `appworkshop_108600.acf`), implémentation réelle d'`ISteamEnvironment`. **À faire en premier** — tout le reste en dépend.
  2. **Gestion du mod Java / agent** — implémentation réelle d'`IJavaModService` (présence/version/SHA-256, lecture `localconfig.vdf`, vrai téléchargement+installation de l'agent ZombieBuddy + `GlasJavaMod.jar`). Dépend de #1 (a besoin du dossier d'installation du jeu détecté par #1).
  3. **Packaging Velopack & CI** — nature différente (infra de build/release, pas un service runtime). À traiter séparément, probablement en dernier.
- **On était en plein brainstorming du sous-projet #1** (fondations Steam & VDF) quand cette note a été écrite — aucune spec n'a encore été rédigée pour ce sous-projet. Reprendre avec le skill `superpowers:brainstorming` avant d'écrire le moindre code.

## Workflow établi cette session (à respecter)

1. **Brainstorming** (skill `superpowers:brainstorming`) — dialogue de clarification, jamais de code avant validation du design par l'utilisateur.
2. Spec écrite dans `docs/superpowers/specs/YYYY-MM-DD-<sujet>-design.md`, **committée** dans git.
3. **Plan** (skill `superpowers:writing-plans`) — plan détaillé tâche par tâche, écrit dans `docs/superpowers/plans/YYYY-MM-DD-<sujet>.md`, **jamais committé** (reste local uniquement — convention établie dès le premier plan de cette session).
4. **Implémentation** (skill `superpowers:subagent-driven-development`) — un subagent implémenteur frais par tâche, puis double review systématique (conformité au spec, puis qualité de code), boucle de correction si des issues Important/Critical sont trouvées. Les issues Minor n'imposent pas de boucle systématique — jugement au cas par cas.
5. Toujours dans un **git worktree** dédié (`.worktrees/<nom-branche>`, jamais directement sur `main`) — skill `superpowers:using-git-worktrees`.
6. Une fois toutes les tâches faites : review finale de l'ensemble du diff, puis skill `superpowers:finishing-a-development-branch` (fusion locale, PR, ou autre selon le choix de l'utilisateur).
7. Vérification visuelle UI : toujours par comparaison capture d'écran fournie par l'utilisateur ↔ maquette originale, jamais d'automatisation de clics fiable trouvée sur macOS — laisser l'utilisateur tester interactivement.

## Conventions de code établies

- **DI** : ViewModels de page (`DashboardViewModel`, `SettingsViewModel`, etc.) = `AddSingleton` (identité stable requise pour la navigation, `ViewLocator` ne les re-résout jamais). ViewModels de **modale** = construits manuellement à la demande, jamais enregistrés comme singleton (état jetable, recréé à chaque usage).
- **Erreurs/succès** : pattern `StatusMessage` (string?) + `IsStatusSuccess` (bool), avec le converter partagé `BoolToStatusBrushConverter` pour la couleur.
- **Converters** : instance statique `Instance`, brushes en hex littéral (pas de binding vers les resources XAML live, pour éviter le couplage), `ConvertBack` lève `NotSupportedException`.
- **`src/GlasLauncher.App` n'a PAS `ImplicitUsings` activé** (contrairement à `GlasLauncher.Core.Tests`, qui l'a) — usings explicites obligatoires dans tout nouveau fichier `.cs` de ce projet.
- **Boutons alignés en paire** : toujours `StackPanel Orientation="Horizontal"` + `MinWidth` sur chaque bouton — **jamais** `Grid ColumnDefinitions="*,*"` avec des boutons censés "stretch" (bug constaté : ils ne remplissent pas la colonne comme attendu dans ce projet).
- **Wrapping de texte à côté d'une icône/puce** : un `StackPanel Orientation="Horizontal"` donne une largeur illimitée à ses enfants dans l'axe d'empilement, donc `TextWrapping="Wrap"` ne s'applique jamais dedans (le texte déborde silencieusement). Utiliser un `Grid` (colonne `Auto` pour l'icône/puce, colonne `*` pour le texte) à la place — voir `DashboardView.axaml` (liste des vérifications) pour le pattern déjà en place.
- **`App.axaml`** : `RequestedThemeVariant="Dark"` forcé — l'app a une identité visuelle sombre fixe, ne doit jamais suivre le thème clair/sombre de l'OS (bug constaté : sur un Windows en thème clair, le texte des boutons sans `Foreground` explicite devenait quasi invisible).
- **Fenêtre custom (`SystemDecorations="None"`)** : la barre de titre a un `PointerPressed` câblé vers `BeginMoveDrag` pour permettre de déplacer la fenêtre — nécessaire dès qu'on utilise un chrome de fenêtre personnalisé.

## Environnement

- Repo cloné sur la VM ; `git pull` pour récupérer jusqu'au commit `391c96b` (et au-delà si du travail a été poussé depuis).
- Outils installés via `setup.ps1` (dossier partagé UTM, **hors du repo git**, ne pas chercher ce script dans le repo) : .NET 8 SDK, Git, GitHub CLI, Node.js, Claude Code.
- Build : `dotnet build` (solution complète) ou `dotnet build src/GlasLauncher.App` / `dotnet build src/GlasLauncher.Core`.
- Tests : `dotnet test tests/GlasLauncher.Core.Tests` (seul projet testé — pas de tests unitaires sur la couche App/ViewModels par convention, vérifié par les reviews spec/qualité à la place).
- Lancer l'app : `dotnet run --project src/GlasLauncher.App`.
