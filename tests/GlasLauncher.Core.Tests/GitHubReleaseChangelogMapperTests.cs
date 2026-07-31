using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class GitHubReleaseChangelogMapperTests
{
    [Fact]
    public void Map_SingleRelease_StripsLeadingVAndParsesBulletPoints()
    {
        var releases = new List<GitHubReleaseDto>
        {
            new(
                TagName: "v0.1.12",
                PublishedAt: new DateTimeOffset(2026, 7, 31, 0, 26, 14, TimeSpan.Zero),
                Body: "- Correction de la modale de mise à jour\n- Amélioration du rapport de diagnostic")
        };

        var result = GitHubReleaseChangelogMapper.Map(releases);

        Assert.Single(result);
        Assert.Equal("0.1.12", result[0].Version);
        Assert.Equal(new DateOnly(2026, 7, 31), result[0].Date);
        Assert.Equal(
            new[] { "Correction de la modale de mise à jour", "Amélioration du rapport de diagnostic" },
            result[0].Changes);
    }

    [Fact]
    public void Map_MultipleReleases_PreservesGivenOrder()
    {
        var releases = new List<GitHubReleaseDto>
        {
            new("v0.1.2", new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero), "- B"),
            new("v0.1.1", new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero), "- A")
        };

        var result = GitHubReleaseChangelogMapper.Map(releases);

        Assert.Equal(2, result.Count);
        Assert.Equal("0.1.2", result[0].Version);
        Assert.Equal("0.1.1", result[1].Version);
    }

    [Fact]
    public void Map_NullBody_ReturnsEmptyChanges()
    {
        var releases = new List<GitHubReleaseDto>
        {
            new("v0.1.0", new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero), Body: null)
        };

        var result = GitHubReleaseChangelogMapper.Map(releases);

        Assert.Single(result);
        Assert.Empty(result[0].Changes);
    }
}
