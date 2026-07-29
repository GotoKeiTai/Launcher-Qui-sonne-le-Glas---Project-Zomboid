using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public class JavaModManifestFetcher
{
    // Published by GlasVoipMod's own CI/release pipeline (see its
    // docs/superpowers/specs/2026-07-30-java-mod-ci-release-design.md).
    private const string ManifestUrl = "https://github.com/GotoKeiTai/GlasVoipMod/releases/latest/download/manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;

    public JavaModManifestFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public static JavaModManifestFetcher CreateDefault() =>
        new(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });

    public async Task<JavaModManifest?> FetchAsync()
    {
        try
        {
            var manifest = await _httpClient.GetFromJsonAsync<JavaModManifest>(ManifestUrl, SerializerOptions);
            return manifest is null
                ? null
                : manifest with
                {
                    Files = manifest.Files ?? Array.Empty<JavaFileEntry>(),
                    RequiredLaunchOptions = manifest.RequiredLaunchOptions ?? Array.Empty<string>()
                };
        }
        catch (Exception)
        {
            return null;
        }
    }
}
