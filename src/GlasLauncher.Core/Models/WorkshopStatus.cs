namespace GlasLauncher.Core.Models;

public record WorkshopStatus(
    IReadOnlyList<string> InstalledIds,
    IReadOnlyList<string> RequiredIds,
    string CollectionId);
