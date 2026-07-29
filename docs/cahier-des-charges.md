<!-- title: Glas Launcher — Cahier des charges -->

# Glas Launcher — Cahier des charges

**Projet :** launcher Windows pour le serveur Project Zomboid *Pour qui sonne le glas*
**Dépôt :** [GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid](https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid) — MIT
**Statut :** conception terminée, maquettes UI validées, développement à venir

---

## 1. Vision du projet

Glas Launcher est un launcher Windows qui prépare automatiquement le poste du joueur avant de rejoindre le serveur *Pour qui sonne le glas*. Il n'installe jamais le jeu et ne remplace pas Steam : il agit comme une couche de **contrôle de conformité** au-dessus — il vérifie que l'installation du joueur correspond exactement à ce que le serveur attend, corrige automatiquement ce qui peut l'être, et n'exige jamais de manipulation manuelle de fichiers.

Expérience cible : le joueur ouvre le launcher, les vérifications s'exécutent seules, le bouton **Jouer** s'active dès que tout est conforme.

---

## 2. Contraintes

| Contrainte | Détail |
|---|---|
| Plateforme joueur | Windows uniquement |
| Distribution du jeu | Steam obligatoire |
| Version du jeu | Fixe (41.78.16), gérée via buildid Steam (voir §5.3) |
| Compte | Compte Steam requis |
| Whitelist | Gérée côté serveur PZ ; le launcher informe seulement (§7.3) |
| Budget infrastructure | Pas de VPS — hébergement statique uniquement (§8.3) |
| Poste de développement | macOS (Apple Silicon) — voir §9 |

---

## 3. Identité de marque

| Élément | Valeur |
|---|---|
| Nom du produit | Glas Launcher |
| Nom du serveur / marque | *Pour qui sonne le glas* |
| Emblème | Bouclier + cloche argentée sur fond vert |
| Palette | Vert forêt profond (`#0d1f16`–`#193624`), doré (`#c6a35f`–`#ecd39b`), argenté/acier (`#b7c4c0`) |
| Typographie display | Cinzel (capitales gravées, titres et wordmark) |
| Typographie interface | Barlow (texte courant, labels, boutons) |

---

## 4. Gestion des mods

### 4.1 Mods Workshop (Lua)

Steam reste seul responsable de l'installation, la mise à jour et la synchronisation des mods Workshop — le launcher ne télécharge rien lui-même.

Le launcher **vérifie obligatoirement** la conformité Workshop, sans appel réseau ni clé Steamworks :

1. `server.json` (ou `mods.json`) publie la liste des Workshop ID requis.
2. Le launcher lit localement `<bibliothèque Steam>/steamapps/workshop/appworkshop_108600.acf`, qui liste les items Workshop installés et leur `manifest` (version locale).
3. Tout Workshop ID requis absent → bouton **Jouer** désactivé, message explicite.
4. Le serveur dispose d'une collection Workshop dédiée, *[Pour Qui Sonne Le Glas](https://steamcommunity.com/sharedfiles/filedetails/?id=3719763771)* (187 objets). En cas de mods manquants, le launcher affiche un lien unique `steam://url/CommunityFilePage/3719763771` — Steam propose "S'abonner à tout" en un clic, plutôt que de lister chaque mod individuellement.

### 4.2 Mod Java (composant serveur)

Le serveur utilise un mod Java propriétaire, non distribuable via le Workshop (ses fichiers doivent être installés directement dans le dossier du jeu). Project Zomboid tourne sur une JVM ; les mods Java ne sont jamais chargés automatiquement et nécessitent un hook au niveau du classpath.

**Mécanisme retenu** : un agent Java (type [ZombieBuddy](https://github.com/zed-0xff/ZombieBuddy), open source) plutôt qu'un remplacement direct des classes du jeu — une modification de classes vanilla serait écrasée à chaque mise à jour ou vérification d'intégrité Steam.

- `ZombieBuddy.jar` + `zbNative.dll` sont déposés dans le dossier d'installation du jeu (hors Workshop).
- Le chargement se fait via l'option de lancement `-agentlib:zbNative --`, configurée dans les **options de lancement Steam** du jeu (stockées dans `localconfig.vdf`) — et non dans un fichier livré avec le jeu, pour survivre à "Vérifier l'intégrité des fichiers du jeu".
- L'agent étend dynamiquement le classpath (`Instrumentation.appendToSystemClassLoaderSearch`) pour charger `GlasJavaMod.jar` sans modifier aucune classe vanilla.
- `zbNative.dll` détecte lui-même les jars obsolètes et les remplace avant chargement, ce qui rend l'installation résiliente aux mises à jour du jeu.

**Configuration de l'option de lancement** : transmise au joueur lors du passage obligatoire par Discord/ticket pour la whitelist (copier-coller unique, en une fois). Le launcher lit `localconfig.vdf` en lecture seule pour vérifier que l'option est présente et affiche l'instruction si elle manque — il ne l'écrit jamais automatiquement, pour ne pas risquer de corrompre un fichier Steam partagé entre tous les jeux du compte.

**Auto-réparation** : le launcher détecte un mod Java manquant, corrompu, en mauvaise version ou supprimé accidentellement, et retélécharge automatiquement la bonne version (vérification SHA-256 avant installation).

---

## 5. Vérifications de conformité

Exécutées à chaque démarrage, dans cet ordre :

### 5.1 Steam
Vérifie que Steam est installé et lancé (bloquant pour toute la suite). Localisation via le registre Windows (`HKEY_CURRENT_USER\Software\Valve\Steam\SteamPath`).

### 5.2 Installation du jeu
Gère les **bibliothèques Steam multiples** :
1. `<SteamPath>/steamapps/libraryfolders.vdf` liste toutes les bibliothèques enregistrées et les AppID qu'elles contiennent (recherche de `108600`).
2. `<bibliothèque>/steamapps/appmanifest_108600.acf` donne `installdir`, `buildid` et `UserConfig.BetaKey`.
3. Dossier réel du jeu : `<bibliothèque>/steamapps/common/<installdir>`.

Parsing VDF/ACF via une librairie .NET existante (`Gameloop.Vdf`), réutilisée pour toute interaction avec les fichiers Steam (libraryfolders, appmanifest, appworkshop, localconfig).

### 5.3 Version du jeu
Le gate fonctionnel se fait sur le **buildid** (exact, non ambigu, généré par Steam à chaque publication) — jamais sur le texte de version affiché ("41.78.16"), qui reste purement cosmétique.

- `server.json` publie le buildid actuellement accepté et la branche attendue (`"public"`).
- Le launcher compare `buildid` + `UserConfig.BetaKey` (détecte un joueur resté sur une branche beta comme "unstable") aux valeurs publiées.
- Récupération du buildid côté admin à chaque patch : `steamcmd +login anonymous +app_info_print 108600 +quit` (outil officiel Valve, accès public anonyme).
- Affichage humain de la version : lu depuis les métadonnées natives de `ProjectZomboid64.exe` (`FileVersionInfo` en C#) si disponible, sinon table de correspondance buildid→texte en fallback cosmétique uniquement.

### 5.4 Mod Java
Présence, version et intégrité (SHA-256) du mod Java et de l'agent — voir §4.2.

### 5.5 Mods Workshop
Synchronisation avec la liste requise — voir §4.1.

### 5.6 Mise à jour du launcher
Vérifiée à chaque démarrage (voir §8.4).

### Lancement du jeu
Une fois toutes les vérifications validées, le bouton **Jouer** déclenche `steam://run/108600` — jamais l'exécutable directement, pour laisser Steam gérer l'overlay, les paramètres de lancement et la vérification DRM.

---

## 6. Interface utilisateur

Disposition inspirée de Battle.net : panneau latéral fixe (statut, checklist, actualités, versions) + zone principale avec le titre de marque et le bouton d'action, plutôt qu'un panneau d'onglets multi-jeux.

### 6.1 Écrans

| Écran | Rôle | Statut d'implémentation |
|---|---|---|
| Dashboard — prêt | État nominal, toutes vérifications validées, bouton Jouer actif | ✅ Implémenté (plan "UI Foundation") |
| Dashboard — bloqué | Une vérification échoue (ex. mods Workshop manquants), bouton Jouer désactivé, action corrective proposée | ✅ Implémenté |
| Premier lancement | Détection initiale (Steam, jeu, téléchargement du mod Java) avant que le dashboard standard soit utilisable | ✅ Implémenté (plan "Écrans secondaires") |
| Paramètres | Emplacement d'installation, support & diagnostics | ✅ Implémenté |
| Actualités / Changelog | Articles complets + historique de versions (onglets) | ✅ Implémenté |
| Modale — réparation en cours | Progression du téléchargement/vérification/installation du mod Java | ⏳ À faire |
| Modale — mise à jour disponible | Changelog + confirmation avant mise à jour du launcher | ⏳ À faire |

Toutes les vérifications de conformité (§5) et l'intégration Steam/Windows réelle (registre, VDF, agent Java) restent à implémenter — l'UI actuelle tourne sur des services Fake, développée entièrement sur macOS. C'est le prochain chantier, à faire sous Windows.

### 6.2 Informations affichées (dashboard)
État du serveur (en ligne/hors ligne), nombre de joueurs, ping, versions (launcher / jeu / mod Java), checklist de vérifications, actualités récentes, avec un lien "Tout voir" vers l'écran Actualités/Changelog.

### 6.3 Boutons principaux
Jouer, Réparer, Paramètres — de taille identique (le bouton Jouer se distingue par sa couleur et sa police, pas par sa taille). Le bouton Jouer est désactivé tant qu'une vérification obligatoire n'est pas validée.

### 6.4 Auto-réparation
Le bouton **Réparer** supprime le mod Java existant, télécharge la dernière version, vérifie son intégrité (SHA-256) et le réinstalle — avec une modale de progression dédiée (voir tableau ci-dessus).

---

## 7. Fonctionnalités de support

### 7.1 Rapport de diagnostic
Un bouton unique **"Générer un rapport de diagnostic"** produit un `.zip` prêt à partager sur Discord, contenant :

1. Les logs du launcher (session en cours).
2. Les logs Project Zomboid récents, lus depuis `%UserProfile%\Zomboid\Logs\` (emplacement standard des jeux Java, distinct du dossier d'installation Steam).
3. Un manifeste texte généré à la volée (version du launcher, buildid/branche détectée, version+hash du mod Java, mods Workshop requis vs détectés, version Windows).

Des liens secondaires "Ouvrir le dossier" (launcher / Project Zomboid) restent disponibles en retrait pour les cas avancés.

*(Note d'implémentation : l'écran Paramètres actuel simule déjà ce bouton — la génération réelle du zip est à câbler avec les vrais chemins de logs une fois sous Windows.)*

### 7.2 Emplacement d'installation
Affiché en lecture seule (détecté automatiquement, §5.2), avec possibilité de le forcer manuellement en cas d'échec de détection.

### 7.3 Whitelist
Rôle **purement informatif** : Project Zomboid applique lui-même le blocage de connexion pour les joueurs non-whitelistés côté serveur. Le launcher affiche un message/lien (Discord, ticket) si besoin, sans automatisation ni soumission de SteamID.

---

## 8. Architecture technique

### 8.1 Composants

1. **Glas Launcher** — application Windows C# / .NET 8, interface Avalonia UI (choisie plutôt que WPF, incompatible avec un développement sur macOS — voir §9).
2. **Hébergement statique** — fichiers de configuration et binaires (`version.json`, `server.json`, `changelog.md`, `news.json`, `GlasJavaMod.jar`, installeur du launcher). Aucun backend applicatif nécessaire.
3. **Steam** — installation du jeu, mises à jour, Workshop, lancement (`steam://run/108600`).

### 8.2 Distribution & mise à jour

- **Packaging & auto-update** : [Velopack](https://velopack.io) (successeur de Squirrel.Windows), support natif de GitHub Releases comme source de mise à jour.
- **Installeur** : `GlasLauncher-win-Setup.exe` généré par Velopack (le nom inclut le canal `-win-` par convention Velopack, pas juste `<packId>Setup.exe`) — installation dans `%LocalAppData%` sans élévation UAC/admin, raccourci Menu Démarrer, désinstallation propre. Pas de MSI (inutilement lourd) ni de MSIX (sandboxing incompatible avec l'accès nécessaire aux fichiers Steam/registre).
- **Canal de diffusion** : URL stable GitHub Releases (`.../releases/latest/download/GlasLauncher-win-Setup.exe`), épinglée une seule fois sur le Discord du serveur.

### 8.3 Hébergement (sans VPS)

- Installeur et mises à jour du launcher : **GitHub Releases** (gratuit, HTTPS natif, versionné).
- Fichiers de configuration légers (`server.json`, `version.json`) et mod Java : GitHub raw ou Cloudflare Pages/R2 (gratuit, HTTPS automatique).
- Aucune option ne nécessite de serveur à administrer.

### 8.4 Sécurité de la chaîne de mise à jour

- Téléchargements exclusivement en HTTPS.
- Vérification SHA-256 systématique avant exécution (launcher et mod Java).
- **Signature de code** : aucune en phase de tests/bêta (hash + HTTPS comme seule garantie, avertissement SmartScreen documenté pour les testeurs) ; passage à **SignPath.io** (signature gratuite pour projets open source) à l'ouverture publique — implique un dépôt public en place avant l'ouverture.
- **Pipeline** : GitHub Actions (gratuit) pour build → signature → hash → publication sur GitHub Releases à chaque nouvelle version, sans serveur à maintenir.

---

## 9. Environnement de développement

Développement sur **macOS (Apple Silicon)**, ciblant exclusivement Windows.

- **IDE** : JetBrains Rider ou VS Code + C# Dev Kit + extension Avalonia (Visual Studio for Mac étant discontinué). Compilation et cross-compilation `win-x64` directement depuis macOS.
- **VM Windows** : **UTM** (gratuit, virtualisation native Apple Silicon — sélectionner "Virtualize", pas "Emulate"), image Windows 11 ARM64 via le programme Windows Insider. Aucune licence requise pour un usage dev/test (Windows non activé reste pleinement fonctionnel).
- **Architecture recommandée** : isoler toute interaction Windows-spécifique (registre, VDF, `steam://`) derrière des interfaces dédiées (déjà en place : `ISteamEnvironment`, `IJavaModService`, etc. dans `GlasLauncher.Core`), pour unit-tester la logique métier (parsing VDF, comparaison de buildid, hash) directement sur macOS sans VM.
- **CI** : runners GitHub Actions `windows-latest` (gratuits) pour les builds/tests automatisés sur environnement Windows réel.
- **Validation finale** : test sur machine Windows physique via des testeurs bêta avant chaque release (comportement SmartScreen/antivirus pouvant différer d'une VM).

---

## 10. Philosophie de conception

Glas Launcher fonctionne comme un contrôleur de conformité : il ne remplace ni Steam ni Project Zomboid, et garantit que chaque joueur dispose exactement de la configuration requise avant de rejoindre le serveur, sans jamais avoir à manipuler de fichiers manuellement — que ce soit pour l'installation initiale, la réparation, la mise à jour ou le diagnostic.
