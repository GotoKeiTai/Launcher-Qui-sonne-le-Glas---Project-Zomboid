using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public interface IJavaModService
{
    Task<JavaModInfo> GetStatusAsync();
    Task RepairAsync(IProgress<RepairProgress> progress);
}
