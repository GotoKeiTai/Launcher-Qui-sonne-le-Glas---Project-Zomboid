using System.Linq;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class WorkshopEvaluator
{
    public static CheckResult Evaluate(WorkshopStatus status)
    {
        var missingCount = status.RequiredIds.Except(status.InstalledIds).Count();

        if (missingCount == 0)
        {
            return new CheckResult("Mods Workshop synchronisés", CheckStatus.Passed, "Tous les mods requis sont installés.");
        }

        return new CheckResult(
            "Mods Workshop manquants",
            CheckStatus.Failed,
            $"{missingCount} mod(s) Workshop manquant(s).");
    }

    public static string GetCollectionSubscribeUrl(string collectionId) =>
        $"steam://url/CommunityFilePage/{collectionId}";
}
