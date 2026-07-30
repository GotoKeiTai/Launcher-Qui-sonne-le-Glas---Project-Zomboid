# Glas Launcher

Launcher Windows dédié au serveur Project Zomboid communautaire *Pour qui sonne le glas*. Automatise la préparation du jeu avant connexion (vérification Steam, version du jeu, mod Java, mods Workshop) — n'installe ni ne remplace Steam.

## Téléchargement

**[Télécharger la dernière version](https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid/releases/latest/download/GlasLauncher-win-Setup.exe)** — installation sans élévation administrateur, aucune manipulation manuelle.

## Statut

Fonctionnel, en bêta active. Voir les [releases](https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid/releases) pour l'historique des versions.

## Sécurité & vérification

Le launcher n'est **pas encore signé numériquement** (un certificat de signature de code coûte 60-225 $/an récurrents, disproportionné pour un projet communautaire à cette échelle) : Windows SmartScreen affichera probablement un avertissement "Éditeur inconnu" au premier lancement de l'installeur. C'est normal pour un petit projet, pas un signe de danger — voici comment vérifier par vous-même plutôt que de nous faire confiance sur parole :

- **Code source entièrement public** — ce dépôt, y compris le [workflow qui construit et publie chaque release](.github/workflows/release.yml) (logs publics sur l'onglet Actions).
- **Somme de contrôle SHA-256** — publiée en tant que fichier `.sha256.txt` sur chaque [release](https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid/releases/latest), pour vérifier que l'installeur téléchargé n'a pas été altéré.
- **Scan VirusTotal** — un lien de rapport public est généré à chaque release (visible dans le résumé du build sur l'onglet Actions).

Pour lancer l'installeur malgré l'avertissement : cliquez sur **"Informations complémentaires"** puis **"Exécuter quand même"**.

## Stack technique

- C# / .NET 8
- Avalonia UI (interface multiplateforme, développée sur macOS, ciblant Windows)
- Velopack (packaging & auto-update)
- Distribution via GitHub Releases

## Licence

[MIT](LICENSE)
