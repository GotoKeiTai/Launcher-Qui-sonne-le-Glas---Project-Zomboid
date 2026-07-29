namespace GlasLauncher.Core.Models;

public record JavaFileStatus(string FileName, string? InstalledVersion, string RequiredVersion, bool IsUpToDate);
