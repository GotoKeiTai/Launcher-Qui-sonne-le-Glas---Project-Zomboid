using System.Linq;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class JavaModEvaluator
{
    private const string CheckName = "Mod Java à jour";

    // Same literal as SteamEnvironment.RequiredLaunchOption — duplicated on purpose rather than
    // introducing a shared constants class, matching the project's existing AppId tradeoff.
    private const string RequiredLaunchOption = "-agentlib:zbNative --";

    public static CheckResult Evaluate(JavaModInfo info)
    {
        if (!info.LaunchOptionConfigured)
        {
            return new CheckResult(
                CheckName,
                CheckStatus.Failed,
                "Option de lancement Steam manquante pour l'agent Java. Ajoutez ceci aux options de " +
                $"lancement du jeu (Steam > clic droit sur Project Zomboid > Propriétés) :\n{RequiredLaunchOption}");
        }

        if (info.Files.Count == 0)
        {
            return new CheckResult(CheckName, CheckStatus.Failed, "Impossible de vérifier le mod Java.");
        }

        if (info.Files.Any(f => !f.IsUpToDate))
        {
            return new CheckResult(CheckName, CheckStatus.Failed, "Le mod Java n'est pas à jour.");
        }

        return new CheckResult(CheckName, CheckStatus.Passed, "Agent Java synchronisé.");
    }
}
