using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wrexercise.IntegrationTests.Infrastructure;

public sealed class SpaFallbackTests : IDisposable
{
    private const string IndexHtml = "<!doctype html><title>Wrexercise</title>";

    // Pasta própria para o teste não depender do build do frontend.
    private readonly DirectoryInfo _webRoot = Directory.CreateTempSubdirectory("wrexercise-webroot-");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public SpaFallbackTests()
    {
        File.WriteAllText(Path.Combine(_webRoot.FullName, "index.html"), IndexHtml);
        File.WriteAllText(Path.Combine(_webRoot.FullName, "sw.js"), "// sw");
        File.WriteAllText(Path.Combine(_webRoot.FullName, "manifest.webmanifest"), "{}");
        File.WriteAllBytes(Path.Combine(_webRoot.FullName, "avatar-jpgma.png"), [0x89, 0x50, 0x4E, 0x47]);
        Directory.CreateDirectory(Path.Combine(_webRoot.FullName, "assets"));
        File.WriteAllText(Path.Combine(_webRoot.FullName, "assets", "index-abc123.js"), "// app");
    }

    public void Dispose() => _webRoot.Delete(recursive: true);

    [Theory]
    [InlineData("/")]
    [InlineData("/grupos/123/desafios")]
    public async Task NonApiRoutes_ReturnTheSpa(string path)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(IndexHtml, await response.Content.ReadAsStringAsync(Token));
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/inexistente")]
    [InlineData("/api/grupos/123")]
    public async Task ApiRoutes_NeverReturnTheSpa(string path)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // Nome fixo: o navegador e a CDN precisam revalidar, ou uma versão nova do arquivo nunca chegaria.
    [Theory]
    [InlineData("/sw.js", "text/javascript")]
    [InlineData("/manifest.webmanifest", "application/manifest+json")]
    [InlineData("/avatar-jpgma.png", "image/png")]
    public async Task FixedNameFiles_AreAlwaysRevalidated(string path, string contentType)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task HashedAssets_AreImmutable()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/assets/index-abc123.js", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("immutable", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.WebRootKey, _webRoot.FullName);
            builder.UseSetting("ConnectionStrings:Database", "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x");
        });
}
