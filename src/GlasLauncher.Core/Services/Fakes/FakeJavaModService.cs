using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeJavaModService : IJavaModService
{
    public Task<JavaModInfo> GetStatusAsync() =>
        Task.FromResult(new JavaModInfo(
            LaunchOptionConfigured: true,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: new List<JavaFileStatus>
            {
                new("GlasVoipMod.jar", InstalledVersion: "0.1.0", RequiredVersion: "0.1.0", IsUpToDate: true)
            }));

    public async Task RepairAsync(IProgress<RepairProgress> progress)
    {
        progress.Report(new RepairProgress(RepairStepNames.OldVersionRemoved, 10));
        await Task.Delay(300);
        progress.Report(new RepairProgress(RepairStepNames.DownloadingJavaMod, 30, MegabytesDownloaded: 1.5, MegabytesTotal: 5.1));
        await Task.Delay(200);
        progress.Report(new RepairProgress(RepairStepNames.DownloadingJavaMod, 60, MegabytesDownloaded: 3.4, MegabytesTotal: 5.1));
        await Task.Delay(200);
        progress.Report(new RepairProgress(RepairStepNames.VerifyingIntegrity, 85));
        await Task.Delay(200);
        progress.Report(new RepairProgress(RepairStepNames.Installing, 100));
    }
}
