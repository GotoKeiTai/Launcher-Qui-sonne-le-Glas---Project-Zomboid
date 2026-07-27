using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class GameVersionEvaluator
{
    private const string CheckName = "Version conforme";

    public static CheckResult Evaluate(GameVersionInfo detected, GameVersionRequirement required)
    {
        if (detected.Branch != required.RequiredBranch)
        {
            return new CheckResult(
                CheckName,
                CheckStatus.Failed,
                $"Branche Steam incorrecte : \"{detected.Branch}\" détectée, \"{required.RequiredBranch}\" attendue.");
        }

        if (detected.BuildId != required.RequiredBuildId)
        {
            return new CheckResult(
                CheckName,
                CheckStatus.Failed,
                $"Version du jeu non conforme (buildid {detected.BuildId} détecté, {required.RequiredBuildId} attendu).");
        }

        return new CheckResult(CheckName, CheckStatus.Passed, required.DisplayVersion);
    }
}
