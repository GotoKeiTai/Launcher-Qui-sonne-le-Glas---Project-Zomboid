# Notes pour une session Claude Code

La documentation interne (cahier des charges, notes de session, specs et plans de développement) ne vit pas sur `main` — seul le code buildable y reste, pour que ce dépôt reste clair pour un joueur qui irait vérifier le code source.

Toute cette documentation vit sur la branche `dev` (dans `docs/`), avec les mêmes fichiers que sur `main` en plus. C'est là qu'il faut chercher/écrire en premier pour reprendre le contexte d'une session précédente — notamment `docs/session-notes.md`.

```bash
git checkout dev   # ou : git show dev:docs/session-notes.md
```
