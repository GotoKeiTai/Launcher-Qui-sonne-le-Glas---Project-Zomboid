using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Velopack;
using Velopack.Sources;

namespace GlasLauncher.Core.Services;

public class VelopackUpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid";
    private const string ReleasesApiUrl = "https://api.github.com/repos/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid/releases";

    private static readonly JsonSerializerOptions ReleasesApiOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
    private readonly HttpClient _httpClient = CreateChangelogHttpClient();
    private Velopack.UpdateInfo? _pendingUpdate;

    private static HttpClient CreateChangelogHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub's API rejects requests with no User-Agent header.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GlasLauncher");
        return client;
    }

    public async Task<GlasLauncher.Core.Models.UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            _pendingUpdate = await _manager.CheckForUpdatesAsync();
            if (_pendingUpdate is null)
            {
                return null;
            }

            return new GlasLauncher.Core.Models.UpdateInfo(
                CurrentVersion: GetCurrentVersion(),
                LatestVersion: _pendingUpdate.TargetFullRelease.Version.ToString(),
                ChangelogEntries: UpdateNotesParser.Parse(_pendingUpdate.TargetFullRelease.NotesMarkdown ?? ""));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task ApplyUpdateAsync()
    {
        if (_pendingUpdate is null)
        {
            throw new InvalidOperationException("Aucune mise à jour en attente.");
        }

        await _manager.DownloadUpdatesAsync(_pendingUpdate);
        _manager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
    }

    public string GetCurrentVersion()
    {
        try
        {
            return _manager.CurrentVersion?.ToString() ?? "dev";
        }
        catch (Exception)
        {
            return "dev";
        }
    }

    public async Task<IReadOnlyList<ChangelogEntry>> GetChangelogAsync()
    {
        try
        {
            var releases = await _httpClient.GetFromJsonAsync<List<GitHubReleaseDto>>(ReleasesApiUrl, ReleasesApiOptions);
            return releases is null ? Array.Empty<ChangelogEntry>() : GitHubReleaseChangelogMapper.Map(releases);
        }
        catch (Exception)
        {
            return Array.Empty<ChangelogEntry>();
        }
    }
}
