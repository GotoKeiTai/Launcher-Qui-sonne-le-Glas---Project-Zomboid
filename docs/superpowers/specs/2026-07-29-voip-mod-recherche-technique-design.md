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

*(à compléter une fois l'investigation terminée)*

---

## Hors-scope (explicitement)

- Toute écriture de code de mod (Lua ou Java) — c'est le sous-projet 2, cadré séparément après cette recherche.
- Le choix du repo/dépôt qui hébergera le mod final (`GlasJavaMod`) — décision à prendre au moment de cadrer le sous-projet 2.
