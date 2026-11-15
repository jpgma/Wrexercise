using System.Buffers.Text;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Integracoes;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Integracoes;

public sealed class IntegracoesRoutesTests : IAsyncLifetime
{
    private const string RedirectUri = "movr://integracoes/wrexercise";

    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly byte[] JpegMinimo =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9,
    ];

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task FluxoCompleto_RascunhoViraPrimeiraEvidencia()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);

        var salvo = await PutRascunhoAsync(app, "treino-1");
        Assert.Equal(HttpStatusCode.OK, salvo.StatusCode);
        var situacao = await salvo.Content.ReadFromJsonAsync(AppJsonContext.Default.RascunhoSituacaoResponse, Token);
        Assert.Equal(SituacaoRascunho.Pendente, situacao!.Situacao);
        Assert.Equal($"/importar/{situacao.Id}", situacao.Url);

        var rascunho = await ana.Client.GetFromJsonAsync(
            $"/api/rascunhos/{situacao.Id}", AppJsonContext.Default.RascunhoResponse, Token);
        Assert.Equal(TipoAtividade.Calistenia, rascunho!.Tipo);
        Assert.Equal(1800, rascunho.DuracaoSegundos);
        var imagem = await ana.Client.GetAsync(rascunho.ImagemUrl, Token);
        Assert.Equal(HttpStatusCode.OK, imagem.StatusCode);

        var criada = await ana.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                rascunho.Tipo,
                rascunho.DuracaoSegundos,
                rascunho.StartedAt,
                null,
                TipoComprovacao.Fotografia,
                rascunho.Id),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        Assert.Single(atividade!.Evidencias);
        Assert.Equal("Integracao", await _host.ScalarAsync<string>(
            "SELECT origem_data_captura FROM evidencias_atividade WHERE atividade_id = $1", atividade.Id));
        Assert.Equal(0, await _host.CountAsync(
            $"SELECT count(*) FROM avisos_atividade WHERE atividade_id = '{atividade.Id}'"));

        var depois = await app.GetFromJsonAsync(
            "/api/integracoes/rascunhos/treino-1", AppJsonContext.Default.RascunhoSituacaoResponse, Token);
        Assert.Equal(SituacaoRascunho.Postada, depois!.Situacao);
        Assert.Equal(atividade.Id, depois.AtividadeId);
        Assert.Equal($"/atividades/{atividade.Id}", depois.Url);

        // Depois de postado, o app não troca mais o rascunho.
        var trocado = await PutRascunhoAsync(app, "treino-1");
        await ApiTestHost.AssertProblemAsync(trocado, HttpStatusCode.Conflict, "RASCUNHO_JA_POSTADO");

        // O mesmo rascunho não vira uma segunda atividade.
        var repetida = await ana.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                rascunho.Tipo, rascunho.DuracaoSegundos, rascunho.StartedAt, null, TipoComprovacao.Fotografia, rascunho.Id),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        await ApiTestHost.AssertProblemAsync(repetida, HttpStatusCode.NotFound, "RASCUNHO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Salvar_RepeteSemCriarOutroRascunho()
    {
        var ana = await _host.CreateUsuarioAsync();
        var app = await ConectarAsync(ana);

        var primeiro = await (await PutRascunhoAsync(app, "treino-1")).Content
            .ReadFromJsonAsync(AppJsonContext.Default.RascunhoSituacaoResponse, Token);
        var segundo = await (await PutRascunhoAsync(app, "treino-1")).Content
            .ReadFromJsonAsync(AppJsonContext.Default.RascunhoSituacaoResponse, Token);

        Assert.Equal(primeiro!.Id, segundo!.Id);
        Assert.Equal(1, await _host.CountAsync("SELECT count(*) FROM rascunhos_atividade"));
    }

    [Fact]
    public async Task Trocar_RecusaVerificadorErrado_EQueimaOCodigo()
    {
        var ana = await _host.CreateUsuarioAsync();
        var (verifier, challenge) = CriarPkce();
        var codigo = await AutorizarAsync(ana, challenge);

        var errado = await TrocarAsync(codigo, CriarPkce().Verifier);
        await ApiTestHost.AssertProblemAsync(errado, HttpStatusCode.BadRequest, "CODIGO_INVALIDO");

        var certo = await TrocarAsync(codigo, verifier);
        await ApiTestHost.AssertProblemAsync(certo, HttpStatusCode.BadRequest, "CODIGO_INVALIDO");
    }

    [Fact]
    public async Task Trocar_RecusaCodigoRepetido()
    {
        var ana = await _host.CreateUsuarioAsync();
        var (verifier, challenge) = CriarPkce();
        var codigo = await AutorizarAsync(ana, challenge);

        Assert.Equal(HttpStatusCode.OK, (await TrocarAsync(codigo, verifier)).StatusCode);
        await ApiTestHost.AssertProblemAsync(
            await TrocarAsync(codigo, verifier), HttpStatusCode.BadRequest, "CODIGO_INVALIDO");
    }

    [Theory]
    [InlineData("https://exemplo.com/callback")]
    [InlineData("movr://outra-coisa")]
    [InlineData("http://127.0.0.1:0/wx-callback")]
    [InlineData("http://127.0.0.1:8080/outro")]
    public async Task Autorizar_RecusaEnderecoDeRetornoForaDaLista(string redirectUri)
    {
        var ana = await _host.CreateUsuarioAsync();
        var response = await ana.Client.PostAsJsonAsync(
            "/api/integracoes/autorizacoes",
            new AutorizarAppRequest(AppIntegracao.Movr, CriarPkce().Challenge, redirectUri),
            AppJsonContext.Default.AutorizarAppRequest,
            Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task Autorizar_AceitaRetornoLocal()
    {
        var ana = await _host.CreateUsuarioAsync();
        var response = await ana.Client.PostAsJsonAsync(
            "/api/integracoes/autorizacoes",
            new AutorizarAppRequest(AppIntegracao.Movr, CriarPkce().Challenge, "http://127.0.0.1:53124/wx-callback"),
            AppJsonContext.Default.AutorizarAppRequest,
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TokenComCookie_ERecusado()
    {
        var ana = await _host.CreateUsuarioAsync();
        var app = await ConectarAsync(ana);
        ana.Client.DefaultRequestHeaders.Authorization = app.DefaultRequestHeaders.Authorization;

        var response = await ana.Client.GetAsync("/api/integracoes/rascunhos/treino-1", Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "APP_NAO_AUTORIZADO");
    }

    [Fact]
    public async Task EscritaComCookieDeOutraOrigem_ContinuaRecusada()
    {
        var ana = await _host.CreateUsuarioAsync();
        ana.Client.DefaultRequestHeaders.Remove("Origin");
        ana.Client.DefaultRequestHeaders.Add("Origin", "https://atacante.example");

        var response = await ana.Client.PostAsJsonAsync(
            "/api/integracoes/autorizacoes",
            new AutorizarAppRequest(AppIntegracao.Movr, CriarPkce().Challenge, RedirectUri),
            AppJsonContext.Default.AutorizarAppRequest,
            Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Forbidden, "ORIGEM_INVALIDA");
    }

    [Fact]
    public async Task Desconectar_InvalidaOToken()
    {
        var ana = await _host.CreateUsuarioAsync();
        var app = await ConectarAsync(ana);

        var lista = await ana.Client.GetFromJsonAsync(
            "/api/apps-conectados", AppJsonContext.Default.AppsConectadosResponse, Token);
        var conectado = Assert.Single(lista!.Apps);
        Assert.Equal(AppIntegracao.Movr, conectado.App);

        var desconectar = await ana.Client.DeleteAsync($"/api/apps-conectados/{conectado.Id}", Token);
        Assert.Equal(HttpStatusCode.NoContent, desconectar.StatusCode);

        await ApiTestHost.AssertProblemAsync(
            await PutRascunhoAsync(app, "treino-1"), HttpStatusCode.Unauthorized, "APP_NAO_AUTORIZADO");
    }

    [Fact]
    public async Task RevogarPeloApp_InvalidaOToken()
    {
        var ana = await _host.CreateUsuarioAsync();
        var app = await ConectarAsync(ana);

        Assert.Equal(HttpStatusCode.NoContent, (await app.DeleteAsync("/api/integracoes/token", Token)).StatusCode);

        var response = await app.GetAsync("/api/integracoes/rascunhos/treino-1", Token);
        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "APP_NAO_AUTORIZADO");
    }

    [Fact]
    public async Task Rascunho_DeOutraPessoa_NaoAparece()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var bia = await _host.CreateUsuarioAsync("Bia");
        var grupoId = await _host.CreateGrupoAsync(bia);
        var app = await ConectarAsync(ana);
        var situacao = await (await PutRascunhoAsync(app, "treino-1")).Content
            .ReadFromJsonAsync(AppJsonContext.Default.RascunhoSituacaoResponse, Token);

        await ApiTestHost.AssertProblemAsync(
            await bia.Client.GetAsync($"/api/rascunhos/{situacao!.Id}", Token),
            HttpStatusCode.NotFound,
            "RASCUNHO_NAO_ENCONTRADO");

        var criada = await bia.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                TipoAtividade.Calistenia, 1800, DateTimeOffset.UtcNow.AddHours(-1), null, TipoComprovacao.Fotografia, situacao.Id),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        await ApiTestHost.AssertProblemAsync(criada, HttpStatusCode.NotFound, "RASCUNHO_NAO_ENCONTRADO");
        Assert.Equal(0, await _host.CountAsync("SELECT count(*) FROM atividades"));
    }

    [Fact]
    public async Task Salvar_RecusaTreinoComMaisDe48Horas()
    {
        var ana = await _host.CreateUsuarioAsync();
        var app = await ConectarAsync(ana);

        var response = await PutRascunhoAsync(app, "treino-1", DateTimeOffset.UtcNow.AddHours(-49));

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "ATIVIDADE_MUITO_ANTIGA");
    }

    private async Task<HttpClient> ConectarAsync(TestUsuario usuario)
    {
        var (verifier, challenge) = CriarPkce();
        var codigo = await AutorizarAsync(usuario, challenge);
        var response = await TrocarAsync(codigo, verifier);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.TrocarCodigoResponse, Token);

        var app = CriarClienteDoApp();
        app.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        return app;
    }

    private static async Task<string> AutorizarAsync(TestUsuario usuario, string challenge)
    {
        var response = await usuario.Client.PostAsJsonAsync(
            "/api/integracoes/autorizacoes",
            new AutorizarAppRequest(AppIntegracao.Movr, challenge, RedirectUri),
            AppJsonContext.Default.AutorizarAppRequest,
            Token);
        response.EnsureSuccessStatusCode();
        var autorizacao = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AutorizarAppResponse, Token);
        Assert.Equal(RedirectUri, autorizacao!.RedirectUri);
        return autorizacao.Codigo;
    }

    private Task<HttpResponseMessage> TrocarAsync(string codigo, string verifier) =>
        CriarClienteDoApp().PostAsJsonAsync(
            "/api/integracoes/token",
            new TrocarCodigoRequest(codigo, verifier, RedirectUri),
            AppJsonContext.Default.TrocarCodigoRequest,
            Token);

    /// <summary>O app não é navegador: sem cookie e sem cabeçalho <c>Origin</c>.</summary>
    private HttpClient CriarClienteDoApp()
    {
        var client = _host.CreateAnonimo();
        client.DefaultRequestHeaders.Remove("Origin");
        return client;
    }

    private static async Task<HttpResponseMessage> PutRascunhoAsync(
        HttpClient app,
        string externalId,
        DateTimeOffset? startedAt = null)
    {
        var inicio = startedAt ?? DateTimeOffset.UtcNow.AddHours(-1);
        using var form = new MultipartFormDataContent
        {
            { new StringContent(TipoAtividade.Calistenia), "tipo" },
            { new StringContent("1800"), "duracaoSegundos" },
            { new StringContent(inicio.ToString("O", CultureInfo.InvariantCulture)), "startedAt" },
            { new StringContent(inicio.AddMinutes(32).ToString("O", CultureInfo.InvariantCulture)), "endedAt" },
        };
        var arquivo = new ByteArrayContent(JpegMinimo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(arquivo, "arquivo", "resumo.jpg");
        var miniatura = new ByteArrayContent(JpegMinimo);
        miniatura.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(miniatura, "miniatura", "miniatura.jpg");

        return await app.PutAsync($"/api/integracoes/rascunhos/{externalId}", form, Token);
    }

    private static (string Verifier, string Challenge) CriarPkce()
    {
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }
}
