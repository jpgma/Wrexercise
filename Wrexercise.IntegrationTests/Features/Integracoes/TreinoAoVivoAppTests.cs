using System.Buffers.Text;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Integracoes;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Integracoes;

/// <summary>O treino ao vivo começado pelo app conectado (ADR 0081).</summary>
public sealed class TreinoAoVivoAppTests : IAsyncLifetime
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
    public async Task Grupos_ListaSoOsGruposDaPessoa()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var primeiro = await _host.CreateGrupoAsync(ana, "Primeiro");
        var bia = await _host.CreateUsuarioAsync("Bia");
        await _host.CreateGrupoAsync(bia, "Da Bia");
        var app = await ConectarAsync(ana);

        var resposta = await app.GetFromJsonAsync(
            "/api/integracoes/grupos", AppJsonContext.Default.GruposAppResponse, Token);

        var grupo = Assert.Single(resposta!.Grupos);
        Assert.Equal(primeiro, grupo.Id);
        Assert.Equal("Primeiro", grupo.Nome);
    }

    [Fact]
    public async Task Rotas_ExigemOTokenDoApp()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);

        // O cookie do navegador não abre as rotas do app.
        var comCookie = await ana.Client.PutAsJsonAsync(
            "/api/integracoes/treinos/treino-1",
            new ComecarTreinoAppRequest(grupoId, TipoAtividade.Calistenia),
            AppJsonContext.Default.ComecarTreinoAppRequest,
            Token);
        await ApiTestHost.AssertProblemAsync(comCookie, HttpStatusCode.Unauthorized, "APP_NAO_AUTORIZADO");

        var semToken = await CriarClienteDoApp().GetAsync("/api/integracoes/grupos", Token);
        await ApiTestHost.AssertProblemAsync(semToken, HttpStatusCode.Unauthorized, "APP_NAO_AUTORIZADO");
    }

    [Fact]
    public async Task Comecar_CriaORepeteEAvisaOGrupo()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana, "Galera");
        await _host.AddMembroAsync(grupoId, nome: "Bia");
        var app = await ConectarAsync(ana);

        var criado = await ComecarAsync(app, "treino-1", grupoId, -23.55, -46.63);
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var treino = await LerAsync(criado);
        Assert.Equal(SituacaoVerificacao.EmAndamento, treino.Situacao);
        Assert.Equal("Galera", treino.GrupoNome);
        Assert.Equal($"/grupos/{grupoId}/treino", treino.Url);
        Assert.False(treino.FimInformado);

        var repetido = await ComecarAsync(app, "treino-1", grupoId);
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.Equal(treino.AtividadeId, (await LerAsync(repetido)).AtividadeId);

        Assert.Equal(1L, await _host.CountAsync("SELECT count(*) FROM atividades"));
        Assert.Equal(-23.55, await _host.ScalarAsync<double>(
            "SELECT latitude FROM atividades WHERE id = $1", treino.AtividadeId));
        Assert.Equal(1L, await _host.CountAsync("SELECT count(*) FROM notificacoes WHERE tipo = 'ComecouTreinar'"));
        Assert.Equal(TipoAtividade.Calistenia, await _host.ScalarAsync<string>(
            "SELECT tipo FROM atividades WHERE id = $1", treino.AtividadeId));
    }

    [Fact]
    public async Task Comecar_RecusaOutroTreinoAberto()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var outroGrupo = await _host.CreateGrupoAsync(ana, "Outro");
        var app = await ConectarAsync(ana);
        Assert.Equal(HttpStatusCode.Created, (await ComecarAsync(app, "treino-1", grupoId)).StatusCode);

        await ApiTestHost.AssertProblemAsync(
            await ComecarAsync(app, "treino-2", grupoId), HttpStatusCode.Conflict, "TREINO_EM_ANDAMENTO");
        await ApiTestHost.AssertProblemAsync(
            await ComecarAsync(app, "treino-3", outroGrupo), HttpStatusCode.Conflict, "TREINO_EM_ANDAMENTO");
    }

    [Fact]
    public async Task Comecar_AdotaOTreinoAbertoNoNavegadorNoMesmoGrupo()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);
        var pelaWeb = await ana.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades/em-andamento",
            new ComecarTreinoRequest(TipoAtividade.Musculacao),
            AppJsonContext.Default.ComecarTreinoRequest,
            Token);
        var aberto = await pelaWeb.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        var response = await ComecarAsync(app, "treino-1", grupoId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(aberto!.Id, (await LerAsync(response)).AtividadeId);
        Assert.Equal("treino-1", await _host.ScalarAsync<string>(
            "SELECT app_external_id FROM atividades WHERE id = $1", aberto.Id));
    }

    [Fact]
    public async Task Fim_GuardaASugestaoEAnexaOResumoUmaVez()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);
        var treino = await LerAsync(await ComecarAsync(app, "treino-1", grupoId));
        await RecuarInicioAsync(treino.AtividadeId, TimeSpan.FromMinutes(40));

        var fim = await PutFimAsync(app, "treino-1", 3600, DateTimeOffset.UtcNow.AddMinutes(-1), comResumo: true);
        Assert.Equal(HttpStatusCode.OK, fim.StatusCode);
        var depois = await LerAsync(fim);
        Assert.True(depois.FimInformado);
        Assert.True(depois.ResumoAnexado);
        Assert.Equal(SituacaoVerificacao.EmAndamento, depois.Situacao);

        // Repetir atualiza a sugestão e não duplica a imagem.
        Assert.Equal(HttpStatusCode.OK, (await PutFimAsync(
            app, "treino-1", 1800, DateTimeOffset.UtcNow.AddMinutes(-1), comResumo: true)).StatusCode);
        Assert.Equal(1L, await _host.CountAsync(
            $"SELECT count(*) FROM evidencias_atividade WHERE atividade_id = '{treino.AtividadeId}'"));
        Assert.Equal("Integracao", await _host.ScalarAsync<string>(
            "SELECT origem_data_captura FROM evidencias_atividade WHERE atividade_id = $1", treino.AtividadeId));
        Assert.Equal(1800, await _host.ScalarAsync<int>(
            "SELECT app_duracao_segundos FROM atividades WHERE id = $1", treino.AtividadeId));

        var atual = await ana.Client.GetFromJsonAsync(
            "/api/atividades/em-andamento", AppJsonContext.Default.TreinoAtualResponse, Token);
        Assert.Equal(AppIntegracao.Movr, atual!.App);
        Assert.Equal(1800, atual.AppDuracaoSegundos);
        Assert.NotNull(atual.AppEndedAt);
        Assert.Equal(1, atual.TotalEvidencias);
    }

    [Fact]
    public async Task Fim_LimitaASugestaoAoTempoDecorrido()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);
        var treino = await LerAsync(await ComecarAsync(app, "treino-1", grupoId));
        await RecuarInicioAsync(treino.AtividadeId, TimeSpan.FromMinutes(10));

        Assert.Equal(HttpStatusCode.OK, (await PutFimAsync(
            app, "treino-1", 3600, DateTimeOffset.UtcNow, comResumo: false)).StatusCode);

        var sugestao = await _host.ScalarAsync<int>(
            "SELECT app_duracao_segundos FROM atividades WHERE id = $1", treino.AtividadeId);
        Assert.InRange(sugestao, 600, 610);
        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM evidencias_atividade"));
    }

    [Fact]
    public async Task Terminar_PelaPessoaComOResumoDoApp()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);
        var treino = await LerAsync(await ComecarAsync(app, "treino-1", grupoId));
        await RecuarInicioAsync(treino.AtividadeId, TimeSpan.FromMinutes(30));
        await PutFimAsync(app, "treino-1", 1500, DateTimeOffset.UtcNow, comResumo: true);

        var terminado = await ana.Client.PostAsJsonAsync(
            $"/api/atividades/{treino.AtividadeId}/terminar",
            new TerminarTreinoRequest(1500, null, TipoComprovacao.Fotografia),
            AppJsonContext.Default.TerminarTreinoRequest,
            Token);
        Assert.Equal(HttpStatusCode.OK, terminado.StatusCode);

        var situacao = await LerAsync(await app.GetAsync("/api/integracoes/treinos/treino-1", Token));
        Assert.NotEqual(SituacaoVerificacao.EmAndamento, situacao.Situacao);
        Assert.Equal($"/atividades/{treino.AtividadeId}", situacao.Url);

        // Depois de terminado, o treino é da pessoa: o app não informa o fim, não cancela e não recomeça.
        await ApiTestHost.AssertProblemAsync(
            await PutFimAsync(app, "treino-1", 1500, DateTimeOffset.UtcNow, comResumo: false),
            HttpStatusCode.Conflict,
            "TREINO_NAO_EM_ANDAMENTO");
        await ApiTestHost.AssertProblemAsync(
            await app.DeleteAsync("/api/integracoes/treinos/treino-1", Token),
            HttpStatusCode.Conflict,
            "TREINO_NAO_EM_ANDAMENTO");
        await ApiTestHost.AssertProblemAsync(
            await ComecarAsync(app, "treino-1", grupoId), HttpStatusCode.Conflict, "TREINO_NAO_EM_ANDAMENTO");
        Assert.Equal(1L, await _host.CountAsync("SELECT count(*) FROM atividades"));
    }

    [Fact]
    public async Task Cancelar_ApagaOTreinoAberto()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);
        await ComecarAsync(app, "treino-1", grupoId);
        await PutFimAsync(app, "treino-1", 120, DateTimeOffset.UtcNow, comResumo: true);

        var cancelado = await app.DeleteAsync("/api/integracoes/treinos/treino-1", Token);

        Assert.Equal(HttpStatusCode.NoContent, cancelado.StatusCode);
        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM atividades"));
        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM evidencias_atividade"));
        await ApiTestHost.AssertProblemAsync(
            await app.GetAsync("/api/integracoes/treinos/treino-1", Token),
            HttpStatusCode.NotFound,
            "TREINO_NAO_ENCONTRADO");
        await ApiTestHost.AssertProblemAsync(
            await app.DeleteAsync("/api/integracoes/treinos/treino-1", Token),
            HttpStatusCode.NotFound,
            "TREINO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Lembrete_AvisaUmaVez30MinutosDepoisDoFimNoApp()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);
        var treino = await LerAsync(await ComecarAsync(app, "treino-1", grupoId));
        await PutFimAsync(app, "treino-1", 120, DateTimeOffset.UtcNow, comResumo: false);

        Assert.Equal(0, (await ProcessarAsync()).Lembretes);

        await _host.ExecuteAsync($"""
            UPDATE atividades SET app_ended_at = now() - interval '31 minutes' WHERE id = '{treino.AtividadeId}'
            """);
        Assert.Equal(1, (await ProcessarAsync()).Lembretes);
        Assert.Equal(0, (await ProcessarAsync()).Lembretes);
        Assert.Equal(1L, await _host.CountAsync(
            "SELECT count(*) FROM notificacoes WHERE tipo = 'TreinoAppConcluido'"));
    }

    private Task<(int Lembretes, int Expirados)> ProcessarAsync() =>
        ExpiracaoTreinos.ProcessarAsync(
            _host.Services.GetRequiredService<NpgsqlDataSource>(),
            _host.Services.GetRequiredService<ArmazenamentoEvidencias>(),
            Token);

    private Task RecuarInicioAsync(Guid atividadeId, TimeSpan quanto) =>
        _host.ExecuteAsync($"""
            UPDATE atividades SET started_at = started_at - interval '{(int)quanto.TotalSeconds} seconds'
            WHERE id = '{atividadeId}'
            """);

    private static Task<HttpResponseMessage> ComecarAsync(
        HttpClient app,
        string externalId,
        Guid grupoId,
        double? latitude = null,
        double? longitude = null) =>
        app.PutAsJsonAsync(
            $"/api/integracoes/treinos/{externalId}",
            new ComecarTreinoAppRequest(grupoId, TipoAtividade.Calistenia, latitude, longitude),
            AppJsonContext.Default.ComecarTreinoAppRequest,
            Token);

    private static async Task<HttpResponseMessage> PutFimAsync(
        HttpClient app,
        string externalId,
        int duracaoSegundos,
        DateTimeOffset endedAt,
        bool comResumo)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(duracaoSegundos.ToString(CultureInfo.InvariantCulture)), "duracaoSegundos" },
            { new StringContent(endedAt.ToString("O", CultureInfo.InvariantCulture)), "endedAt" },
        };
        if (comResumo)
        {
            var arquivo = new ByteArrayContent(JpegMinimo);
            arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(arquivo, "arquivo", "resumo.jpg");
            var miniatura = new ByteArrayContent(JpegMinimo);
            miniatura.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(miniatura, "miniatura", "miniatura.jpg");
        }

        return await app.PutAsync($"/api/integracoes/treinos/{externalId}/fim", form, Token);
    }

    private static async Task<TreinoAppResponse> LerAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.TreinoAppResponse, Token))!;
    }

    private async Task<HttpClient> ConectarAsync(TestUsuario usuario)
    {
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var autorizacao = await usuario.Client.PostAsJsonAsync(
            "/api/integracoes/autorizacoes",
            new AutorizarAppRequest(AppIntegracao.Movr, challenge, RedirectUri),
            AppJsonContext.Default.AutorizarAppRequest,
            Token);
        autorizacao.EnsureSuccessStatusCode();
        var codigo = await autorizacao.Content.ReadFromJsonAsync(AppJsonContext.Default.AutorizarAppResponse, Token);

        var troca = await CriarClienteDoApp().PostAsJsonAsync(
            "/api/integracoes/token",
            new TrocarCodigoRequest(codigo!.Codigo, verifier, RedirectUri),
            AppJsonContext.Default.TrocarCodigoRequest,
            Token);
        troca.EnsureSuccessStatusCode();
        var token = await troca.Content.ReadFromJsonAsync(AppJsonContext.Default.TrocarCodigoResponse, Token);

        var app = CriarClienteDoApp();
        app.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        return app;
    }

    /// <summary>O app não é navegador: sem cookie e sem cabeçalho <c>Origin</c>.</summary>
    private HttpClient CriarClienteDoApp()
    {
        var client = _host.CreateAnonimo();
        client.DefaultRequestHeaders.Remove("Origin");
        return client;
    }
}
