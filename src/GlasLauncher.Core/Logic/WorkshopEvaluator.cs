using System.Linq;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class WorkshopEvaluator
{
    private const string CheckName = "Mods Workshop synchronisés";

    public static CheckResult Evaluate(WorkshopStatus status)
    {
        var missingCount = status.RequiredIds.Except(status.InstalledIds).Count();

        if (missingCount == 0)
        {
            return new CheckResult(CheckName, CheckStatus.Passed, "Tous les mods requis sont installés.");
        }

        return new CheckResult(
            CheckName,
            CheckStatus.Failed,
            $"{missingCount} mod(s) Workshop manquant(s).");
    }

    public static string GetCollectionSubscribeUrl(string collectionId) =>
        $"steam://url/CommunityFilePage/{collectionId}";
}
