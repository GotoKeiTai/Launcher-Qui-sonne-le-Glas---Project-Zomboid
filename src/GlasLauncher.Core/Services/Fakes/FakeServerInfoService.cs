using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeServerInfoService : IServerInfoService
{
    // No real server connectivity exists yet (see docs/session-notes.md, sub-project #4:
    // a real IServerInfoService fetching server.json/mods.json from static hosting) —
    // report honestly that the server is unreachable rather than faking an online status.
    public Task<ServerInfo> GetServerInfoAsync() =>
        Task.FromResult(new ServerInfo(Status: "offline", Players: 0, MaxPlayers: 0, PingMs: 0));

    // legacy41 is still receiving hotfixes as of this writing — this buildid/version pair
    // will go stale again the next time the server updates. Until the real IServerInfoService
    // (sub-project #4) fetches this from hosting, it must be updated here by hand.
    public Task<GameVersionRequirement> GetGameVersionRequirementAsync() =>
        Task.FromResult(new GameVersionRequirement(
            RequiredBuildId: "24432948",
            RequiredBranch: "legacy41",
            DisplayVersion: "41.78.20"));

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
}
