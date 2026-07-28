using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeServerInfoService : IServerInfoService
{
    public Task<ServerInfo> GetServerInfoAsync() =>
        Task.FromResult(new ServerInfo(Status: "online", Players: 14, MaxPlayers: 32, PingMs: 32));

    public Task<GameVersionRequirement> GetGameVersionRequirementAsync() =>
        Task.FromResult(new GameVersionRequirement(
            RequiredBuildId: "18234567",
            RequiredBranch: "public",
            DisplayVersion: "41.78.16"));

    public Task<IReadOnlyList<NewsItem>> GetNewsAsync() =>
        Task.FromResult<IReadOnlyList<NewsItem>>(new List<NewsItem>
        {
            new("Mise à jour 41.78.16 déployée sur le serveur", new DateOnly(2026, 7, 24),
                "Le serveur a été mis à jour vers la build 41.78.16 de Project Zomboid."),
            new("Session communautaire ce samedi soir", new DateOnly(2026, 7, 21),
                "Rejoignez-nous samedi à partir de 20h pour une session groupée sur le serveur."),
            new("Nouveau mod Workshop ajouté à la liste requise", new DateOnly(2026, 7, 15),
                "Le mod \"Filibuster Rhymes' Vehicle Pack\" a été ajouté à la liste des mods obligatoires.")
        });

    public Task<IReadOnlyList<ChangelogEntry>> GetChangelogAsync() =>
        Task.FromResult<IReadOnlyList<ChangelogEntry>>(new List<ChangelogEntry>
        {
            new("v0.1.0", new DateOnly(2026, 7, 20), new List<string>
            {
                "Version initiale du launcher.",
                "Vérification automatique de la version du jeu et des mods Workshop requis.",
                "Abonnement en un clic à la collection Workshop du serveur."
            })
        });
}
