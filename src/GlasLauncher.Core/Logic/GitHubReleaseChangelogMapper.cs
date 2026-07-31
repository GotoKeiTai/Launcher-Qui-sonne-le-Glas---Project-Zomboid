using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class GitHubReleaseChangelogMapper
{
    public static IReadOnlyList<ChangelogEntry> Map(IReadOnlyList<GitHubReleaseDto> releases) =>
        releases
            .Select(r => new ChangelogEntry(
                Version: r.TagName.TrimStart('v'),
                Date: DateOnly.FromDateTime(r.PublishedAt.UtcDateTime),
                Changes: UpdateNotesParser.Parse(r.Body ?? "")))
            .ToList();
}
