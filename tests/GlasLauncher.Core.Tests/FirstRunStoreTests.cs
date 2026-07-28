using GlasLauncher.Core.Services;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class FirstRunStoreTests
{
    [Fact]
    public async Task HasCompletedFirstRunAsync_NoFileYet_ReturnsFalse()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "state.json");
        var store = new FirstRunStore(path);

        var result = await store.HasCompletedFirstRunAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task MarkFirstRunCompleteAsync_ThenHasCompletedFirstRunAsync_ReturnsTrue()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "state.json");
        var store = new FirstRunStore(path);

        await store.MarkFirstRunCompleteAsync();
        var result = await store.HasCompletedFirstRunAsync();

        Assert.True(result);

        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task HasCompletedFirstRunAsync_CorruptedFile_ReturnsFalse()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "state.json");
        await File.WriteAllTextAsync(path, "{not valid json");
        var store = new FirstRunStore(path);

        var result = await store.HasCompletedFirstRunAsync();

        Assert.False(result);

        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task MarkFirstRunCompleteAsync_CalledTwice_StillReturnsTrue()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "state.json");
        var store = new FirstRunStore(path);

        await store.MarkFirstRunCompleteAsync();
        await store.MarkFirstRunCompleteAsync();
        var result = await store.HasCompletedFirstRunAsync();

        Assert.True(result);

        Directory.Delete(directory, recursive: true);
    }
}
