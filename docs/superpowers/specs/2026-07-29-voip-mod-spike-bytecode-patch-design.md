# Mod VOIP roleplay — Spike technique : patch bytecode (sous-projet 2, étape 0) — Design

**Statut :** approuvé, prêt pour planification
**Portée :** premier jalon du sous-projet 2 (implémentation du mod VOIP). Valide uniquement la faisabilité technique du patch bytecode — pas de logique whisper/parler/hurler réelle, pas de Lua, pas de synchronisation réseau. Ces éléments seront cadrés dans un spec séparé une fois ce spike validé.

Contexte complet et conclusions de la recherche : `docs/cahier-des-charges.md` §4.2, et `docs/superpowers/specs/2026-07-29-voip-mod-recherche-technique-design.md` (dans le repo `GlasLauncher`).

---

## But

Prouver qu'un agent Java, chargé au démarrage de Project Zomboid via `-agentlib`/`-javaagent` (même mécanisme que ZombieBuddy, déjà retenu pour `GlasJavaMod` dans le launcher), peut :

1. Localiser au chargement de la classe `zombie.core.raknet.VoiceManager` la méthode `UpdateVMClient()`.
2. Y injecter, juste avant chaque calcul `IsoUtils.smoothstep(maxDistance, minDistance, distance)` à l'intérieur de la boucle par-joueur distant, une écriture des deux champs statiques `minDistance`/`maxDistance` — sans modifier la logique environnante (formule, appels FMOD, etc. restent 100% vanilla).
3. Confirmer par un log que le patch s'est appliqué et que les valeurs injectées sont bien celles lues au moment du calcul.

Ce spike ne fait **aucune distinction par joueur** pour l'instant — il suffit d'injecter une valeur fixe (ex. codée en dur) pour prouver que l'interception fonctionne au bon endroit, au bon moment.

## Dépôt

Nouveau repo GitHub dédié : **`GlasVoipMod`** (compte `GotoKeiTai`, même compte que `Launcher-Qui-sonne-le-Glas---Project-Zomboid`), licence à définir plus tard (probablement MIT comme le launcher, non bloquant pour ce spike).

## Outillage

- **Gradle** avec wrapper (`gradlew`) — aucune installation globale requise, cohérent avec l'absence de JDK/outil Java préexistant sur la machine de développement.
- **JDK 17** — la version exacte utilisée par Project Zomboid lui-même (`zulu-17.jre`, confirmé lors de la recherche technique). Le agent doit cibler la même version pour éviter tout problème de compatibilité bytecode.
- **ASM** (bibliothèque de manipulation bytecode) — dépendance Gradle standard (`org.ow2.asm:asm` + `org.ow2.asm:asm-commons` pour les visiteurs utilitaires de plus haut niveau si besoin).

## Architecture du spike

### Fichiers

- `build.gradle.kts` / `settings.gradle.kts` — projet Gradle minimal, produisant un jar unique avec un `Premain-Class` déclaré dans le manifeste (agent Java classique).
- `src/main/java/glas/voip/spike/Agent.java` — point d'entrée de l'agent (`public static void premain(String args, Instrumentation inst)`), enregistre un `ClassFileTransformer` filtré sur `zombie/core/raknet/VoiceManager`.
- `src/main/java/glas/voip/spike/VoiceManagerTransformer.java` — implémente `ClassFileTransformer.transform(...)`, utilise ASM (`ClassReader`/`ClassWriter` + un `MethodVisitor` ciblant `UpdateVMClient`) pour injecter l'écriture des deux champs juste avant l'instruction qui charge `maxDistance`/`minDistance` pour l'appel à `smoothstep`.
- `README.md` — comment lancer le jeu avec l'agent (option de lancement Steam, cohérent avec la façon dont le launcher configurera ça plus tard), et ce que ce spike prouve/ne prouve pas.

### Comment on sait que ça marche

Critère de succès unique : lancer Project Zomboid avec `-javaagent:glasvoipmod-spike.jar`, se connecter à un serveur (ou une partie solo avec un second joueur/bot si possible, sinon juste vérifier que le patch s'applique sans crash même sans autre joueur), et observer dans les logs du jeu (`DebugLog.Voice` ou notre propre log) que :
1. L'agent a bien intercepté le chargement de `VoiceManager`.
2. La méthode patchée s'exécute sans exception au runtime (le jeu ne crash pas, le VOIP fonctionne toujours normalement par ailleurs).
3. Nos écritures de champs sont bien exécutées à chaque itération (log à chaque appel).

Si le patch casse le jeu (crash au chargement de classe, `VerifyError`, etc.), c'est un résultat de spike tout aussi valide — ça oriente vers l'approche B (Javassist) ou C (réécriture complète de méthode) évoquées en brainstorming, à retenter dans une itération séparée.

## Hors-scope (explicitement)

- Toute logique whisper/parler/hurler réelle, palier par joueur, lecture de `server.json`.
- Synchronisation réseau (Lua ou Java) du palier choisi.
- Indicateur visuel au sol (Lua, indépendant de ce spike).
- Distribution/installation du mod dans le launcher (`GlasJavaMod.jar` réel, vérification SHA-256, auto-réparation) — sujet du launcher lui-même, pas de ce repo.
- Tests automatisés — un spike de faisabilité technique se valide manuellement en lançant le jeu, pas par une suite de tests.
