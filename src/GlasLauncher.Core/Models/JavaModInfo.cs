namespace GlasLauncher.Core.Models;

public record JavaModInfo(bool LaunchOptionConfigured, IReadOnlyList<string> RequiredLaunchOptions, IReadOnlyList<JavaFileStatus> Files);
