using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public interface IServerInfoService
{
    Task<ServerInfo> GetServerInfoAsync();
    Task<GameVersionRequirement> GetGameVersionRequirementAsync();
    Task<IReadOnlyList<NewsItem>> GetNewsAsync();
}
