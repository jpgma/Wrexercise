using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Atividades;

/// <summary>O treino ao vivo: "Começar treino" e "Terminar treino" (ADR 0078).</summary>
public sealed class TreinoAoVivoTests : IAsyncLifetime
{
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
    public async Task Comecar_CriaTreinoEmAndamentoSemRevisores()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await _host.AddMembroAsync(grupoId, nome: "Bia");

        var response = await ComecarAsync(ana, grupoId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var atividade = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        Assert.Equal(SituacaoVerificacao.EmAndamento, atividade!.SituacaoVerificacao);
        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM revisores_atividade"));
        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM notificacoes WHERE tipo = 'NovaAtividade'"));
        Assert.Equal(1L, await _host.CountAsync("SELECT count(*) FROM notificacoes WHERE tipo = 'ComecouTreinar'"));

        var atual = await ana.Client.GetFromJsonAsync(
            "/api/atividades/em-andamento", AppJsonContext.Default.TreinoAtualResponse, Token);
        Assert.Equal(atividade.Id, atual!.AtividadeId);
        Assert.Equal(grupoId, atual.GrupoId);
    }

    [Fact]
    public async Task Comecar_ComLocal_GuardaEMostraOMapaAoGrupo()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var estranho = await _host.CreateUsuarioAsync("Estranho");

        var atividade = await LerAsync(await ComecarAsync(ana, grupoId, -23.5505, -46.6333));
        var vistaPelaBia = await bia.Client.GetFromJsonAsync(
            $"/api/atividades/{atividade.Id}", AppJsonContext.Default.AtividadeResponse, Token);
        var mapa = await bia.Client.GetAsync($"/api/atividades/{atividade.Id}/mapa/0/1", Token);
        var deFora = await estranho.Client.GetAsync($"/api/atividades/{atividade.Id}/mapa/0/0", Token);

        Assert.Equal(-23.5505, vistaPelaBia!.Latitude);
        Assert.Equal(-46.6333, vistaPelaBia.Longitude);
        Assert.Equal(HttpStatusCode.OK, mapa.StatusCode);
        Assert.Equal("image/png", mapa.Content.Headers.ContentType?.MediaType);
        await ApiTestHost.AssertProblemAsync(deFora, HttpStatusCode.NotFound, "ATIVIDADE_NAO_ENCONTRADA");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(-23.5505, null)]
    [InlineData(91.0, -46.6333)]
    [InlineData(0.0, 0.0)]
    public async Task Comecar_SemLocalOuInvalido_ComecaSemLocal(double? latitude, double? longitude)
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);

        var response = await ComecarAsync(ana, grupoId, latitude, longitude);
        var atividade = await LerAsync(response);
        var mapa = await ana.Client.GetAsync($"/api/atividades/{atividade.Id}/mapa/0/0", Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(atividade.Latitude);
        Assert.Null(atividade.Longitude);
        await ApiTestHost.AssertProblemAsync(mapa, HttpStatusCode.NotFound, "ATIVIDADE_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task SemTreino_EmAndamentoDevolve204()
    {
        var ana = await _host.CreateUsuarioAsync();

        var response = await ana.Client.GetAsync("/api/atividades/em-andamento", Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Comecar_DeNovoNoMesmoGrupoDevolveOMesmoTreino_EmOutroGrupoRecusa()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoA = await _host.CreateGrupoAsync(ana, "A");
        var grupoB = await _host.CreateGrupoAsync(ana, "B");
        var primeiro = await LerAsync(await ComecarAsync(ana, grupoA));

        var repetido = await ComecarAsync(ana, grupoA);
        var outroGrupo = await ComecarAsync(ana, grupoB);

        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.Equal(primeiro.Id, (await LerAsync(repetido)).Id);
        await ApiTestHost.AssertProblemAsync(outroGrupo, HttpStatusCode.Conflict, "TREINO_EM_ANDAMENTO");
    }

    [Fact]
    public async Task Registrar_ComTreinoAoVivoAberto_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        await ComecarAsync(ana, grupoId);

        var response = await ana.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                TipoAtividade.Corrida, 3600, DateTimeOffset.UtcNow.AddHours(-3), null, TipoComprovacao.RegistroManual),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Conflict, "TREINO_EM_ANDAMENTO");
    }

    [Fact]
    public async Task Terminar_ViraRegistroComumEAvisaOsRevisores()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var treino = await LerAsync(await ComecarAsync(ana, grupoId));
        Assert.Equal(HttpStatusCode.Created, (await PostEvidenciaAsync(ana, treino.Id)).StatusCode);
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(40));

        var response = await TerminarAsync(ana, treino.Id, new(1800, "Bom treino", TipoComprovacao.Fotografia));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var atividade = await LerAsync(response);
        Assert.Equal(SituacaoVerificacao.AguardandoFeedback, atividade.SituacaoVerificacao);
        Assert.Equal(1800, atividade.DuracaoSegundos);
        Assert.Single(atividade.Evidencias);
        // O envio passa a ser agora: a janela de avaliação e a exclusão contam do término.
        Assert.True(atividade.SubmittedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.NotNull(atividade.ExclusaoPermitidaAte);
        Assert.Equal(1L, await _host.CountAsync(
            $"SELECT count(*) FROM revisores_atividade WHERE usuario_id = '{bia.Id}'"));
        Assert.Equal(1L, await _host.CountAsync(
            $"SELECT count(*) FROM notificacoes WHERE tipo = 'NovaAtividade' AND usuario_id = '{bia.Id}'"));
        var atual = await ana.Client.GetAsync("/api/atividades/em-andamento", Token);
        Assert.Equal(HttpStatusCode.NoContent, atual.StatusCode);
    }

    [Fact]
    public async Task Terminar_SemColegasAcordados_VerificaNaHora()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId));
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(20));

        var atividade = await LerAsync(
            await TerminarAsync(ana, treino.Id, new(900, null, TipoComprovacao.RegistroManual)));

        Assert.Equal("Verificada", atividade.SituacaoVerificacao);
    }

    [Fact]
    public async Task Terminar_RecusaDuracaoMaiorQueOTempoDecorrido()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId));
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(10));

        var response = await TerminarAsync(ana, treino.Id, new(3600, null, TipoComprovacao.RegistroManual));

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task Terminar_ComEvidencia_NaoAceitaRegistroManual()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId));
        await PostEvidenciaAsync(ana, treino.Id);
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(10));

        var response = await TerminarAsync(ana, treino.Id, new(300, null, TipoComprovacao.RegistroManual));

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task Terminar_DuasVezes_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId));
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(10));
        await TerminarAsync(ana, treino.Id, new(300, null, TipoComprovacao.RegistroManual));

        var response = await TerminarAsync(ana, treino.Id, new(300, null, TipoComprovacao.RegistroManual));

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Conflict, "TREINO_NAO_EM_ANDAMENTO");
    }

    [Fact]
    public async Task Cancelar_ApagaOTreinoMesmoDepoisDe15Minutos()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId));
        await _host.ExecuteAsync($"""
            UPDATE atividades SET started_at = now() - interval '1 hour', submitted_at = now() - interval '1 hour'
            WHERE id = '{treino.Id}'
            """);

        var response = await ana.Client.DeleteAsync($"/api/atividades/{treino.Id}", Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM atividades"));
    }

    [Fact]
    public async Task EmAndamento_NaoContaParaAEnergiaNemParaAFilaDeAvaliacao()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await ComecarAsync(ana, grupoId);

        var fila = await bia.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/atividades/para-avaliar", AppJsonContext.Default.FilaAvaliacaoResponse, Token);
        var energia = await ana.Client.GetFromJsonAsync(
            "/api/jogo/energia", AppJsonContext.Default.EnergiaResponse, Token);

        Assert.Empty(fila!.Atividades);
        Assert.Equal(0, energia!.Saldo);
    }

    [Fact]
    public async Task Torcer_UmaVezACada10Minutos_ENuncaNoProprioTreino()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var treino = await LerAsync(await ComecarAsync(ana, grupoId));

        var primeira = await bia.Client.PostAsync($"/api/atividades/{treino.Id}/torcida", null, Token);
        var segunda = await bia.Client.PostAsync($"/api/atividades/{treino.Id}/torcida", null, Token);
        var propria = await ana.Client.PostAsync($"/api/atividades/{treino.Id}/torcida", null, Token);

        Assert.Equal(HttpStatusCode.NoContent, primeira.StatusCode);
        await ApiTestHost.AssertProblemAsync(segunda, HttpStatusCode.TooManyRequests, "TORCIDA_MUITO_CEDO");
        await ApiTestHost.AssertProblemAsync(propria, HttpStatusCode.Forbidden, "TORCIDA_PROPRIA");
        var atual = await ana.Client.GetFromJsonAsync(
            "/api/atividades/em-andamento", AppJsonContext.Default.TreinoAtualResponse, Token);
        var torcida = Assert.Single(atual!.Torcidas);
        Assert.Equal(bia.Id, torcida.UsuarioId);
        Assert.Equal(1L, await _host.CountAsync("SELECT count(*) FROM notificacoes WHERE tipo = 'Torcida'"));
    }

    [Fact]
    public async Task Torcer_ForaDoGrupo_NaoEncontra()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var estranho = await _host.CreateUsuarioAsync("Estranho");
        var treino = await LerAsync(await ComecarAsync(ana, grupoId));

        var response = await estranho.Client.PostAsync($"/api/atividades/{treino.Id}/torcida", null, Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.NotFound, "ATIVIDADE_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task Cena_MostraQuemTreinaAoVivo()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var treino = await LerAsync(await ComecarAsync(ana, grupoId));

        var cena = await bia.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/cena", AppJsonContext.Default.CenaResponse, Token);

        var membro = cena!.Membros.Single(m => m.UsuarioId == ana.Id);
        Assert.Equal("treinando", membro.Pose);
        Assert.Equal(treino.Id, membro.TreinoAoVivo!.AtividadeId);
        Assert.Null(cena.Membros.Single(m => m.UsuarioId == bia.Id).TreinoAoVivo);
    }

    [Fact]
    public async Task Expiracao_LembraDepoisDe3HorasEApagaDepoisDe24()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.CreateUsuarioAsync("Bia");
        var grupoBia = await _host.CreateGrupoAsync(bia);
        var longo = await LerAsync(await ComecarAsync(ana, grupoId));
        var esquecido = await LerAsync(await ComecarAsync(bia, grupoBia));
        await RecuarInicioAsync(longo.Id, TimeSpan.FromHours(4));
        await RecuarInicioAsync(esquecido.Id, TimeSpan.FromHours(25));

        var (lembretes, expirados) = await ExpiracaoTreinos.ProcessarAsync(
            _host.Services.GetRequiredService<NpgsqlDataSource>(),
            _host.Services.GetRequiredService<ArmazenamentoEvidencias>(),
            Token);
        var (lembretesDeNovo, _) = await ExpiracaoTreinos.ProcessarAsync(
            _host.Services.GetRequiredService<NpgsqlDataSource>(),
            _host.Services.GetRequiredService<ArmazenamentoEvidencias>(),
            Token);

        Assert.Equal(2, lembretes);
        Assert.Equal(1, expirados);
        Assert.Equal(0, lembretesDeNovo);
        Assert.Equal(1L, await _host.CountAsync($"SELECT count(*) FROM atividades WHERE id = '{longo.Id}'"));
        Assert.Equal(0L, await _host.CountAsync($"SELECT count(*) FROM atividades WHERE id = '{esquecido.Id}'"));
    }

    [Fact]
    public async Task Evidencias_AceitaAte10()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId));
        await _host.ExecuteAsync($"""
            INSERT INTO evidencias_atividade (atividade_id, caminho_relativo, tipo_mime, tamanho_bytes, sha256, ordem)
            SELECT '{treino.Id}', 'falsa-' || n, 'image/jpeg', 10, lpad(n::text, 64, '0'), n
            FROM generate_series(1, 10) AS n
            """);

        var response = await PostEvidenciaAsync(ana, treino.Id);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Conflict, "LIMITE_EVIDENCIAS");
    }

    private Task RecuarInicioAsync(Guid atividadeId, TimeSpan quanto) =>
        _host.ExecuteAsync($"""
            UPDATE atividades SET started_at = started_at - interval '{(int)quanto.TotalSeconds} seconds'
            WHERE id = '{atividadeId}'
            """);

    private static Task<HttpResponseMessage> ComecarAsync(
        TestUsuario usuario,
        Guid grupoId,
        double? latitude = null,
        double? longitude = null) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades/em-andamento",
            new ComecarTreinoRequest(TipoAtividade.Musculacao, latitude, longitude),
            AppJsonContext.Default.ComecarTreinoRequest,
            Token);

    private static Task<HttpResponseMessage> TerminarAsync(
        TestUsuario usuario,
        Guid atividadeId,
        TerminarTreinoRequest body) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/atividades/{atividadeId}/terminar",
            body,
            AppJsonContext.Default.TerminarTreinoRequest,
            Token);

    private static async Task<AtividadeResponse> LerAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token))!;

    private static Task<HttpResponseMessage> PostEvidenciaAsync(TestUsuario usuario, Guid atividadeId)
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(JpegMinimo);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(bytes, "arquivo", "foto.jpg");
        return usuario.Client.PostAsync($"/api/atividades/{atividadeId}/evidencias", content, Token);
    }
}
