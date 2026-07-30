namespace GlasLauncher.Core.Logic;

/// Guards against a compromised remote manifest turning a file name into a path-traversal
/// write outside the game install directory (e.g. "..\..\Startup\evil.exe").
public static class SafeFilePath
{
    public static bool TryResolve(string baseDirectory, string fileName, out string resolvedPath)
    {
        resolvedPath = "";

        if (string.IsNullOrWhiteSpace(fileName) || Path.IsPathRooted(fileName))
        {
            return false;
        }

        try
        {
            var basePath = Path.GetFullPath(baseDirectory);
            var candidate = Path.GetFullPath(Path.Combine(basePath, fileName));

            var baseWithSeparator = basePath.EndsWith(Path.DirectorySeparatorChar)
                ? basePath
                : basePath + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            resolvedPath = candidate;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
