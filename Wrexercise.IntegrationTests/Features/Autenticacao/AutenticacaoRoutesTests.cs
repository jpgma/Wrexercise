using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Wrexercise.Features.Autenticacao;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Autenticacao;

public sealed class AutenticacaoRoutesTests : IAsyncLifetime
{
    private const string Registrar = "/api/autenticacao/registrar";
    private const string ConfiguracaoInicial = "/api/autenticacao/configuracao-inicial";
    private const string Entrar = "/api/autenticacao/entrar";
    private const string Sair = "/api/autenticacao/sair";
    private const string Sessao = "/api/autenticacao/sessao";
    private const string Origin = "http://localhost";
    private const string Senha = "senha-segura";

    private TestDatabase _database = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Registrar_CreatesAccountAndSession_ThatSurvivesTheNextRequest()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        var response = await CriarContaAsync(client, "Ana", "  Ana@Exemplo.com ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.SessaoResponse, Token);
        Assert.Equal("Ana", created?.Usuario.NomeExibicao);
        Assert.Equal("ana@exemplo.com", created?.Usuario.Email);

        var sessao = await client.GetAsync(Sessao, Token);

        Assert.Equal(HttpStatusCode.OK, sessao.StatusCode);
        var body = await sessao.Content.ReadFromJsonAsync(AppJsonContext.Default.SessaoResponse, Token);
        Assert.Equal(created?.Usuario, body?.Usuario);
    }

    [Fact]
    public async Task ConfiguracaoInicial_IsPendente_OnlyWhileThereAreNoUsers()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        var antes = await client.GetFromJsonAsync(
            ConfiguracaoInicial, AppJsonContext.Default.ConfiguracaoInicialResponse, Token);
        await CriarContaAsync(client, "Ana", "ana@exemplo.com");
        var depois = await client.GetFromJsonAsync(
            ConfiguracaoInicial, AppJsonContext.Default.ConfiguracaoInicialResponse, Token);

        Assert.True(antes?.Pendente);
        Assert.False(depois?.Pendente);
    }

    [Fact]
    public async Task ConfiguracaoInicial_IsRefused_WhenAUserAlreadyExists()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        await CriarContaAsync(client, "Ana", "ana@exemplo.com");

        using var outro = CreateClient(factory);
        var response = await CriarContaAsync(outro, "Outra Ana", "outra@exemplo.com");

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "CONFIGURACAO_JA_CONCLUIDA");
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(1L, await CountAsync("SELECT count(*) FROM usuarios"));
    }

    [Fact]
    public async Task ConfiguracaoInicial_CreatesOnlyOneUser_WhenCalledConcurrently()
    {
        await using var factory = CreateFactory();
        using var primeiro = CreateClient(factory);
        using var segundo = CreateClient(factory);

        var responses = await Task.WhenAll(
            CriarContaAsync(primeiro, "Ana", "ana@exemplo.com"),
            CriarContaAsync(segundo, "Bia", "bia@exemplo.com"));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1L, await CountAsync("SELECT count(*) FROM usuarios"));
    }

    [Theory]
    [InlineData("", "ana@exemplo.com", Senha)]
    [InlineData("Ana", "sem-arroba", Senha)]
    [InlineData("Ana", "ana@exemplo.com", "curta")]
    [InlineData(null, null, null)]
    public async Task ConfiguracaoInicial_RejectsInvalidData(string? nome, string? email, string? senha)
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            ConfiguracaoInicial,
            new ConfiguracaoInicialRequest(nome, email, senha),
            AppJsonContext.Default.ConfiguracaoInicialRequest,
            Token);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
        Assert.Equal(0L, await CountAsync("SELECT count(*) FROM usuarios"));
    }

    [Fact]
    public async Task Registrar_WithoutAnInvite_IsRefused_EvenBeforeTheFirstUser()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            Registrar,
            new RegistrarRequest("Ana", "ana@exemplo.com", Senha),
            AppJsonContext.Default.RegistrarRequest,
            Token);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "CADASTRO_FECHADO");
        Assert.Equal(0L, await CountAsync("SELECT count(*) FROM usuarios"));
    }

    [Theory]
    [InlineData("", "ana@exemplo.com", Senha)]
    [InlineData("Ana", "sem-arroba", Senha)]
    [InlineData("Ana", "ana@exemplo.com", "curta")]
    [InlineData(null, null, null)]
    public async Task Registrar_RejectsInvalidData(string? nome, string? email, string? senha)
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            Registrar,
            new RegistrarRequest(nome, email, senha, "qualquer-convite"),
            AppJsonContext.Default.RegistrarRequest,
            Token);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task MalformedJson_ReturnsBadRequest()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var content = new StringContent("{ nao é json", Encoding.UTF8, "application/json");

        var response = await client.PostAsync(Entrar, content, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Entrar_WithCorrectCredentials_CreatesSession_RegardlessOfEmailCase()
    {
        await using var factory = CreateFactory();
        using (var registering = CreateClient(factory))
        {
            await CriarContaAsync(registering, "Ana", "ana@exemplo.com");
        }

        using var client = CreateClient(factory);

        var response = await EntrarAsync(client, "ANA@exemplo.com", Senha);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Sessao, Token)).StatusCode);
    }

    [Fact]
    public async Task Entrar_RespondsIdentically_ForWrongPasswordAndUnknownEmail()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        await CriarContaAsync(client, "Ana", "ana@exemplo.com");
        await client.PostAsync(Sair, null, Token);

        var wrongPassword = await EntrarAsync(client, "ana@exemplo.com", "senha-errada");
        var unknownEmail = await EntrarAsync(client, "ninguem@exemplo.com", Senha);

        await AssertProblemAsync(wrongPassword, HttpStatusCode.Unauthorized, "CREDENCIAIS_INVALIDAS");
        await AssertProblemAsync(unknownEmail, HttpStatusCode.Unauthorized, "CREDENCIAIS_INVALIDAS");
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(Token),
            await unknownEmail.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Entrar_RemovesThatUsersExpiredSessions()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        await CriarContaAsync(client, "Ana", "ana@exemplo.com");
        await ExecuteAsync("UPDATE sessoes_autenticacao SET expires_at = now() - interval '1 minute'");

        await EntrarAsync(client, "ana@exemplo.com", Senha);

        Assert.Equal(1L, await CountAsync("SELECT count(*) FROM sessoes_autenticacao"));
    }

    [Fact]
    public async Task Sessao_WithoutCookie_ReturnsNotAuthenticated()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync(Sessao, Token);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "NAO_AUTENTICADO");
    }

    [Fact]
    public async Task Sessao_WithUnknownToken_ReturnsNotAuthenticated()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, handleCookies: false);
        client.DefaultRequestHeaders.Add("Cookie", "wx_session=token-inventado");

        var response = await client.GetAsync(Sessao, Token);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "NAO_AUTENTICADO");
    }

    [Fact]
    public async Task Sessao_Expired_ReturnsNotAuthenticated()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        await CriarContaAsync(client, "Ana", "ana@exemplo.com");
        await ExecuteAsync("UPDATE sessoes_autenticacao SET expires_at = now() - interval '1 minute'");

        var response = await client.GetAsync(Sessao, Token);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "NAO_AUTENTICADO");
    }

    [Fact]
    public async Task Sessao_Revoked_ReturnsNotAuthenticated_EvenIfTheCookieIsReplayed()
    {
        await using var factory = CreateFactory();
        using var registering = CreateClient(factory, handleCookies: false);
        var cookie = ReadSessionCookie(await CriarContaAsync(registering, "Ana", "ana@exemplo.com"));

        using var client = CreateClient(factory, handleCookies: false);
        client.DefaultRequestHeaders.Add("Cookie", $"wx_session={cookie.Value}");

        var sair = await client.PostAsync(Sair, null, Token);
        var response = await client.GetAsync(Sessao, Token);

        Assert.Equal(HttpStatusCode.NoContent, sair.StatusCode);
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "NAO_AUTENTICADO");
    }

    [Fact]
    public async Task Sair_IsIdempotent_AndClearsTheCookie()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, handleCookies: false);

        var response = await client.PostAsync(Sair, null, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            header => header.StartsWith("wx_session=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sair_OnlyEndsTheCurrentDevice()
    {
        await using var factory = CreateFactory();
        using var phone = CreateClient(factory);
        using var laptop = CreateClient(factory);
        await CriarContaAsync(phone, "Ana", "ana@exemplo.com");
        await EntrarAsync(laptop, "ana@exemplo.com", Senha);

        await phone.PostAsync(Sair, null, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync(Sessao, Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync(Sessao, Token)).StatusCode);
    }

    [Fact]
    public async Task Sessao_NearingExpiry_IsRenewed()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        await CriarContaAsync(client, "Ana", "ana@exemplo.com");
        await ExecuteAsync("UPDATE sessoes_autenticacao SET expires_at = now() + interval '10 days'");

        var response = await client.GetAsync(Sessao, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(1L, await CountAsync(
            "SELECT count(*) FROM sessoes_autenticacao WHERE expires_at > now() + interval '29 days'"));
    }

    [Fact]
    public async Task Sessao_Recent_IsNotRewrittenOnEveryRequest()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        await CriarContaAsync(client, "Ana", "ana@exemplo.com");

        var response = await client.GetAsync(Sessao, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Database_StoresOnlyHashes_NeverTheTokenOrThePassword()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, handleCookies: false);

        var cookie = ReadSessionCookie(await CriarContaAsync(client, "Ana", "ana@exemplo.com"));

        Assert.Equal(1L, await CountAsync(
            "SELECT count(*) FROM sessoes_autenticacao WHERE token_hash = $1", OpaqueToken.Hash(cookie.Value)));
        Assert.Equal(0L, await CountAsync(
            "SELECT count(*) FROM sessoes_autenticacao WHERE token_hash = $1", Encoding.UTF8.GetBytes(cookie.Value)));
        Assert.Equal(1L, await CountAsync(
            "SELECT count(*) FROM usuarios WHERE password_hash LIKE 'pbkdf2-sha256$%' AND password_hash NOT LIKE '%senha%'"));
    }

    [Fact]
    public async Task Cookie_HasTheExpectedAttributes_InDevelopment()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, handleCookies: false);

        var cookie = ReadSessionCookie(await CriarContaAsync(client, "Ana", "ana@exemplo.com"));

        Assert.Contains("httponly", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=2592000", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cookie_IsSecure_OutsideDevelopment()
    {
        await using var factory = CreateFactory(environment: "Production");
        using var client = CreateClient(factory, handleCookies: false);

        var cookie = ReadSessionCookie(await CriarContaAsync(client, "Ana", "ana@exemplo.com"));

        Assert.Contains("secure", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://malicioso.exemplo")]
    [InlineData(null)]
    public async Task Writes_FromAnotherOrigin_AreRefused_EvenWithValidCookie(string? origin)
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        await CriarContaAsync(client, "Ana", "ana@exemplo.com");

        client.DefaultRequestHeaders.Remove("Origin");
        if (origin is not null)
        {
            client.DefaultRequestHeaders.Add("Origin", origin);
        }

        var response = await client.PostAsync(Sair, null, Token);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "ORIGEM_INVALIDA");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Sessao, Token)).StatusCode);
    }

    [Fact]
    public async Task Entrar_IsLimitedPerClient_AndOtherClientsAreNotAffected()
    {
        await using var factory = CreateFactory();
        using var attacker = CreateClient(factory);
        attacker.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.1");
        using var other = CreateClient(factory);
        other.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.2");

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var allowed = await EntrarAsync(attacker, "ninguem@exemplo.com", Senha);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var blocked = await EntrarAsync(attacker, "ninguem@exemplo.com", Senha);
        var unaffected = await EntrarAsync(other, "ninguem@exemplo.com", Senha);

        await AssertProblemAsync(blocked, HttpStatusCode.TooManyRequests, "MUITAS_TENTATIVAS");
        Assert.Equal(HttpStatusCode.Unauthorized, unaffected.StatusCode);
    }

    // A primeira conta nasce na configuração inicial; as demais exigem convite (ver ConvitesRoutesTests).
    private static Task<HttpResponseMessage> CriarContaAsync(HttpClient client, string nome, string email) =>
        client.PostAsJsonAsync(
            ConfiguracaoInicial,
            new ConfiguracaoInicialRequest(nome, email, Senha),
            AppJsonContext.Default.ConfiguracaoInicialRequest,
            Token);

    private static Task<HttpResponseMessage> EntrarAsync(HttpClient client, string email, string senha) =>
        client.PostAsJsonAsync(Entrar, new EntrarRequest(email, senha), AppJsonContext.Default.EntrarRequest, Token);

    private WebApplicationFactory<Program> CreateFactory(string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseTestDefaults();
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:Database", _database.ConnectionString);
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool handleCookies = true)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = handleCookies });
        client.DefaultRequestHeaders.Add("Origin", Origin);
        return client;
    }

    private static (string Value, string Attributes) ReadSessionCookie(HttpResponseMessage response)
    {
        var header = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        var separator = header.IndexOf(';');
        var pair = header[..separator];
        Assert.StartsWith("wx_session=", pair);
        return (pair["wx_session=".Length..], header[separator..]);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(Token);
    }

    private async Task<long> CountAsync(string sql, byte[]? parameter = null)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        if (parameter is not null)
        {
            command.Parameters.AddWithValue(parameter);
        }

        return (long)(await command.ExecuteScalarAsync(Token))!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ProblemResponse, Token);
        Assert.Equal(code, problem?.Code);
    }
}
