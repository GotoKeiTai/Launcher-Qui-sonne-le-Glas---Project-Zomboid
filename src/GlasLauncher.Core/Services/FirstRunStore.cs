using System.Text.Json;

namespace GlasLauncher.Core.Services;

public class FirstRunStore : IFirstRunStore
{
    private readonly string _filePath;

    public FirstRunStore(string filePath)
    {
        _filePath = filePath;
    }

    public static IFirstRunStore CreateDefault() =>
        new FirstRunStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GlasLauncher",
            "state.json"));

    public async Task<bool> HasCompletedFirstRunAsync()
    {
        if (!File.Exists(_filePath))
        {
            return false;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var state = await JsonSerializer.DeserializeAsync<FirstRunState>(stream);
            return state?.FirstRunCompleted ?? false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task MarkFirstRunCompleteAsync()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, new FirstRunState(true));
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private record FirstRunState(bool FirstRunCompleted);
}
