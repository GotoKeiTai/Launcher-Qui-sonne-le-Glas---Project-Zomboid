using System.Net.Http;
using System.Security.Cryptography;
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public class JavaModService : IJavaModService
{
    // Fallback used only when the remote manifest fetch fails or omits RequiredLaunchOptions
    // (e.g. network unavailable). Mirrors GlasVoipMod's published manifest value, so the check
    // stays correct even when the manifest can't be reached.
    private const string DefaultRequiredLaunchOption = "-javaagent:GlasVoipMod.jar";

    private readonly ISteamEnvironment _steamEnvironment;
    private readonly JavaModManifestFetcher _manifestFetcher;

    public JavaModService(ISteamEnvironment steamEnvironment, JavaModManifestFetcher manifestFetcher)
    {
        _steamEnvironment = steamEnvironment;
        _manifestFetcher = manifestFetcher;
    }

    public async Task<JavaModInfo> GetStatusAsync()
    {
        IReadOnlyList<string> requiredLaunchOptions = Array.Empty<string>();
        var launchOptionConfigured = false;

        try
        {
            var manifest = await _manifestFetcher.FetchAsync();
            var manifestOptions = manifest?.RequiredLaunchOptions;
            requiredLaunchOptions = manifestOptions is { Count: > 0 } ? manifestOptions : new[] { DefaultRequiredLaunchOption };
            launchOptionConfigured = await _steamEnvironment.IsJavaAgentLaunchOptionConfiguredAsync(requiredLaunchOptions);

            var installPath = await _steamEnvironment.GetGameInstallPathAsync();
            if (installPath is null || manifest is null)
            {
                return new JavaModInfo(launchOptionConfigured, requiredLaunchOptions, Array.Empty<JavaFileStatus>());
            }

            var files = JavaFileInspector.GetFileStatuses(installPath, manifest);
            return new JavaModInfo(launchOptionConfigured, requiredLaunchOptions, files);
        }
        catch (Exception)
        {
            return new JavaModInfo(launchOptionConfigured, requiredLaunchOptions, Array.Empty<JavaFileStatus>());
        }
    }

    public async Task RepairAsync(IProgress<RepairProgress> progress)
    {
        var installPath = await _steamEnvironment.GetGameInstallPathAsync()
            ?? throw new InvalidOperationException("Dossier d'installation de Project Zomboid introuvable.");

        var manifest = await _manifestFetcher.FetchAsync()
            ?? throw new InvalidOperationException("Impossible de récupérer le manifeste du mod Java.");

        var statuses = JavaFileInspector.GetFileStatuses(installPath, manifest);
        var outdatedEntries = manifest.Files
            .Where(entry => statuses.First(s => s.FileName == entry.FileName).IsUpToDate == false)
            .ToList();

        progress.Report(new RepairProgress(RepairStepNames.OldVersionRemoved, 10));

        progress.Report(new RepairProgress(RepairStepNames.DownloadingJavaMod, 30));

        foreach (var entry in outdatedEntries)
        {
            if (!Uri.TryCreate(entry.DownloadUrl, UriKind.Absolute, out var downloadUri)
                || downloadUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"URL de téléchargement invalide pour {entry.FileName}.");
            }
        }

        using var httpClient = new HttpClient();
        var totalBytes = 0L;
        foreach (var entry in outdatedEntries)
        {
            using var headResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, entry.DownloadUrl));
            totalBytes += headResponse.Content.Headers.ContentLength ?? 0;
        }

        var tempFiles = new Dictionary<string, string>();
        var downloadedBytes = 0L;
        for (var i = 0; i < outdatedEntries.Count; i++)
        {
            var entry = outdatedEntries[i];
            var tempPath = Path.GetTempFileName();
            await using (var responseStream = await httpClient.GetStreamAsync(entry.DownloadUrl))
            await using (var fileStream = File.Create(tempPath))
            {
                await responseStream.CopyToAsync(fileStream);
            }

            tempFiles[entry.FileName] = tempPath;
            downloadedBytes += new FileInfo(tempPath).Length;

            progress.Report(new RepairProgress(
                RepairStepNames.DownloadingJavaMod,
                PercentComplete: 30 + (int)(30.0 * (i + 1) / outdatedEntries.Count),
                MegabytesDownloaded: downloadedBytes / 1024.0 / 1024.0,
                MegabytesTotal: totalBytes / 1024.0 / 1024.0));
        }

        progress.Report(new RepairProgress(RepairStepNames.VerifyingIntegrity, 85));
        foreach (var entry in outdatedEntries)
        {
            var tempPath = tempFiles[entry.FileName];
            string localSha256;
            await using (var stream = File.OpenRead(tempPath))
            {
                localSha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            }

            if (!string.Equals(localSha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Intégrité invalide pour {entry.FileName}.");
            }
        }

        progress.Report(new RepairProgress(RepairStepNames.Installing, 100));
        foreach (var entry in outdatedEntries)
        {
            if (!SafeFilePath.TryResolve(installPath, entry.FileName, out var destinationPath))
            {
                throw new InvalidOperationException($"Nom de fichier invalide pour {entry.FileName}.");
            }

            var tempPath = tempFiles[entry.FileName];
            File.Move(tempPath, destinationPath, overwrite: true);
        }
    }
}
