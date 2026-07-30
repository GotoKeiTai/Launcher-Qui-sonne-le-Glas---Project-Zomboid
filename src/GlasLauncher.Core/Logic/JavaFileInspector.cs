using System.Security.Cryptography;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class JavaFileInspector
{
    public static IReadOnlyList<JavaFileStatus> GetFileStatuses(string installPath, JavaModManifest manifest)
    {
        var statuses = new List<JavaFileStatus>();

        foreach (var entry in manifest.Files)
        {
            var localSha256 = SafeFilePath.TryResolve(installPath, entry.FileName, out var filePath)
                ? TryComputeSha256(filePath)
                : null;
            var isUpToDate = localSha256 is not null
                && string.Equals(localSha256, entry.Sha256, StringComparison.OrdinalIgnoreCase);

            statuses.Add(new JavaFileStatus(
                entry.FileName,
                InstalledVersion: isUpToDate ? entry.Version : null,
                RequiredVersion: entry.Version,
                IsUpToDate: isUpToDate));
        }

        return statuses;
    }

    private static string? TryComputeSha256(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
