namespace GlasLauncher.Core.Models;

public record GitHubReleaseDto(string TagName, DateTimeOffset PublishedAt, string? Body);
