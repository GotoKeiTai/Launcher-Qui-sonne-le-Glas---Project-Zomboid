using System.Text;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class DiagnosticManifestBuilder
{
    public static string Build(DiagnosticSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Rapport de diagnostic — Glas Launcher ===");
        sb.AppendLine($"Généré le : {snapshot.GeneratedAtLocal:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"Launcher : {snapshot.LauncherVersion}");
        sb.AppendLine($"Windows : {snapshot.WindowsDescription}");
        sb.AppendLine();
        sb.AppendLine("--- Project Zomboid ---");
        sb.AppendLine($"Buildid détecté : {snapshot.DetectedGameVersion?.BuildId ?? "introuvable"}");
        sb.AppendLine($"Branche détectée : {snapshot.DetectedGameVersion?.Branch ?? "introuvable"}");
        sb.AppendLine($"Buildid requis : {snapshot.RequiredGameVersion.RequiredBuildId}");
        sb.AppendLine($"Branche requise : {snapshot.RequiredGameVersion.RequiredBranch}");
        sb.AppendLine($"Version affichée requise : {snapshot.RequiredGameVersion.DisplayVersion}");
        sb.AppendLine($"Dossier d'installation : {snapshot.InstallPath ?? "introuvable"}");
        sb.AppendLine();
        sb.AppendLine("--- Mod Java ---");
        sb.AppendLine($"Option de lancement configurée : {(snapshot.JavaModInfo.LaunchOptionConfigured ? "oui" : "non")}");
        sb.AppendLine($"Option(s) requise(s) : {string.Join(" ", snapshot.JavaModInfo.RequiredLaunchOptions)}");
        if (snapshot.JavaModInfo.Files.Count == 0)
        {
            sb.AppendLine("Aucun fichier de mod Java détecté.");
        }
        foreach (var file in snapshot.JavaModInfo.Files)
        {
            var hash = snapshot.JavaModFileHashes.FirstOrDefault(h => h.FileName == file.FileName)?.Sha256 ?? "indisponible";
            sb.AppendLine($"{file.FileName} :");
            var installedLabel = file.InstalledVersion ?? (hash == "indisponible" ? "non installé" : "installée mais non conforme");
            sb.AppendLine($"  Version installée : {installedLabel}");
            sb.AppendLine($"  Version requise : {file.RequiredVersion}");
            sb.AppendLine($"  À jour : {(file.IsUpToDate ? "oui" : "non")}");
            sb.AppendLine($"  SHA-256 : {hash}");
        }
        sb.AppendLine();
        sb.AppendLine("--- Mods Workshop ---");
        sb.AppendLine($"Requis : {string.Join(", ", snapshot.WorkshopStatus.RequiredIds)}");
        sb.AppendLine($"Détectés : {string.Join(", ", snapshot.WorkshopStatus.InstalledIds)}");
        var missing = snapshot.WorkshopStatus.RequiredIds.Except(snapshot.WorkshopStatus.InstalledIds).ToList();
        sb.AppendLine($"Manquants : {(missing.Count == 0 ? "aucun" : string.Join(", ", missing))}");

        return sb.ToString();
    }
}
