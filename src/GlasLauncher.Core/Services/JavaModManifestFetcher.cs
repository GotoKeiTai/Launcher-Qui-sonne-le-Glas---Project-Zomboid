using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public class JavaModManifestFetcher
{
    // Placeholder — no real hosting exists yet (see spec, §8.3 of the cahier des charges).
    // Update once the manifest is actually published somewhere.
    private const string ManifestUrl = "https://raw.githubusercontent.com/GotoKeiTai/glas-launcher-hosting/main/java-mod-manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;

    public JavaModManifestFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public static JavaModManifestFetcher CreateDefault() => new(new HttpClient());

    public async Task<JavaModManifest?> FetchAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<JavaModManifest>(ManifestUrl, SerializerOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
