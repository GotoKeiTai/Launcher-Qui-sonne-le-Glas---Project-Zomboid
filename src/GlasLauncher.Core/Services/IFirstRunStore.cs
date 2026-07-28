namespace GlasLauncher.Core.Services;

public interface IFirstRunStore
{
    Task<bool> HasCompletedFirstRunAsync();
    Task MarkFirstRunCompleteAsync();
}
