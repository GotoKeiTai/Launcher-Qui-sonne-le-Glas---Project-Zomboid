namespace GlasLauncher.Core.Services;

public interface ILauncherLogger
{
    string? CurrentLogFilePath { get; }
    void Info(string message);
    void Error(string message, Exception? exception = null);
}
