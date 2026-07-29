<!-- title: Correction — option de lancement du mod Java (VOIP) -->

# Correction — option de lancement du mod Java (VOIP)

**Statut :** constat + recommandation, écrit depuis le repo `GlasVoipMod` après investigation technique réelle. Corrige une hypothèse prise pendant le sous-projet #2 (`docs/superpowers/specs/2026-07-29-java-mod-agent-foundations-design.md`, marqué "terminé" dans `session-notes.md`).

## Ce qui a changé depuis le sous-projet #2

Au moment du sous-projet #2, le mod VOIP (`GlasVoipMod`, dépôt séparé) n'était pas terminé, et l'hypothèse de travail était qu'il serait éventuellement chargé **via ZombieBuddy** (`@Patch`, framework tiers) — d'où `SteamEnvironment.RequiredLaunchOption = "-agentlib:zbNative --"` codé en dur.

Le mod VOIP est maintenant terminé côté Lua (publié sur le Workshop, non répertorié) et côté Java (patch bytecode fonctionnel, vérifié en jeu). Une tentative de portage vers l'API `@Patch` de ZombieBuddy a été investiguée sérieusement cette semaine (voir `GlasVoipMod` pour le détail complet si besoin) :

- **Bloqué techniquement, pas par manque d'effort.** Le point d'injection réel (`VoiceManager.UpdateVMClient()`) n'a aucun paramètre — le joueur concerné est une variable locale de boucle, calculée en plein milieu d'une double boucle imbriquée, exactement à l'endroit où le patch doit s'insérer. L'API `@Patch.OnEnter`/`@Patch.OnExit` de ZombieBuddy ne s'accroche qu'au début/à la fin d'une méthode entière — pas à un point arbitraire au milieu d'une boucle. La seule alternative serait de réécrire `UpdateVMClient()` en entier (mode "remplacement complet" de ZombieBuddy), ce qui va à l'encontre de la philosophie "patch chirurgical, résistant aux mises à jour" du projet — jugé trop risqué, non retenu.
- **Décision prise (avec l'utilisateur) : le mod VOIP reste un agent `-javaagent:` autonome**, pas un mod ZombieBuddy. Il ne dépend pas de ZombieBuddy et n'a pas besoin d'être installé "dans" ZombieBuddy.
- **Vérifié empiriquement** (macOS, technique d'injection temporaire dans `Info.plist`, équivalent du `-javaagent:`/`-agentlib:` Windows) : notre propre agent et l'agent ZombieBuddy chargés **simultanément** (`-javaagent:GlasVoipMod-x.y.z.jar -javaagent:ZombieBuddy.jar` sur Mac ; `-javaagent:GlasVoipMod-x.y.z.jar -agentlib:zbNative --` serait l'équivalent Windows) coexistent sans aucun conflit — les deux agents patchent leurs classes respectives, le jeu atteint le menu principal normalement, aucune `VerifyError`/`ClassCircularityError`/exception. Donc si un futur mod Java du serveur exige réellement ZombieBuddy, les deux options de lancement peuvent cohabiter dans une seule chaîne combinée sans risque technique connu.

## Correction concrète à apporter

`SteamEnvironment.RequiredLaunchOption` (actuellement `"-agentlib:zbNative --"`, en dur) vérifie la mauvaise chose : ZombieBuddy n'est **pas requis** aujourd'hui, seul le mod VOIP l'est. Deux options :

1. **Correctif minimal** : remplacer la constante par l'option de lancement réelle du mod VOIP (`-javaagent:<nom-du-jar>` — nom exact encore à fixer, voir "Reste ouvert" ci-dessous).
2. **Recommandé** : rendre l'option de lancement requise **pilotée par le manifeste distant**, au même titre que la liste de fichiers (`JavaModManifest`). Ajouter un champ (ex. `RequiredLaunchOptions: string[]`) au manifeste, et changer `SteamLaunchOptionInspector.IsLaunchOptionConfigured` pour accepter une liste de tokens requis (chacun vérifié indépendamment via `.Contains()`, peu importe l'ordre dans la chaîne) plutôt qu'une seule chaîne exacte. Ça évite tout nouveau changement de code C# le jour où un futur mod Java exigera effectivement ZombieBuddy en plus — il suffira de mettre à jour le manifeste distant pour exiger `-javaagent:GlasVoipMod-x.y.z.jar -agentlib:zbNative --` (ou équivalent) au lieu d'une seule option. Cohérent avec la philosophie déjà établie du sous-projet #2 ("le launcher ne connaît aucun nom de mod en dur, il boucle sur ce que le manifeste liste").

## Reste ouvert (pas nouveau, mais redevient bloquant maintenant que le mod existe)

- **Hébergement réel du manifeste + du jar** : toujours un placeholder (déjà noté dans `session-notes.md`, sous-projet #4 non planifié). `GlasVoipMod` est un dépôt **privé** — comme pour `GlasLauncher` avant sa v0.1.0, une Release GitHub d'un dépôt privé n'est pas téléchargeable sans authentification. Il faudra soit le rendre public, soit choisir un autre hébergement statique (§8.3 du cahier des charges), avant que `RepairAsync` puisse réellement télécharger quoi que ce soit.
- **Nom/version stable du jar** : le nom actuel (`GlasVoipMod-0.1.0-spike.jar`) est un artefact de build de développement, pas un nom de release. `GlasVoipMod` n'a pas encore de processus CI/release propre (contrairement à `GlasLauncher`, qui a Velopack + GitHub Actions depuis le sous-projet #3) — à mettre en place avant de fixer l'URL/SHA-256 dans le manifeste.
- **Question annexe soulevée pendant l'investigation, pas tranchée** : le mod VOIP a été conçu pour que son fichier `.jar` puisse un jour vivre dans `media/java/` du même mod Workshop que sa partie Lua (déjà publiée), plutôt que d'être installé séparément par le launcher dans le dossier du jeu — mais cette option supposait un portage vers ZombieBuddy, qui n'a pas eu lieu. Le mod Java reste donc bien un fichier à installer par le launcher dans le dossier du jeu, comme prévu à l'origine au §4.2 du cahier des charges — pas de changement d'architecture sur ce point, malgré la piste explorée.

## Ce qu'il reste à faire côté GlasLauncher (résumé actionnable)

1. Ajouter `RequiredLaunchOptions` (ou équivalent) au schéma `JavaModManifest`, alimenté par le vrai manifeste une fois hébergé.
2. Changer `SteamLaunchOptionInspector.IsLaunchOptionConfigured` pour accepter plusieurs tokens requis (liste), pas une seule chaîne.
3. Mettre à jour `SteamEnvironment` en conséquence (supprimer le `-agentlib:zbNative --` codé en dur).
4. Mettre à jour le message d'instruction affiché au joueur (actuellement montre l'option ZombieBuddy en clair/copiable — doit montrer la vraie option requise, dynamique depuis le manifeste).
5. Ne rien construire de plus tant que l'hébergement réel (manifeste + jar) n'est pas en place — c'est le vrai bloquant restant, pas un problème de logique C#.
