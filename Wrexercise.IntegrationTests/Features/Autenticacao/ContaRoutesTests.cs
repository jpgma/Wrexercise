using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Autenticacao;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Autenticacao;

public sealed class ContaRoutesTests : IAsyncLifetime
{
    private const string SenhaAntiga = "senha-antiga";
    private const string SenhaNova = "senha-nova-123";

    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task AlterarNome_AtualizaENormaliza()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var response = await ana.Client.PutAsJsonAsync(
            "/api/usuarios/eu",
            new AlterarNomeRequest("  Ana Maria  "),
            AppJsonContext.Default.AlterarNomeRequest,
            Token);

        response.EnsureSuccessStatusCode();
        var sessao = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.SessaoResponse, Token);
        Assert.Equal("Ana Maria", sessao!.Usuario.NomeExibicao);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task AlterarNome_Invalido_Recusado(string? nome)
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var response = await ana.Client.PutAsJsonAsync(
            "/api/usuarios/eu",
            new AlterarNomeRequest(nome),
            AppJsonContext.Default.AlterarNomeRequest,
            Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task ModoInicio_PadraoEApp_EAlteracaoPersiste()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var antes = await ana.Client.GetFromJsonAsync(
            "/api/autenticacao/sessao",
            AppJsonContext.Default.SessaoResponse,
            Token);
        Assert.Equal(ModoInicio.App, antes!.Usuario.ModoInicio);

        var response = await ana.Client.PutAsJsonAsync(
            "/api/usuarios/eu/modo-inicio",
            new AlterarModoInicioRequest(ModoInicio.Jogo),
            AppJsonContext.Default.AlterarModoInicioRequest,
            Token);

        response.EnsureSuccessStatusCode();
        var sessao = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.SessaoResponse, Token);
        Assert.Equal(ModoInicio.Jogo, sessao!.Usuario.ModoInicio);

        var depois = await ana.Client.GetFromJsonAsync(
            "/api/autenticacao/sessao",
            AppJsonContext.Default.SessaoResponse,
            Token);
        Assert.Equal(ModoInicio.Jogo, depois!.Usuario.ModoInicio);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("jogo")]
    [InlineData("Outro")]
    public async Task ModoInicio_Invalido_RecusadoSemAlterar(string? modo)
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var response = await ana.Client.PutAsJsonAsync(
            "/api/usuarios/eu/modo-inicio",
            new AlterarModoInicioRequest(modo),
            AppJsonContext.Default.AlterarModoInicioRequest,
            Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
        Assert.Equal(
            ModoInicio.App,
            await _host.ScalarAsync<string>("SELECT modo_inicio FROM usuarios WHERE id = $1", ana.Id));
    }

    [Fact]
    public async Task ModoInicio_ExigeSessao()
    {
        await ApiTestHost.AssertProblemAsync(
            await _host.CreateAnonimo().PutAsJsonAsync(
                "/api/usuarios/eu/modo-inicio",
                new AlterarModoInicioRequest(ModoInicio.Jogo),
                AppJsonContext.Default.AlterarModoInicioRequest,
                Token),
            HttpStatusCode.Unauthorized,
            "NAO_AUTENTICADO");
    }

    [Fact]
    public async Task Conta_ExigeSessao()
    {
        var anonimo = _host.CreateAnonimo();

        await ApiTestHost.AssertProblemAsync(
            await anonimo.PutAsJsonAsync(
                "/api/usuarios/eu",
                new AlterarNomeRequest("Ana"),
                AppJsonContext.Default.AlterarNomeRequest,
                Token),
            HttpStatusCode.Unauthorized,
            "NAO_AUTENTICADO");
        await ApiTestHost.AssertProblemAsync(
            await AlterarSenhaAsync(anonimo, SenhaAntiga, SenhaNova),
            HttpStatusCode.Unauthorized,
            "NAO_AUTENTICADO");
    }

    [Fact]
    public async Task AlterarSenha_MantemEstaSessao_EEncerraAsOutras()
    {
        var ana = await CriarComSenhaAsync();
        var outroAparelho = await NovaSessaoAsync(ana.Id);

        var response = await AlterarSenhaAsync(ana.Client, SenhaAntiga, SenhaNova);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ana.Client.GetAsync("/api/autenticacao/sessao", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await outroAparelho.GetAsync("/api/autenticacao/sessao", Token)).StatusCode);

        var hash = await _host.ScalarAsync<string>("SELECT password_hash FROM usuarios WHERE id = $1", ana.Id);
        Assert.True(Senhas.Verify(SenhaNova, hash));
        Assert.False(Senhas.Verify(SenhaAntiga, hash));
    }

    [Fact]
    public async Task AlterarSenha_AtualIncorreta_NaoMudaNada()
    {
        var ana = await CriarComSenhaAsync();
        var outroAparelho = await NovaSessaoAsync(ana.Id);

        var response = await AlterarSenhaAsync(ana.Client, "senha-errada", SenhaNova);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "SENHA_ATUAL_INCORRETA");
        Assert.Equal(HttpStatusCode.OK, (await outroAparelho.GetAsync("/api/autenticacao/sessao", Token)).StatusCode);
    }

    [Theory]
    [InlineData("curta")]
    [InlineData(null)]
    public async Task AlterarSenha_NovaInvalida_Recusada(string? nova)
    {
        var ana = await CriarComSenhaAsync();

        var response = await AlterarSenhaAsync(ana.Client, SenhaAntiga, nova);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task ResetSenha_EncerraSessoes_ELinkValeUmaVez()
    {
        var ana = await CriarComSenhaAsync();
        var email = await _host.ScalarAsync<string>("SELECT email FROM usuarios WHERE id = $1", ana.Id);

        var token = await ResetSenhaCommand.CriarTokenAsync(_host.DataSource, email.ToUpperInvariant(), Token);

        Assert.NotNull(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ana.Client.GetAsync("/api/autenticacao/sessao", Token)).StatusCode);

        var anonimo = _host.CreateAnonimo();
        Assert.Equal(HttpStatusCode.NoContent, (await RedefinirAsync(anonimo, token, SenhaNova)).StatusCode);
        var hash = await _host.ScalarAsync<string>("SELECT password_hash FROM usuarios WHERE id = $1", ana.Id);
        Assert.True(Senhas.Verify(SenhaNova, hash));

        await ApiTestHost.AssertProblemAsync(
            await RedefinirAsync(anonimo, token, "outra-senha-456"),
            HttpStatusCode.BadRequest,
            "REDEFINICAO_INVALIDA");
    }

    [Fact]
    public async Task ResetSenha_GuardaSoOHash_ESubstituiOLinkAnterior()
    {
        var ana = await CriarComSenhaAsync();
        var email = await _host.ScalarAsync<string>("SELECT email FROM usuarios WHERE id = $1", ana.Id);

        var primeiro = await ResetSenhaCommand.CriarTokenAsync(_host.DataSource, email, Token);
        var segundo = await ResetSenhaCommand.CriarTokenAsync(_host.DataSource, email, Token);

        Assert.Equal(0L, await _host.CountAsync(
            $"SELECT count(*) FROM redefinicoes_senha WHERE encode(token_hash, 'escape') = '{segundo}'"));
        var anonimo = _host.CreateAnonimo();
        await ApiTestHost.AssertProblemAsync(
            await RedefinirAsync(anonimo, primeiro, SenhaNova),
            HttpStatusCode.BadRequest,
            "REDEFINICAO_INVALIDA");
        Assert.Equal(HttpStatusCode.NoContent, (await RedefinirAsync(anonimo, segundo, SenhaNova)).StatusCode);
    }

    [Fact]
    public async Task ResetSenha_LinkExpirado_Recusado()
    {
        var ana = await CriarComSenhaAsync();
        var email = await _host.ScalarAsync<string>("SELECT email FROM usuarios WHERE id = $1", ana.Id);
        var token = await ResetSenhaCommand.CriarTokenAsync(_host.DataSource, email, Token);
        await _host.ExecuteAsync("UPDATE redefinicoes_senha SET expires_at = now() - interval '1 second'");

        await ApiTestHost.AssertProblemAsync(
            await RedefinirAsync(_host.CreateAnonimo(), token, SenhaNova),
            HttpStatusCode.BadRequest,
            "REDEFINICAO_INVALIDA");
    }

    [Fact]
    public async Task ResetSenha_EmailDesconhecido_NaoGeraLink()
    {
        Assert.Null(await ResetSenhaCommand.CriarTokenAsync(_host.DataSource, "ninguem@exemplo.com", Token));
    }

    [Fact]
    public async Task Redefinir_TokenDesconhecido_Recusado()
    {
        await ApiTestHost.AssertProblemAsync(
            await RedefinirAsync(_host.CreateAnonimo(), "desconhecido", SenhaNova),
            HttpStatusCode.BadRequest,
            "REDEFINICAO_INVALIDA");
    }

    private async Task<TestUsuario> CriarComSenhaAsync()
    {
        var usuario = await _host.CreateUsuarioAsync("Ana");
        await _host.ExecuteAsync(
            $"UPDATE usuarios SET password_hash = '{Senhas.Hash(SenhaAntiga)}' WHERE id = '{usuario.Id}'");
        return usuario;
    }

    private async Task<HttpClient> NovaSessaoAsync(Guid usuarioId)
    {
        var sessao = OpaqueToken.Generate();
        var hash = Convert.ToHexString(OpaqueToken.Hash(sessao));
        await _host.ExecuteAsync($"""
            INSERT INTO sessoes_autenticacao (usuario_id, token_hash, expires_at)
            VALUES ('{usuarioId}', decode('{hash}', 'hex'), now() + interval '1 day')
            """);
        var client = _host.CreateAnonimo();
        client.DefaultRequestHeaders.Add("Cookie", $"wx_session={sessao}");
        return client;
    }

    private static Task<HttpResponseMessage> AlterarSenhaAsync(HttpClient client, string atual, string? nova) =>
        client.PostAsJsonAsync(
            "/api/usuarios/eu/senha",
            new AlterarSenhaRequest(atual, nova),
            AppJsonContext.Default.AlterarSenhaRequest,
            Token);

    private static Task<HttpResponseMessage> RedefinirAsync(HttpClient client, string? token, string nova) =>
        client.PostAsJsonAsync(
            "/api/autenticacao/redefinir-senha",
            new RedefinirSenhaRequest(token, nova),
            AppJsonContext.Default.RedefinirSenhaRequest,
            Token);
}
