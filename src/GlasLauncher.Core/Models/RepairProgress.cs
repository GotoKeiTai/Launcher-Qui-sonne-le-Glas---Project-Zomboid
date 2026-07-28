namespace GlasLauncher.Core.Models;

public record RepairProgress(
    string StepName,
    int PercentComplete,
    double? MegabytesDownloaded = null,
    double? MegabytesTotal = null);
