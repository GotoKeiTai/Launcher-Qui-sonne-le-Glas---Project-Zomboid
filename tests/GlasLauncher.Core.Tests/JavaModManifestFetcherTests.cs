using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using GlasLauncher.Core.Services;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class JavaModManifestFetcherTests
{
    [Fact]
    public async Task FetchAsync_ValidJson_ReturnsManifest()
    {
        const string json = """
            {
                "files": [
                    { "fileName": "GlasVoipMod.jar", "version": "0.1.0", "sha256": "abc123", "downloadUrl": "https://example.com/GlasVoipMod.jar" }
                ]
            }
            """;
        var httpClient = new HttpClient(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
        var fetcher = new JavaModManifestFetcher(httpClient);

        var manifest = await fetcher.FetchAsync();

        Assert.NotNull(manifest);
        Assert.Single(manifest!.Files);
        Assert.Equal("GlasVoipMod.jar", manifest.Files[0].FileName);
        Assert.Equal("0.1.0", manifest.Files[0].Version);
        Assert.Equal("abc123", manifest.Files[0].Sha256);
        Assert.Equal("https://example.com/GlasVoipMod.jar", manifest.Files[0].DownloadUrl);
    }

    [Fact]
    public async Task FetchAsync_HttpErrorStatus_ReturnsNull()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        var fetcher = new JavaModManifestFetcher(httpClient);

        var manifest = await fetcher.FetchAsync();

        Assert.Null(manifest);
    }

    [Fact]
    public async Task FetchAsync_MalformedJson_ReturnsNull()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not valid json", Encoding.UTF8, "application/json")
        }));
        var fetcher = new JavaModManifestFetcher(httpClient);

        var manifest = await fetcher.FetchAsync();

        Assert.Null(manifest);
    }

    [Fact]
    public async Task FetchAsync_NetworkFailure_ReturnsNull()
    {
        var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        var fetcher = new JavaModManifestFetcher(httpClient);

        var manifest = await fetcher.FetchAsync();

        Assert.Null(manifest);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_response);
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("simulated network failure");
    }
}
