namespace GlasLauncher.Core.Models;

public record DiagnosticSnapshot(
    string LauncherVersion,
    string WindowsDescription,
    GameVersionInfo? DetectedGameVersion,
    GameVersionRequirement RequiredGameVersion,
    JavaModInfo JavaModInfo,
    IReadOnlyList<JavaModFileHash> JavaModFileHashes,
    WorkshopStatus WorkshopStatus,
    DateTime GeneratedAtLocal);
