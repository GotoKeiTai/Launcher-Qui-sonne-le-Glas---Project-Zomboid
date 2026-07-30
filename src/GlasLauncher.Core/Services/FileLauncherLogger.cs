namespace GlasLauncher.Core.Services;

public class FileLauncherLogger : ILauncherLogger
{
    private readonly string? _logFilePath;

    public FileLauncherLogger()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlasLauncher", "logs");
            Directory.CreateDirectory(dir);
            _logFilePath = Path.Combine(dir, $"session-{DateTime.Now:yyyy-MM-dd-HHmmss}.log");
        }
        catch (Exception)
        {
            _logFilePath = null;
        }
    }

    public string? CurrentLogFilePath => _logFilePath;

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} — {exception}");

    private void Write(string level, string message)
    {
        if (_logFilePath is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(_logFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Best-effort — jamais d'exception propagée pour un simple log.
        }
    }
}
