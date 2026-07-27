namespace GlasLauncher.Core.Models;

public record JavaModInfo(
    string? InstalledVersion,
    string? InstalledSha256,
    string RequiredVersion,
    string RequiredSha256);
