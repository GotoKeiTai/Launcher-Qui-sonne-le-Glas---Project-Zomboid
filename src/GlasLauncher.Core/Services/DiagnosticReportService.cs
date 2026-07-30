using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public class DiagnosticReportService : IDiagnosticReportService
{
    private readonly ISteamEnvironment _steamEnvironment;
    private readonly IServerInfoService _serverInfoService;
    private readonly IJavaModService _javaModService;
    private readonly IUpdateService _updateService;
    private readonly ILauncherLogger _logger;

    public DiagnosticReportService(
        ISteamEnvironment steamEnvironment,
        IServerInfoService serverInfoService,
        IJavaModService javaModService,
        IUpdateService updateService,
        ILauncherLogger logger)
    {
        _steamEnvironment = steamEnvironment;
        _serverInfoService = serverInfoService;
        _javaModService = javaModService;
        _updateService = updateService;
        _logger = logger;
    }

    public async Task<string> GenerateAsync()
    {
        string? zipPath = null;
        try
        {
            var detectedVersion = await _steamEnvironment.GetInstalledGameVersionAsync();
            var requiredVersion = await _serverInfoService.GetGameVersionRequirementAsync();
            var javaModInfo = await _javaModService.GetStatusAsync();
            var installPath = await _steamEnvironment.GetGameInstallPathAsync();
            var workshopStatus = await _steamEnvironment.GetWorkshopStatusAsync(
                WorkshopRequirement.RequiredIds, WorkshopRequirement.CollectionId);

            var fileHashes = javaModInfo.Files
                .Select(f => new JavaModFileHash(f.FileName, TryComputeSha256(installPath, f.FileName)))
                .ToList();

            var snapshot = new DiagnosticSnapshot(
                LauncherVersion: _updateService.GetCurrentVersion(),
                WindowsDescription: RuntimeInformation.OSDescription,
                DetectedGameVersion: detectedVersion,
                RequiredGameVersion: requiredVersion,
                InstallPath: installPath,
                JavaModInfo: javaModInfo,
                JavaModFileHashes: fileHashes,
                WorkshopStatus: workshopStatus,
                GeneratedAtLocal: DateTime.Now);

            var manifestText = DiagnosticManifestBuilder.Build(snapshot);

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            zipPath = Path.Combine(desktopPath, $"GlasLauncher-diagnostic-{DateTime.Now:yyyy-MM-dd-HHmmss}.zip");

            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var manifestEntry = zip.CreateEntry("manifest.txt");
                await using (var writer = new StreamWriter(manifestEntry.Open()))
                {
                    await writer.WriteAsync(manifestText);
                }

                if (_logger.CurrentLogFilePath is not null && File.Exists(_logger.CurrentLogFilePath))
                {
                    zip.CreateEntryFromFile(_logger.CurrentLogFilePath, "launcher.log");
                }

                var pzLogsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid", "Logs");
                if (Directory.Exists(pzLogsPath))
                {
                    var cutoff = DateTime.UtcNow.AddDays(-3);
                    foreach (var file in Directory.GetFiles(pzLogsPath))
                    {
                        if (File.GetLastWriteTimeUtc(file) >= cutoff)
                        {
                            zip.CreateEntryFromFile(file, $"projectzomboid-logs/{Path.GetFileName(file)}");
                        }
                    }
                }
            }

            _logger.Info($"Rapport de diagnostic généré : {zipPath}");
            return zipPath;
        }
        catch (Exception ex)
        {
            _logger.Error("Échec de la génération du rapport de diagnostic", ex);
            try
            {
                if (zipPath is not null && File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }
            catch (Exception)
            {
                // Best-effort cleanup — never let this mask the real failure below.
            }
            throw new InvalidOperationException("Impossible de générer le rapport de diagnostic.", ex);
        }
    }

    private static string? TryComputeSha256(string? installPath, string fileName)
    {
        if (installPath is null || !SafeFilePath.TryResolve(installPath, fileName, out var filePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
