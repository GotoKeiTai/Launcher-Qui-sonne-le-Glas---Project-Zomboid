using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeUpdateService : IUpdateService
{
    public Task<UpdateInfo?> CheckForUpdateAsync() => Task.FromResult<UpdateInfo?>(new UpdateInfo(
        CurrentVersion: "v0.1.0",
        LatestVersion: "v0.2.0",
        ChangelogEntries: new[]
        {
            "Ajout du rapport de diagnostic (logs launcher + Project Zomboid)",
            "Détection améliorée des bibliothèques Steam multiples",
            "Correction d'un cas où le mod Java n'était pas réinstallé après une mise à jour du jeu"
        }));

    public async Task ApplyUpdateAsync()
    {
        await Task.Delay(500);
    }
}
