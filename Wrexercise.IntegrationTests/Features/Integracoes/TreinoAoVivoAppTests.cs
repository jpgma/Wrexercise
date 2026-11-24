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
    public async Task Percurso_OAppGravaEMandaOsPontosSemOLocalDeInicio()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);

        var criado = await LerAsync(await ComecarComPercursoAsync(app, "corrida-1", grupoId, TipoAtividade.Corrida));
        Assert.Equal(-1, criado.PercursoUltimoSeq);
        Assert.True(await _host.ScalarAsync<bool>(
            "SELECT latitude IS NULL FROM atividades WHERE id = $1", criado.AtividadeId));
        await RecuarInicioAsync(criado.AtividadeId, TimeSpan.FromMinutes(5));

        // 3 m/s para o norte, um ponto por segundo, como o Movr grava.
        var lote = Reta(0, 61, 3, DateTimeOffset.UtcNow.AddSeconds(-120));
        var primeiro = await EnviarPontosAsync(app, "corrida-1", lote);
        var repetido = await EnviarPontosAsync(app, "corrida-1", lote);

        Assert.Equal(HttpStatusCode.OK, primeiro.StatusCode);
        var resposta = await repetido.Content.ReadFromJsonAsync(AppJsonContext.Default.PontosPercursoResponse, Token);
        Assert.Equal(60, resposta!.UltimoSeq);
        Assert.Equal(61, resposta.TotalPontos);
        Assert.InRange(resposta.DistanciaMetros, 150, 185);

        var situacao = await app.GetFromJsonAsync(
            "/api/integracoes/treinos/corrida-1", AppJsonContext.Default.TreinoAppResponse, Token);
        Assert.Equal(60, situacao!.PercursoUltimoSeq);
    }

    [Fact]
    public async Task Percurso_SemPercursoOuSemTreino_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);

        // Calistenia não tem percurso, mesmo pedindo.
        var treino = await LerAsync(await ComecarComPercursoAsync(app, "treino-1", grupoId, TipoAtividade.Calistenia));
        Assert.Null(treino.PercursoUltimoSeq);
        var lote = Reta(0, 5, 3, DateTimeOffset.UtcNow);

        await ApiTestHost.AssertProblemAsync(
            await EnviarPontosAsync(app, "treino-1", lote), HttpStatusCode.NotFound, "PERCURSO_NAO_ENCONTRADO");
        await ApiTestHost.AssertProblemAsync(
            await EnviarPontosAsync(app, "outro", lote), HttpStatusCode.NotFound, "TREINO_NAO_ENCONTRADO");
        await ApiTestHost.AssertProblemAsync(
            await EnviarPontosAsync(app, "treino-1", []), HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task Percurso_DepoisDeTerminadoAquiRecusaOsPontos()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);
        var treino = await LerAsync(await ComecarComPercursoAsync(app, "caminhada-1", grupoId, TipoAtividade.Caminhada));
        await _host.ExecuteAsync(
            $"UPDATE atividades SET situacao_verificacao = 'AguardandoFeedback' WHERE id = '{treino.AtividadeId}'");

        await ApiTestHost.AssertProblemAsync(
            await EnviarPontosAsync(app, "caminhada-1", Reta(0, 5, 3, DateTimeOffset.UtcNow)),
            HttpStatusCode.Conflict,
            "TREINO_NAO_EM_ANDAMENTO");
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
        await RecuarInicioAsync(treino.AtividadeId, TimeSpan.FromMinutes(10) + FolgaRelogio);

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
    public async Task Resumo_DepoisDeTerminadoSemFotos_EntraUmaVezComoAdicionadaDepois()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);
        var treino = await LerAsync(await ComecarAsync(app, "treino-1", grupoId));
        await RecuarInicioAsync(treino.AtividadeId, TimeSpan.FromMinutes(30));

        // Enquanto o treino está aberto, o resumo vai pelo fim.
        await ApiTestHost.AssertProblemAsync(
            await PutResumoAsync(app, "treino-1"), HttpStatusCode.Conflict, "TREINO_EM_ANDAMENTO");

        // O fim chegou sem a imagem, e a pessoa terminou sem fotos: a atividade é um registro manual.
        await PutFimAsync(app, "treino-1", 1500, DateTimeOffset.UtcNow, comResumo: false);
        var terminado = await ana.Client.PostAsJsonAsync(
            $"/api/atividades/{treino.AtividadeId}/terminar",
            new TerminarTreinoRequest(1500, null, TipoComprovacao.RegistroManual),
            AppJsonContext.Default.TerminarTreinoRequest,
            Token);
        Assert.Equal(HttpStatusCode.OK, terminado.StatusCode);
        var antes = await ObterAtividadeAsync(ana, treino.AtividadeId);

        var anexado = await LerAsync(await PutResumoAsync(app, "treino-1"));
        Assert.True(anexado.ResumoAnexado);
        var repetido = await LerAsync(await PutResumoAsync(app, "treino-1"));
        Assert.True(repetido.ResumoAnexado);

        var depois = await ObterAtividadeAsync(ana, treino.AtividadeId);
        var evidencia = Assert.Single(depois.Evidencias);
        Assert.True(evidencia.AdicionadaDepois);
        Assert.Equal(OrigemDataCaptura.Integracao, evidencia.OrigemDataCaptura);
        Assert.Equal(TipoComprovacao.RegistroManual, depois.TipoComprovacao);
        Assert.Equal(antes.SituacaoVerificacao, depois.SituacaoVerificacao);

        // A repetição não deixa arquivo sobrando: só a imagem e a miniatura do primeiro envio.
        Assert.Equal(2, Directory.EnumerateFiles(_host.EvidenciasDiretorio, "*", SearchOption.AllDirectories).Count());
    }

    [Fact]
    public async Task Resumo_AtividadeAnulada_Recusado()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var app = await ConectarAsync(ana);
        var treino = await LerAsync(await ComecarAsync(app, "treino-1", grupoId));
        await RecuarInicioAsync(treino.AtividadeId, TimeSpan.FromMinutes(30));
        await ana.Client.PostAsJsonAsync(
            $"/api/atividades/{treino.AtividadeId}/terminar",
            new TerminarTreinoRequest(1500, null, TipoComprovacao.RegistroManual),
            AppJsonContext.Default.TerminarTreinoRequest,
            Token);
        await _host.ExecuteAsync($"UPDATE atividades SET situacao_verificacao = 'Anulada' WHERE id = '{treino.AtividadeId}'");

        await ApiTestHost.AssertProblemAsync(
            await PutResumoAsync(app, "treino-1"), HttpStatusCode.Conflict, "ATIVIDADE_ENCERRADA");
        Assert.Empty(Directory.EnumerateFiles(_host.EvidenciasDiretorio, "*", SearchOption.AllDirectories));
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

    /// <summary>
    /// Folga para conferir a sugestão contra o tempo recuado: o início vem do relógio do banco (<c>now()</c>), o
    /// fim do relógio do servidor, e o tempo decorrido é arredondado para baixo. Sem ela, alguns milissegundos de
    /// diferença viram 599 s.
    /// </summary>
    private static readonly TimeSpan FolgaRelogio = TimeSpan.FromSeconds(5);

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

    private static Task<HttpResponseMessage> ComecarComPercursoAsync(
        HttpClient app,
        string externalId,
        Guid grupoId,
        string tipo) =>
        app.PutAsJsonAsync(
            $"/api/integracoes/treinos/{externalId}",
            new ComecarTreinoAppRequest(grupoId, tipo, -23.55, -46.63, GravarPercurso: true),
            AppJsonContext.Default.ComecarTreinoAppRequest,
            Token);

    private static Task<HttpResponseMessage> EnviarPontosAsync(
        HttpClient app,
        string externalId,
        PontoPercursoRequest[] pontos) =>
        app.PutAsJsonAsync(
            $"/api/integracoes/treinos/{externalId}/percurso/pontos",
            new EnviarPontosPercursoRequest(pontos),
            AppJsonContext.Default.EnviarPontosPercursoRequest,
            Token);

    /// <summary>Pontos para o norte, a <paramref name="metros"/> por segundo, com precisão de 5 m.</summary>
    private static PontoPercursoRequest[] Reta(int seqInicial, int total, double metros, DateTimeOffset de) =>
        Enumerable.Range(0, total)
            .Select(i => new PontoPercursoRequest(
                seqInicial + i,
                de.AddSeconds(i),
                -23.55 + (i * metros / (6_371_000 * Math.PI / 180)),
                -46.63,
                5,
                null,
                0))
            .ToArray();

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

    private static async Task<HttpResponseMessage> PutResumoAsync(HttpClient app, string externalId)
    {
        using var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(JpegMinimo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(arquivo, "arquivo", "resumo.jpg");
        var miniatura = new ByteArrayContent(JpegMinimo);
        miniatura.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(miniatura, "miniatura", "miniatura.jpg");
        return await app.PutAsync($"/api/integracoes/treinos/{externalId}/resumo", form, Token);
    }

    private static async Task<AtividadeResponse> ObterAtividadeAsync(TestUsuario usuario, Guid atividadeId) =>
        (await usuario.Client.GetFromJsonAsync(
            $"/api/atividades/{atividadeId}", AppJsonContext.Default.AtividadeResponse, Token))!;

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
