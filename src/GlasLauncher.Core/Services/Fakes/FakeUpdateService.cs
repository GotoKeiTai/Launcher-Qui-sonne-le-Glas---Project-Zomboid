using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeUpdateService : IUpdateService
{
    // No update available: this Fake is used on non-Windows platforms only, where
    // VelopackUpdateService (the real IUpdateService) doesn't run — purely a dev-UI
    // convenience, not a placeholder awaiting real functionality.
    public Task<UpdateInfo?> CheckForUpdateAsync() => Task.FromResult<UpdateInfo?>(null);

    public async Task ApplyUpdateAsync()
    {
        await Task.Delay(500);
    }

    public string GetCurrentVersion() => "0.1.0-dev";

    public Task<IReadOnlyList<ChangelogEntry>> GetChangelogAsync() =>
        Task.FromResult<IReadOnlyList<ChangelogEntry>>(new List<ChangelogEntry>
        {
            new("0.1.1", new DateOnly(2026, 7, 30), new List<string>
            {
                "Correction de l'affichage de la version installée du jeu.",
                "Le bouton Jouer n'est plus bloqué par les mods Workshop manquants."
            }),
            new("0.1.0", new DateOnly(2026, 7, 20), new List<string>
            {
                "Version initiale du launcher.",
                "Vérification automatique de la version du jeu et des mods Workshop requis.",
                "Abonnement en un clic à la collection Workshop du serveur."
            })
        });
}
