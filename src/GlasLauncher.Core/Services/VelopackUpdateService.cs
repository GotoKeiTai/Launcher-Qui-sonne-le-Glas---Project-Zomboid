using GlasLauncher.Core.Logic;
using Velopack;
using Velopack.Sources;

namespace GlasLauncher.Core.Services;

public class VelopackUpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
    private Velopack.UpdateInfo? _pendingUpdate;

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
}
