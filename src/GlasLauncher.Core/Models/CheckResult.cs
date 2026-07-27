namespace GlasLauncher.Core.Models;

public enum CheckStatus
{
    Passed,
    Failed
}

public record CheckResult(string Name, CheckStatus Status, string Message);
