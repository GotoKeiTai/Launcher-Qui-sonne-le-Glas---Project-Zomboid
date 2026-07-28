using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeJavaModService : IJavaModService
{
    public Task<JavaModInfo> GetStatusAsync() =>
        Task.FromResult(new JavaModInfo(
            InstalledVersion: "1.0.0",
            InstalledSha256: "abc123",
            RequiredVersion: "1.0.0",
            RequiredSha256: "abc123"));

    public async Task RepairAsync(IProgress<RepairProgress> progress)
    {
        progress.Report(new RepairProgress("Ancienne version supprimée", 10));
        await Task.Delay(300);
        progress.Report(new RepairProgress("Téléchargement du mod Java", 30, MegabytesDownloaded: 1.5, MegabytesTotal: 5.1));
        await Task.Delay(200);
        progress.Report(new RepairProgress("Téléchargement du mod Java", 60, MegabytesDownloaded: 3.4, MegabytesTotal: 5.1));
        await Task.Delay(200);
        progress.Report(new RepairProgress("Vérification de l'intégrité (SHA-256)", 85));
        await Task.Delay(200);
        progress.Report(new RepairProgress("Installation", 100));
    }
}
