using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Jogo;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Jogo;

public sealed class RivaisRoutesTests : IAsyncLifetime
{
    private const string HojeNoGrupo = "(now() AT TIME ZONE 'America/Sao_Paulo')::date";
    private const string SegundaDaSemana = $"date_trunc('week', {HojeNoGrupo})::date";

    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Escolher_CriaRivalENotificaQuemFoiEscolhido()
    {
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync();

        var response = await EscolherAsync(ana, grupoId, bia.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await _host.CountAsync($"""
            SELECT count(*) FROM notificacoes
            WHERE usuario_id = '{bia.Id}' AND tipo = 'RivalEscolhido' AND ator_id = '{ana.Id}'
            """));

        var daAna = await GetRivaisAsync(ana, grupoId);
        var rival = Assert.Single(daAna.Rivais);
        Assert.True(rival.Escolhido);
        Assert.Equal(bia.Id, rival.RivalId);
        Assert.Equal(ResultadoRival.EmAndamento, rival.Situacao);
        Assert.False(rival.Nova);
        Assert.False(daAna.PodeEscolher);

        var daBia = Assert.Single((await GetRivaisAsync(bia, grupoId)).Rivais);
        Assert.False(daBia.Escolhido);
        Assert.True(daBia.Nova);
    }

    [Fact]
    public async Task Listar_MostraCandidatosNoModoJogo()
    {
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync();
        await _host.AddMembroAsync(grupoId, nome: "Caio");

        var rivais = await GetRivaisAsync(ana, grupoId);

        Assert.True(rivais.PodeEscolher);
        Assert.Equal([bia.Id], rivais.Candidatos);
        Assert.Empty(rivais.Rivais);
    }

    [Fact]
    public async Task Escolher_SegundaVezNaSemana_Retorna409()
    {
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync();
        (await EscolherAsync(ana, grupoId, bia.Id)).EnsureSuccessStatusCode();

        await ApiTestHost.AssertProblemAsync(
            await EscolherAsync(ana, grupoId, bia.Id),
            HttpStatusCode.Conflict,
            "RIVAL_JA_ESCOLHIDO");
    }

    [Fact]
    public async Task Escolher_ColegaNoModoApp_Retorna409()
    {
        var (ana, _, grupoId) = await CriarGrupoNoJogoAsync();
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");

        await ApiTestHost.AssertProblemAsync(
            await EscolherAsync(ana, grupoId, caio.Id),
            HttpStatusCode.Conflict,
            "RIVAL_INDISPONIVEL");
    }

    [Fact]
    public async Task Escolher_ASiMesmo_Retorna409()
    {
        var (ana, _, grupoId) = await CriarGrupoNoJogoAsync();

        await ApiTestHost.AssertProblemAsync(
            await EscolherAsync(ana, grupoId, ana.Id),
            HttpStatusCode.Conflict,
            "RIVAL_INDISPONIVEL");
    }

    [Fact]
    public async Task Escolher_NoModoApp_Retorna409()
    {
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync();
        await _host.ExecuteAsync($"UPDATE usuarios SET modo_inicio = 'App' WHERE id = '{ana.Id}'");

        await ApiTestHost.AssertProblemAsync(
            await EscolherAsync(ana, grupoId, bia.Id),
            HttpStatusCode.Conflict,
            "MODO_JOGO_NECESSARIO");
    }

    [Fact]
    public async Task Escolher_SemDesafioAtivo_Retorna409()
    {
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync(iniciarDesafio: false);

        await ApiTestHost.AssertProblemAsync(
            await EscolherAsync(ana, grupoId, bia.Id),
            HttpStatusCode.Conflict,
            "SEM_DESAFIO_ATIVO");
    }

    [Fact]
    public async Task Escolher_DepoisQueORivalTemAMeta_Retorna409()
    {
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync();
        // Ainda pendente: a meta provisória já impede a escolha.
        await PostAtividadeAsync(bia, grupoId);

        await ApiTestHost.AssertProblemAsync(
            await EscolherAsync(ana, grupoId, bia.Id),
            HttpStatusCode.Conflict,
            "META_JA_CUMPRIDA");
    }

    [Fact]
    public async Task Escolher_QuemNaoEMembro_Retorna404()
    {
        var (_, bia, grupoId) = await CriarGrupoNoJogoAsync();
        var cara = await _host.CreateUsuarioAsync();
        await JogoTestes.AtivarJogoAsync(_host, cara);

        await ApiTestHost.AssertProblemAsync(
            await EscolherAsync(cara, grupoId, bia.Id),
            HttpStatusCode.NotFound,
            "GRUPO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task QuemEscolheuVerificaPrimeiro_VenceERecebeEnergia()
    {
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync();
        (await EscolherAsync(ana, grupoId, bia.Id)).EnsureSuccessStatusCode();
        await PostAtividadeAsync(ana, grupoId);
        await PostAtividadeAsync(bia, grupoId);
        await VerificarAsync(ana, "now() - interval '1 hour'");
        await VerificarAsync(bia, "now()");

        var rival = Assert.Single((await GetRivaisAsync(ana, grupoId)).Rivais);
        Assert.Equal(ResultadoRival.Venceu, rival.Situacao);
        Assert.True(rival.Nova);
        Assert.Equal(1, (await GetRivaisAsync(ana, grupoId)).Vitorias);
        Assert.Equal(ResultadoRival.Energia, (await GetEnergiaAsync(ana)).Detalhamento.Rivais);
        // O Rival não ganha nem perde nada.
        Assert.Equal(0, (await GetEnergiaAsync(bia)).Detalhamento.Rivais);

        (await MarcarVistosAsync(ana, grupoId, rival.Id)).EnsureSuccessStatusCode();
        Assert.False(Assert.Single((await GetRivaisAsync(ana, grupoId)).Rivais).Nova);
    }

    [Fact]
    public async Task RivalVerificaPrimeiro_QuemEscolheuPerde()
    {
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync();
        (await EscolherAsync(ana, grupoId, bia.Id)).EnsureSuccessStatusCode();
        await PostAtividadeAsync(ana, grupoId);
        await PostAtividadeAsync(bia, grupoId);
        await VerificarAsync(bia, "now() - interval '1 hour'");
        await VerificarAsync(ana, "now()");

        Assert.Equal(ResultadoRival.Perdeu, Assert.Single((await GetRivaisAsync(ana, grupoId)).Rivais).Situacao);
        Assert.Equal(0, (await GetEnergiaAsync(ana)).Detalhamento.Rivais);
    }

    [Fact]
    public async Task MetaDiaria_ContaQuandoASomaDoDiaAlcancaOMinimo()
    {
        // Mínimo de 30 min: a Ana divide em 2 × 15 min, e o dia só conta quando a segunda é verificada.
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync();
        (await EscolherAsync(ana, grupoId, bia.Id)).EnsureSuccessStatusCode();
        await PostAtividadeAsync(ana, grupoId, 900);
        await PostAtividadeAsync(ana, grupoId, 900);
        await PostAtividadeAsync(bia, grupoId);
        await _host.ExecuteAsync($"""
            UPDATE atividades a
            SET situacao_verificacao = 'Verificada', verified_at = now() - o.n * interval '2 hours'
            FROM (SELECT id, row_number() OVER (ORDER BY created_at, id) AS n
                  FROM atividades WHERE usuario_id = '{ana.Id}') o
            WHERE a.id = o.id
            """);

        // A Ana completa o dia há 2 horas; a Bia, há 3.
        await VerificarAsync(bia, "now() - interval '3 hours'");
        Assert.Equal(ResultadoRival.Perdeu, Assert.Single((await GetRivaisAsync(ana, grupoId)).Rivais).Situacao);

        await VerificarAsync(bia, "now() - interval '1 hour'");
        Assert.Equal(ResultadoRival.Venceu, Assert.Single((await GetRivaisAsync(ana, grupoId)).Rivais).Situacao);
    }

    [Fact]
    public async Task MarcarVistos_RivalViuAProvocacao()
    {
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync();
        (await EscolherAsync(ana, grupoId, bia.Id)).EnsureSuccessStatusCode();
        var rival = Assert.Single((await GetRivaisAsync(bia, grupoId)).Rivais);

        (await MarcarVistosAsync(bia, grupoId, rival.Id)).EnsureSuccessStatusCode();

        Assert.False(Assert.Single((await GetRivaisAsync(bia, grupoId)).Rivais).Nova);
    }

    [Fact]
    public async Task MarcarVistos_ResultadoEmAndamentoNaoEMarcado()
    {
        var (ana, bia, grupoId) = await CriarGrupoNoJogoAsync();
        (await EscolherAsync(ana, grupoId, bia.Id)).EnsureSuccessStatusCode();
        var rival = Assert.Single((await GetRivaisAsync(ana, grupoId)).Rivais);

        (await MarcarVistosAsync(ana, grupoId, rival.Id)).EnsureSuccessStatusCode();

        Assert.Equal(0, await _host.CountAsync(
            $"SELECT count(*) FROM rivais WHERE id = '{rival.Id}' AND resultado_visto_em IS NOT NULL"));
    }

    [Fact]
    public async Task VerificacaoAutomatica_GravaVerifiedAt()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);

        // Sem revisores, o registro manual já fica Verificada.
        await PostAtividadeAsync(ana, grupoId);

        Assert.Equal(1, await _host.CountAsync($"""
            SELECT count(*) FROM atividades
            WHERE usuario_id = '{ana.Id}' AND situacao_verificacao = 'Verificada' AND verified_at IS NOT NULL
            """));
    }

    private async Task<(TestUsuario Ana, TestUsuario Bia, Guid GrupoId)> CriarGrupoNoJogoAsync(
        bool iniciarDesafio = true)
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await JogoTestes.AtivarJogoAsync(_host, ana);
        await JogoTestes.AtivarJogoAsync(_host, bia);
        if (iniciarDesafio)
        {
            await IniciarDesafioAsync(ana, grupoId);
        }

        return (ana, bia, grupoId);
    }

    private async Task IniciarDesafioAsync(TestUsuario ator, Guid grupoId)
    {
        var inicio = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        while (inicio.DayOfWeek != DayOfWeek.Monday)
        {
            inicio = inicio.AddDays(1);
        }

        var response = await ator.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/desafios",
            new Wrexercise.Features.Desafios.CriarDesafioRequest("Competição", null, inicio, inicio.AddDays(27), 1800, 1),
            AppJsonContext.Default.CriarDesafioRequest,
            Token);
        response.EnsureSuccessStatusCode();
        var desafio = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.DesafioResponse, Token);

        await _host.ExecuteAsync($"""
            UPDATE desafios
            SET data_inicio = {SegundaDaSemana}, data_fim = {SegundaDaSemana} + 27
            WHERE id = '{desafio!.Id}'
            """);
        await _host.ExecuteAsync($"""
            UPDATE membros_grupo SET joined_at = now() - interval '60 days'
            WHERE grupo_id = '{grupoId}'
            """);
    }

    private async Task PostAtividadeAsync(TestUsuario usuario, Guid grupoId, int duracao = 3600)
    {
        await ApiTestHost.AvaliarFilaAsync(usuario, grupoId);
        var response = await usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                TipoAtividade.Corrida,
                duracao,
                DateTimeOffset.UtcNow.AddMinutes(-30),
                "Treino",
                TipoComprovacao.RegistroManual),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        response.EnsureSuccessStatusCode();
    }

    private Task VerificarAsync(TestUsuario usuario, string verifiedAt) =>
        _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'Verificada', verified_at = {verifiedAt}
            WHERE usuario_id = '{usuario.Id}'
            """);

    private static Task<HttpResponseMessage> EscolherAsync(TestUsuario usuario, Guid grupoId, Guid rivalId) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/rivais",
            new EscolherRivalRequest(rivalId),
            AppJsonContext.Default.EscolherRivalRequest,
            Token);

    private static Task<HttpResponseMessage> MarcarVistosAsync(TestUsuario usuario, Guid grupoId, Guid id) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/rivais/vistos",
            new MarcarRivaisVistosRequest([id]),
            AppJsonContext.Default.MarcarRivaisVistosRequest,
            Token);

    private static async Task<RivaisResponse> GetRivaisAsync(TestUsuario usuario, Guid grupoId) =>
        (await usuario.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/rivais",
            AppJsonContext.Default.RivaisResponse,
            Token))!;

    private static async Task<EnergiaResponse> GetEnergiaAsync(TestUsuario usuario) =>
        (await usuario.Client.GetFromJsonAsync("/api/jogo/energia", AppJsonContext.Default.EnergiaResponse, Token))!;
}
