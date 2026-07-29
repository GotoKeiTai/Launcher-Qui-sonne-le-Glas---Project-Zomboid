# Mod VOIP roleplay — Recherche technique (sous-projet 1) — Design

**Statut :** approuvé, investigation en cours
**Portée :** déterminer la faisabilité technique d'un mod permettant aux joueurs d'ajuster eux-mêmes leur portée de voix en jeu (chuchoter/parler/hurler, façon FiveM), sur le serveur *Pour qui sonne le glas*. Ce document ne couvre **que** la phase de recherche — l'implémentation réelle du mod (sous-projet 2) sera cadrée séparément une fois les résultats connus.

---

## 1. Contexte et contrainte de départ

Le chat vocal (VOIP) de Project Zomboid est actuellement à portée **fixe pour tous les joueurs**, réglée côté serveur. Il est **entièrement codé en Java** et n'expose aucun point d'entrée Lua — un mod Lua classique ne peut donc pas influencer cette distance. Personne dans l'équipe n'a encore examiné le code source décompilé du jeu : cette phase part de zéro.

Seule voie technique envisageable : un agent Java (même mécanisme que celui déjà retenu pour `GlasJavaMod` dans le launcher — voir `docs/cahier-des-charges.md` §4.2), potentiellement avec une transformation de bytecode (`Instrumentation.addTransformer` + une librairie comme ASM ou Javassist) si le comportement existant doit être **modifié** plutôt que simplement complété. C'est une opération nettement plus invasive que l'approche actuelle du launcher, qui ne fait qu'*ajouter* des classes sans toucher au code existant du jeu.

## 2. But de cette phase

Répondre à ces questions, avant d'écrire la moindre ligne de code de mod :

1. Quelle(s) classe(s) Java gère(nt) le calcul/l'application de la distance audio du VOIP ?
2. Cette distance est-elle une constante, un champ par joueur, une valeur calculée dynamiquement, ou un paramètre de sandbox/serveur déjà existant ?
3. Un patch bytecode est-il réalisable avec les outils déjà retenus pour le launcher (agent Java + classpath), ou faut-il une technique plus lourde (transformation de bytecode à l'injection) ?
4. Y a-t-il un obstacle rédhibitoire (code natif, obfuscation forte, valeur non accessible en mémoire) ?

## 3. Processus

Contrairement au reste du développement de cette session (worktree isolé + subagents dispatchés par tâche), cette phase se fait **en collaboration directe, dans la conversation en cours** : travail exploratoire et itératif, qui nécessite l'installation du jeu par l'utilisateur et une adaptation en temps réel selon ce qu'on découvre — mal adapté à des subagents isolés sans contexte de la découverte précédente.

**Étapes prévues (indicatives, s'adapteront aux découvertes) :**
1. Localiser les fichiers `.jar`/`.class` de l'installation Project Zomboid sur ce Mac (Steam).
2. Décompiler avec un outil comme CFR ou Vineflower (installés via Homebrew si besoin).
3. Chercher les classes/méthodes liées à la voix/l'audio (mots-clés : voice, voip, audio, mic, talk, shout, whisper, radio...).
4. Lire et comprendre le mécanisme trouvé.
5. Documenter les conclusions et évaluer la faisabilité.

## 4. Point légal — code décompilé

Le code source décompilé de Project Zomboid appartient à The Indie Stone. Il **ne sera jamais committé** dans ce repo (ni aucun autre repo public) — il reste uniquement en local, hors du dossier du projet git (scratchpad ou équivalent). Seules nos **conclusions écrites** (compréhension du mécanisme, dans nos propres mots, sans coller le code source) sont documentées et committées.

## 5. Livrable

Un document de synthèse (mis à jour dans ce fichier, section "Conclusions" ci-dessous, une fois la recherche terminée) couvrant :
- Le(s) nom(s) de classe(s) identifiée(s) et leur rôle
- La nature de la valeur de distance (constante / champ / calcul dynamique / option sandbox)
- La faisabilité et l'approche technique recommandée pour le patch
- Une recommandation d'approche pour le sous-projet 2 (implémentation)

## 6. Conclusions

Investigation menée par décompilation (CFR 0.152) des classes du jeu, directement depuis l'installation macOS de Project Zomboid (`Project Zomboid.app/Contents/Java`, classes non empaquetées en jar, non obfusquées — décompilation directe sans extraction nécessaire).

### Classes identifiées

- **`zombie.core.raknet.VoiceManager`** — cœur de la logique VOIP côté client : calcul du volume perçu selon la distance, gestion de session, lecture des paramètres serveur.
- **`zombie.core.raknet.VoiceManagerData`** — état par joueur, mais uniquement côté "auditeur" (mute, canal de lecture FMOD, données radio à proximité) — **aucun champ existant pour un "mode/palier" de transmission choisi par le joueur qui parle**.
- **`zombie.network.ServerOptions`** — contient déjà `VoiceMinDistance` / `VoiceMaxDistance` (`DoubleServerOption`, bornes 0–100000, défauts 10.0 / 100.0) et `VoiceEnable`, `Voice3D`. Configurable côté admin serveur (équivalent PZ d'un `servertest.ini`), au même titre que les autres options serveur.
- **`zombie.core.raknet.RakVoice`** — pont JNI vers `libRakNet.dylib` (bibliothèque native RakNet). Gère le **transport brut** des trames audio, hors de portée d'un patch bytecode Java.

### Nature de la valeur de distance

`minDistance`/`maxDistance` sont des **champs `static` sur `VoiceManager`** : une seule valeur globale par client, synchronisée une fois depuis `ServerOptions` au moment de la connexion (`VoiceOpenChannelReply`). **Aucune notion par-joueur n'existe aujourd'hui.**

Point clé pour la faisabilité : le calcul "volume perçu selon la distance" (`UpdateVMClient()` → `IsoUtils.smoothstep(maxDistance, minDistance, distance)` → appels FMOD `SetVolume`/`Set3DAttributes`) est **entièrement en bytecode Java pur**, exécuté côté client. Le transport natif RakNet livre les trames audio à tout le monde indépendamment de la distance ; c'est uniquement le volume de lecture, calculé en Java, qui rend une voix inaudible au-delà de `maxDistance`. **On n'a donc pas besoin de toucher au code natif** — toute la logique pertinente est patchable par transformation bytecode.

### Faisabilité et approche recommandée

**Ce qui manque** : une notion de palier (chuchoter/parler/hurler) par joueur, synchronisée entre clients, et un point de patch qui substitue les `minDistance`/`maxDistance` globaux par des valeurs dépendant du palier choisi par le joueur **qui parle** (pas celui qui écoute) — la boucle de `UpdateVMClient()` itère déjà sur chaque joueur distant (`isoPlayer`) avant de calculer son volume, donc le point d'interception existe.

Deux façons de synchroniser le palier choisi entre joueurs :
- **Option A (recommandée)** — entièrement en **Lua**, via l'API réseau standard de PZ (`sendClientCommand` / `Events.OnClientCommand`), sans toucher au Java pour cette partie. Le patch bytecode n'a plus qu'à lire une table exposée par le mod Lua (le moteur Java dialogue déjà en permanence avec la VM Lua embarquée — imports `se.krka.kahlua.*` visibles dans `VoiceManager` lui-même). Minimise la portion de code patchée en bytecode, qui est la plus fragile et la plus coûteuse à revalider à chaque mise à jour du jeu.
- **Option B** — un paquet réseau custom entièrement géré côté Java (agent). Plus robuste en théorie, mais duplique un mécanisme que Lua sait déjà faire nativement, pour un gain incertain.

**Indicateur visuel (cercle au sol)** : confirmé faisable en Lua pur (dessin d'overlay + détection de touche), sans aucune dépendance au patch Java — comme pressenti initialement.

### Recommandation pour le sous-projet 2

1. Patch bytecode ciblé sur `VoiceManager` (méthode `UpdateVMClient()` ou équivalent après mise à jour du jeu) pour substituer le lookup global `minDistance`/`maxDistance` par un lookup par-émetteur.
2. Synchronisation du palier choisi et indicateur visuel : 100% Lua (Option A), aucun nouveau code Java au-delà du patch de distance.
3. Distances par palier configurables via `server.json` (cohérent avec le launcher) — `VoiceMaxDistance`/`VoiceMinDistance` de PZ servent de bornes globales pour le palier "hurler" (le plus fort), whisper/talk calculés comme fractions de cette plage.
4. **Prochaine étape technique concrète, avant d'écrire le mod** : valider qu'un agent Java + une lib de transformation bytecode (ASM ou Javassist) peut réellement réécrire `UpdateVMClient()` sans casser le reste — ce n'est pas encore testé, seulement identifié comme le point de patch probable. À faire en tout début du sous-projet 2 (spike technique isolé avant le reste de l'implémentation).
5. Risque à documenter dans le cahier des charges du mod : comme tout patch bytecode, fragile aux mises à jour du jeu — prévoir une détection de version + message d'erreur clair si la méthode cible a changé de forme, plutôt qu'un crash silencieux.

---

## Hors-scope (explicitement)

- Toute écriture de code de mod (Lua ou Java) — c'est le sous-projet 2, cadré séparément après cette recherche.
- Le choix du repo/dépôt qui hébergera le mod final (`GlasJavaMod`) — décision à prendre au moment de cadrer le sous-projet 2.
