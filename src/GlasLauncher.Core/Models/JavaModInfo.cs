namespace GlasLauncher.Core.Models;

public record JavaModInfo(bool LaunchOptionConfigured, IReadOnlyList<JavaFileStatus> Files);
