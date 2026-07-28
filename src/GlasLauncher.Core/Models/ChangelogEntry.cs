namespace GlasLauncher.Core.Models;

public record ChangelogEntry(string Version, DateOnly Date, IReadOnlyList<string> Changes);
