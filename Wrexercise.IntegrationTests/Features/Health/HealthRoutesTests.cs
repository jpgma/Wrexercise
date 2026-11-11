using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Wrexercise.Features.Health;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Health;

public class HealthRoutesTests
{
    private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_ReturnsOk_WithoutUsingTheDatabase()
    {
        await using var factory = CreateFactory(UnreachableDatabase);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.HealthResponse, Token);
        Assert.Equal(new HealthResponse("ok"), body);
    }

    [Fact]
    public async Task Ready_ReturnsOk_WhenDatabaseIsAvailable()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ready", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReturnsProblem_WhenDatabaseIsUnavailable()
    {
        await using var factory = CreateFactory(UnreachableDatabase);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ready", Token);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "BANCO_INDISPONIVEL");
    }

    [Fact]
    public async Task UnknownApiRoute_ReturnsJsonProblem_InsteadOfSpa()
    {
        await using var factory = CreateFactory(UnreachableDatabase);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/inexistente", Token);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "ROTA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task UnhandledException_DoesNotExposeInternalDetails()
    {
        // Sem connection string, resolver o NpgsqlDataSource lança uma exceção não tratada em /ready.
        await using var factory = CreateFactory(connectionString: "");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ready", Token);

        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "ERRO_INTERNO");
        var body = await response.Content.ReadAsStringAsync(Token);
        Assert.DoesNotContain("ConnectionStrings", body);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseTestDefaults().UseSetting("ConnectionStrings:Database", connectionString));

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ProblemResponse, Token);
        Assert.Equal(code, problem?.Code);
    }
}
