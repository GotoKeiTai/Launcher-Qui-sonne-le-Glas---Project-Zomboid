namespace GlasLauncher.Core.Services;

public interface IDiagnosticReportService
{
    Task<string> GenerateAsync();
}
