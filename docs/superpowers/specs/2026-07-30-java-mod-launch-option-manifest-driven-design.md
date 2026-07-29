# Option de lancement du mod Java — pilotée par le manifeste — Design

**Statut :** approuvé, prêt pour planification
**Portée :** correction ciblée du sous-projet #2 (Gestion du mod Java / agent), suite à `docs/superpowers/specs/2026-07-30-java-mod-launch-option-correction.md` (constat écrit depuis le dépôt `GlasVoipMod` après investigation technique réelle). Ne touche à rien côté Velopack/CI (sous-projet #3, terminé et indépendant).

## Contexte

`SteamEnvironment.RequiredLaunchOption` vaut aujourd'hui `"-agentlib:zbNative --"` (l'option de lancement de ZombieBuddy), codée en dur — hypothèse prise au sous-projet #2 quand on pensait que le mod VOIP (`GlasVoipMod`) serait chargé via ZombieBuddy. Investigation technique depuis confirmée : le portage vers l'API `@Patch` de ZombieBuddy est bloqué (point d'injection en plein milieu d'une boucle, hors de portée d'`OnEnter`/`OnExit`) — le mod VOIP reste un agent `-javaagent:` autonome, sans dépendance à ZombieBuddy. Le check "option de lancement Steam" vérifie donc aujourd'hui la mauvaise chose.

Décidé pendant ce brainstorming :

- **Approche manifeste-driven** (recommandée par le document de constat, confirmée ici) plutôt qu'un simple remplacement de constante — cohérent avec la philosophie déjà établie du sous-projet #2 ("le launcher ne connaît aucun nom de mod en dur, il boucle sur ce que le manifeste liste").
- **Valeur réelle non disponible** — le nom du jar du mod VOIP n'est pas encore fixé côté `GlasVoipMod` (pas de release/CI propre là-bas). Un placeholder est utilisé ici, exactement comme pour l'URL du manifeste elle-même.
- **Tension identifiée et résolue** : rendre l'option de lancement pilotée par le manifeste la rendrait dépendante de l'hébergement (qui n'existe pas encore) — ce qui annulerait la correction précédente (review finale du sous-projet #2) qui gardait volontairement ce check *indépendant* du manifeste pour rester observable. **Résolution : approche hybride** — `SteamEnvironment` garde une valeur par défaut codée en dur (repli), utilisée tant que le manifeste ne fournit rien ; le manifeste prend le dessus automatiquement dès qu'il est disponible.

## Architecture

### Modèles

`JavaModManifest` (`Models/`) gagne un champ, désérialisé en liste vide si absent du JSON (même pattern défensif que `Files`, déjà corrigé) :

```csharp
public record JavaModManifest(IReadOnlyList<JavaFileEntry> Files, IReadOnlyList<string> RequiredLaunchOptions);
```

`JavaModInfo` (`Models/`) gagne un champ pour que le message affiché au joueur reflète toujours ce qui a réellement été vérifié, pas une constante figée :

```csharp
public record JavaModInfo(bool LaunchOptionConfigured, IReadOnlyList<string> RequiredLaunchOptions, IReadOnlyList<JavaFileStatus> Files);
```

### `SteamLaunchOptionInspector` (`Logic/`) — signature généralisée à une liste

`IsLaunchOptionConfigured(string steamPath, string appId, string requiredOption)` devient `AreLaunchOptionsConfigured(string steamPath, string appId, IReadOnlyList<string> requiredOptions)` : lit `LaunchOptions` une seule fois, vérifie que **chaque** option de la liste y est présente (`.Contains()` par option, ordre indifférent dans la chaîne). Liste vide → `true` (rien à vérifier ; ne devrait plus se produire une fois `SteamEnvironment` en place avec son repli, mais garde la fonction pure sûre indépendamment de l'appelant). Même comportement défensif qu'aujourd'hui sur toute erreur de lecture/parsing (dégrade vers `false`, jamais d'exception).

### `ISteamEnvironment` / `SteamEnvironment` — repli hybride

```csharp
Task<bool> IsJavaAgentLaunchOptionConfiguredAsync(IReadOnlyList<string> requiredOptions);
```

`SteamEnvironment` garde une constante `DefaultRequiredLaunchOption` (placeholder mod VOIP, remplace l'actuelle `RequiredLaunchOption`). Implémentation :

```csharp
public Task<bool> IsJavaAgentLaunchOptionConfiguredAsync(IReadOnlyList<string> requiredOptions)
{
    var effective = requiredOptions.Count > 0 ? requiredOptions : new[] { DefaultRequiredLaunchOption };
    return Task.FromResult(_steamPath is not null
        && SteamLaunchOptionInspector.AreLaunchOptionsConfigured(_steamPath, AppId, effective));
}
```

`FakeSteamEnvironment` reçoit la même signature, retourne toujours `true` (comportement Fake "tout passe" déjà établi, indépendant de la liste passée).

### `JavaModService.GetStatusAsync()` — ordre inversé

Le manifeste doit être récupéré **avant** de vérifier l'option de lancement (aujourd'hui c'est l'inverse). Nouvel ordre :

1. Récupérer le manifeste (`_manifestFetcher.FetchAsync()`).
2. `requiredOptions = manifest?.RequiredLaunchOptions ?? []` (liste vide si pas de manifeste → `SteamEnvironment` retombe sur son défaut).
3. `launchOptionConfigured = await _steamEnvironment.IsJavaAgentLaunchOptionConfiguredAsync(requiredOptions)`.
4. Récupérer `installPath` ; si `null` ou manifeste `null` → `JavaModInfo(launchOptionConfigured, requiredOptions, Files: [])`.
5. Sinon, fichiers via `JavaFileInspector.GetFileStatuses` comme aujourd'hui.

Le `try/catch` global déjà en place (ajouté lors de la review finale du sous-projet Velopack) couvre aussi ce nouveau chemin — dégrade vers `JavaModInfo(false, [], [])` sur toute exception, aucun changement de robustesse nécessaire.

### `JavaModEvaluator` — message dynamique

Supprime sa constante `RequiredLaunchOption` dupliquée ; construit le message d'échec à partir de `info.RequiredLaunchOptions` (jointes par espace, cohérent avec le format d'une chaîne d'options de lancement Steam) :

```csharp
"Option de lancement Steam manquante pour l'agent Java. Ajoutez ceci aux options de " +
$"lancement du jeu (Steam > clic droit sur Project Zomboid > Propriétés) :\n{string.Join(" ", info.RequiredLaunchOptions)}"
```

## Flux de données

**Aujourd'hui (sans hébergement réel)** : manifeste `null` → `requiredOptions = []` → `SteamEnvironment` retombe sur `DefaultRequiredLaunchOption` (placeholder mod VOIP) → check reste observable exactement comme avant la régression identifiée, mais avec la bonne valeur cette fois. Le message affiché au joueur montre ce placeholder.

**Une fois l'hébergement du manifeste en place** : `RequiredLaunchOptions` vient du JSON distant → prend le dessus automatiquement sur le défaut codé en dur, sans nouveau changement de code C#. Si un futur mod exige plusieurs options simultanément (ex. VOIP + ZombieBuddy), il suffit de lister les deux dans le manifeste.

## Gestion des erreurs

- `SteamLaunchOptionInspector.AreLaunchOptionsConfigured` : identique au comportement actuel, généralisé à une liste — jamais d'exception, dégrade vers `false`.
- `JavaModService.GetStatusAsync()` : aucun nouveau cas d'erreur, le `try/catch` global existant couvre le nouveau flux.
- `RepairAsync` : non affecté, ne touche pas aux options de lancement (déjà décidé : jamais d'écriture dans `localconfig.vdf`).

## Tests

- `SteamLaunchOptionInspectorTests` : les 8 tests existants migrent vers une liste d'une option (comportement identique). Nouveaux cas : plusieurs options toutes présentes → `true` ; une option manquante parmi plusieurs → `false` ; liste vide → `true`.
- `JavaModEvaluatorTests` : nouveau cas vérifiant que le message contient la valeur de `info.RequiredLaunchOptions` passée en paramètre, pas une constante interne à `JavaModEvaluator`.
- `JavaModManifestFetcherTests` : nouveau cas où `requiredLaunchOptions` est absent du JSON → désérialisé en liste vide, pas `null`.
- Toujours aucun test dédié pour `SteamEnvironment`/`JavaModService` (orchestration Windows/réseau) — convention déjà établie.

## Hors-scope (explicitement)

- Valeur réelle de l'option de lancement du mod VOIP — reste un placeholder jusqu'à ce que `GlasVoipMod` fixe un nom/version de release stable.
- Hébergement réel du manifeste — toujours un placeholder (sous-projet #4, non planifié).
- Tout changement dans le dépôt `GlasVoipMod` lui-même.
- Écriture automatique de `localconfig.vdf` — jamais, décision déjà actée au sous-projet #1.
