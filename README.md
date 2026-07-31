# Glas Launcher

Launcher Windows dédié au serveur Project Zomboid communautaire *Pour qui sonne le glas*. Automatise la préparation du jeu avant connexion (vérification Steam, version du jeu, mod Java, mods Workshop) — n'installe ni ne remplace Steam.

## Téléchargement

**[Télécharger la dernière version](https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid/releases/latest/download/GlasLauncher-win-Setup.exe)** — installation sans élévation administrateur, aucune manipulation manuelle.

## Statut

Fonctionnel, en bêta active. Voir les [releases](https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid/releases) pour l'historique des versions.

## Sécurité & vérification

### Qu'est-ce qu'une "signature de code" ?

C'est un certificat numérique qui prouve deux choses : qui a publié le logiciel, et que le fichier n'a pas été modifié depuis sa publication — un peu comme une carte d'identité pour un programme. Windows fait confiance aux éditeurs qui ont acheté et fait vérifier ce certificat par une autorité reconnue ; sans lui, Windows ne peut pas confirmer qui a créé le logiciel, d'où l'avertissement SmartScreen. ([Explication complète par Microsoft](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options))

### Pourquoi Glas Launcher n'est pas signé

Un certificat de signature de code coûte 60-225 $/an récurrents — disproportionné pour un projet communautaire d'une vingtaine de joueurs. Windows SmartScreen affichera donc probablement un avertissement "Éditeur inconnu" au premier lancement de l'installeur. **C'est normal pour un petit projet, pas un signe de danger.** Pour lancer l'installeur malgré l'avertissement : cliquez sur **"Informations complémentaires"** puis **"Exécuter quand même"**.

### Comment vérifier par vous-même (plutôt que de nous croire sur parole)

- **Code source entièrement public** — ce dépôt, y compris le [workflow qui construit et publie chaque release](.github/workflows/release.yml) (logs publics sur l'onglet Actions). Le dossier `tests/` que vous verrez dans le code contient des tests automatisés (des vérifications de qualité que les développeurs font tourner à chaque changement) — pas une "version d'essai" du launcher : la version que vous téléchargez est la version complète.
- **Somme de contrôle SHA-256** — publiée en tant que fichier `.sha256.txt` sur chaque [release](https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid/releases/latest), pour vérifier que l'installeur téléchargé n'a pas été altéré.
- **Scan VirusTotal** — un lien de rapport public est généré à chaque release (visible dans le résumé du build sur l'onglet Actions).

### Si VirusTotal affiche une alerte

Sur un exécutable non signé et peu diffusé comme celui-ci, il est normal qu'un scan VirusTotal montre parfois une alerte isolée d'un moteur heuristique/ML (ex. "score de malveillance élevé"), ou des règles comportementales génériques (ex. "création d'un exécutable détectée", qui se déclenche pour n'importe quel installeur). Ce n'est pas la même chose qu'une vraie détection de malware confirmée par plusieurs antivirus : le manque de signature et la faible diffusion sont justement deux des causes les plus fréquentes de faux positifs, documentées par la presse spécialisée ([PCWorld, sur VirusTotal et les faux positifs](https://www.pcworld.com/article/431848/virustotal-tackles-false-positive-malware-detections-plaguing-antivirus-and-software-vendors.html)). Regardez toujours le nombre total de moteurs qui détectent quelque chose, pas une seule alerte isolée.

## Stack technique

- C# / .NET 8
- Avalonia UI (interface multiplateforme, développée sur macOS, ciblant Windows)
- Velopack (packaging & auto-update)
- Distribution via GitHub Releases

## Licence

[MIT](LICENSE)
