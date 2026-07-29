namespace GlasLauncher.Core.Models;

public record JavaModManifest(IReadOnlyList<JavaFileEntry> Files, IReadOnlyList<string> RequiredLaunchOptions);
