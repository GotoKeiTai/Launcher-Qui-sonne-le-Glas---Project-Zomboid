namespace GlasLauncher.Core.Models;

public record UpdateInfo(
    string CurrentVersion,
    string LatestVersion,
    IReadOnlyList<string> ChangelogEntries);
