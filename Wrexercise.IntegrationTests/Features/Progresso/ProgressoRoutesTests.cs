using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Desafios;
using Wrexercise.Features.Progresso;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Progresso;

public sealed class ProgressoRoutesTests : IAsyncLifetime
{
    private const string HojeNoGrupo = "(now() AT TIME ZONE 'America/Sao_Paulo')::date";
    private const string SegundaDaSemana = $"date_trunc('week', {HojeNoGrupo})::date";

    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Classificacao_OrdenaPorSemanasCumpridas()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var desafioId = await CreateDesafioAsync(ana, grupoId);
        await IniciarAsync(desafioId);

        for (var i = 0; i < 4; i++)
        {
            await PostAtividadeAsync(ana, grupoId, 3600);
        }

        await DistribuirNaSemanaAsync(grupoId);
        await _host.AddMembroAsync(grupoId, nome: "Bia");

        var progresso = await GetProgressoAsync(ana, desafioId);
        var anaProgresso = Assert.Single(progresso.Participantes, p => p.NomeExibicao == "Ana");
        Assert.Equal(4, anaProgresso.SessoesVerificadas);
        Assert.True(anaProgresso.MetaCumprida);
        Assert.Equal(4, progresso.SeusDias.Count);

        var classificacao = await GetClassificacaoAsync(ana, desafioId);

        Assert.Equal(2, classificacao.Participantes.Count);
        Assert.Equal("Ana", classificacao.Participantes[0].NomeExibicao);
        Assert.Equal(1, classificacao.Participantes[0].Posicao);
        Assert.Equal(1, classificacao.Participantes[0].SemanasCumpridas);
        Assert.Equal(4, classificacao.Participantes[0].SessoesVerificadas);
        Assert.Equal("Bia", classificacao.Participantes[1].NomeExibicao);
        Assert.Equal(0, classificacao.Participantes[1].SemanasCumpridas);
    }

    [Fact]
    public async Task MetaDiaria_AtividadesDoMesmoDiaSomamUmaSessao()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var desafioId = await CreateDesafioAsync(ana, grupoId);
        await IniciarAsync(desafioId);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3).AddMinutes(-30));

        await PostAtividadeAsync(ana, grupoId, 1800);

        var progresso = await GetProgressoAsync(ana, desafioId);
        Assert.Equal(0, Assert.Single(progresso.Participantes).SessoesVerificadas);
        Assert.Empty(progresso.SeusDias);
        Assert.Equal([(hoje, 1800)], progresso.SeusSegundosPorDia.Select(d => (d.Data, d.Segundos)));

        await PostAtividadeAsync(ana, grupoId, 1800);

        progresso = await GetProgressoAsync(ana, desafioId);
        Assert.Equal(1, Assert.Single(progresso.Participantes).SessoesVerificadas);
        Assert.Equal([new DiaQueContaResponse(hoje, null)], progresso.SeusDias);

        // Um dia longo continua sendo uma sessão só.
        await PostAtividadeAsync(ana, grupoId, 3600);

        progresso = await GetProgressoAsync(ana, desafioId);
        Assert.Equal(1, Assert.Single(progresso.Participantes).SessoesVerificadas);
        var dia = Assert.Single(progresso.SeusSegundosPorDia);
        Assert.Equal((hoje, 7200), (dia.Data, dia.Segundos));
        Assert.Equal([new SegundosPorTipoResponse(TipoAtividade.Corrida, 7200)], dia.SegundosPorTipo);
        Assert.Equal(1, (await GetClassificacaoAsync(ana, desafioId)).Participantes[0].SessoesVerificadas);
    }

    [Fact]
    public async Task MetaPorTipo_ContaODiaComMenosMinutosDosTiposDela()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var desafio = await CreateDesafioAsync(ana, grupoId, MetasCorridaECardio);
        await IniciarAsync(desafio.Id);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3).AddMinutes(-30));

        // 20 min de Musculacao não cumprem nenhuma condição.
        await PostAtividadeAsync(ana, grupoId, 1200, TipoAtividade.Musculacao);
        var progresso = await GetProgressoAsync(ana, desafio.Id);
        Assert.Empty(progresso.SeusDias);
        Assert.Equal(0, Assert.Single(progresso.Participantes).SessoesVerificadas);

        // 20 min de Corrida cumprem a primeira meta por tipo, mesmo com 40 min no total.
        await PostAtividadeAsync(ana, grupoId, 1200, TipoAtividade.Corrida);
        progresso = await GetProgressoAsync(ana, desafio.Id);
        Assert.Equal([new DiaQueContaResponse(hoje, desafio.MetasPorTipo[0].Id)], progresso.SeusDias);
        Assert.Equal(1, Assert.Single(progresso.Participantes).SessoesVerificadas);
        Assert.Equal(1, (await GetClassificacaoAsync(ana, desafio.Id)).Participantes[0].SessoesVerificadas);
        var dia = Assert.Single(progresso.SeusSegundosPorDia);
        Assert.Equal(2400, dia.Segundos);
        Assert.Equal(
            [
                new SegundosPorTipoResponse(TipoAtividade.Corrida, 1200),
                new SegundosPorTipoResponse(TipoAtividade.Musculacao, 1200),
            ],
            dia.SegundosPorTipo);

        // Com a duração mínima do desafio alcançada, o dia conta pela meta geral.
        await PostAtividadeAsync(ana, grupoId, 1200, TipoAtividade.Musculacao);
        progresso = await GetProgressoAsync(ana, desafio.Id);
        Assert.Equal([new DiaQueContaResponse(hoje, null)], progresso.SeusDias);
        Assert.Equal(1, Assert.Single(progresso.Participantes).SessoesVerificadas);
    }

    [Fact]
    public async Task MetaPorTipo_SomaOsTiposDaMesmaMeta()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var desafio = await CreateDesafioAsync(ana, grupoId, MetasCorridaECardio);
        await IniciarAsync(desafio.Id);

        await PostAtividadeAsync(ana, grupoId, 900, TipoAtividade.Caminhada);
        Assert.Empty((await GetProgressoAsync(ana, desafio.Id)).SeusDias);

        await PostAtividadeAsync(ana, grupoId, 900, TipoAtividade.Ciclismo);
        var progresso = await GetProgressoAsync(ana, desafio.Id);
        Assert.Equal(desafio.MetasPorTipo[1].Id, Assert.Single(progresso.SeusDias).MetaPorTipoId);
    }

    [Fact]
    public async Task MetaPorTipo_PendenteContaComoProvisorio()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var desafio = await CreateDesafioAsync(ana, grupoId, MetasCorridaECardio);
        await IniciarAsync(desafio.Id);
        await _host.AddMembroAsync(grupoId, nome: "Bia");

        await PostAtividadeAsync(ana, grupoId, 1200, TipoAtividade.Corrida);

        var pendente = Assert.Single((await GetProgressoAsync(ana, desafio.Id)).Participantes, p => p.NomeExibicao == "Ana");
        Assert.Equal(0, pendente.SessoesVerificadas);
        Assert.Equal(1, pendente.SessoesProvisorias);

        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'Verificada', verified_at = now()
            WHERE grupo_id = '{grupoId}'
            """);

        var verificada = Assert.Single((await GetProgressoAsync(ana, desafio.Id)).Participantes, p => p.NomeExibicao == "Ana");
        Assert.Equal(1, verificada.SessoesVerificadas);
        Assert.Equal(1, verificada.SessoesProvisorias);
    }

    [Fact]
    public async Task MetaDiaria_DiaComUmaPendente_ContaComoProvisorio()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var desafioId = await CreateDesafioAsync(ana, grupoId);
        await IniciarAsync(desafioId);
        await _host.AddMembroAsync(grupoId, nome: "Bia");

        await PostAtividadeAsync(ana, grupoId, 1800);
        await PostAtividadeAsync(ana, grupoId, 1800);
        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'Verificada', verified_at = now()
            WHERE id = (SELECT id FROM atividades WHERE grupo_id = '{grupoId}' ORDER BY created_at LIMIT 1)
            """);

        var ana1 = Assert.Single((await GetProgressoAsync(ana, desafioId)).Participantes, p => p.NomeExibicao == "Ana");
        Assert.Equal(0, ana1.SessoesVerificadas);
        Assert.Equal(1, ana1.SessoesProvisorias);

        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'Verificada', verified_at = now()
            WHERE grupo_id = '{grupoId}'
            """);

        var ana2 = Assert.Single((await GetProgressoAsync(ana, desafioId)).Participantes, p => p.NomeExibicao == "Ana");
        Assert.Equal(1, ana2.SessoesVerificadas);
        Assert.Equal(1, ana2.SessoesProvisorias);
    }

    [Fact]
    public async Task Historico_ListaTodasAsSemanasDoUsuario()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var desafioId = await CreateDesafioAsync(ana, grupoId);
        await IniciarAsync(desafioId);

        await PostAtividadeAsync(ana, grupoId, 3600);

        var historico = await GetHistoricoAsync(ana, desafioId);

        Assert.Equal(4, historico.TotalSemanas);
        Assert.Equal(4, historico.Semanas.Count);
        Assert.Equal(1, historico.Semanas[0].SessoesVerificadas);
        Assert.True(historico.Semanas[0].MetaCumprida is false);
    }

    [Fact]
    public async Task ResumoGrupo_IncluiDesafioAtivoProgressoEClassificacao()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var desafioId = await CreateDesafioAsync(ana, grupoId);
        await IniciarAsync(desafioId);
        await PostAtividadeAsync(ana, grupoId, 3600);

        var resumo = await GetResumoAsync(ana, grupoId);

        Assert.Equal(grupoId, resumo.GrupoId);
        Assert.NotNull(resumo.Desafio);
        Assert.Equal(desafioId, resumo.Desafio!.Id);
        Assert.NotNull(resumo.MeuProgresso);
        Assert.Equal(1, resumo.MeuProgresso!.SessoesVerificadas);
        Assert.InRange(resumo.MeuProgresso.DiasRestantes, 1, 7);
        Assert.Single(resumo.Classificacao);
        Assert.Equal("Ana", resumo.Classificacao[0].NomeExibicao);
        Assert.Equal(ana.Id, resumo.Classificacao[0].UsuarioId);
    }

    [Fact]
    public async Task Classificacao_Retorna404ParaQuemNaoEMembro()
    {
        var ana = await _host.CreateUsuarioAsync();
        var cara = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var desafioId = await CreateDesafioAsync(ana, grupoId);

        await ApiTestHost.AssertProblemAsync(
            await cara.Client.GetAsync($"/api/desafios/{desafioId}/classificacao", Token),
            HttpStatusCode.NotFound,
            "DESAFIO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task AdesaoTardia_ContaSoAPartirDaPrimeiraSemanaInteira()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var desafioId = await CreateDesafioAsync(ana, grupoId);
        await IniciarAsync(desafioId);

        // Bia entrou na terça: a semana atual não conta para ela, e a seguinte conta.
        await _host.ExecuteAsync($"""
            UPDATE membros_grupo
            SET joined_at = (({SegundaDaSemana} + 1)::timestamp AT TIME ZONE 'America/Sao_Paulo')
            WHERE usuario_id = '{bia.Id}'
            """);
        await PostAtividadeAsync(bia, grupoId, 3600);

        var progresso = await bia.Client.GetFromJsonAsync(
            $"/api/desafios/{desafioId}/progresso",
            AppJsonContext.Default.ProgressoResponse,
            Token);
        var biaProgresso = Assert.Single(progresso!.Participantes, p => p.NomeExibicao == "Bia");
        Assert.False(biaProgresso.ContaNestaSemana);
        Assert.Equal(0, biaProgresso.SessoesProvisorias);

        var historico = await GetHistoricoAsync(bia, desafioId);
        Assert.Equal([false, true, true, true], historico.Semanas.Select(s => s.Participou));

        var resumo = await GetResumoAsync(bia, grupoId);
        Assert.False(resumo.MeuProgresso!.ContaNestaSemana);
    }

    /// <summary>20 min de Corrida, ou 30 min de Caminhada e Ciclismo somados, contra 60 min de qualquer treino.</summary>
    private static readonly MetaPorTipoRequest[] MetasCorridaECardio =
    [
        new(null, [TipoAtividade.Corrida], 1200),
        new("Cardio", [TipoAtividade.Caminhada, TipoAtividade.Ciclismo], 1800),
    ];

    private static CriarAtividadeRequest Atividade(int duracao, string tipo = TipoAtividade.Corrida) => new(
        tipo,
        duracao,
        DateTimeOffset.UtcNow.AddMinutes(-30),
        "Treino",
        TipoComprovacao.RegistroManual);

    private async Task PostAtividadeAsync(
        TestUsuario usuario,
        Guid grupoId,
        int duracao,
        string tipo = TipoAtividade.Corrida)
    {
        var response = await usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            Atividade(duracao, tipo),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateDesafioAsync(TestUsuario ator, Guid grupoId) =>
        (await CreateDesafioAsync(ator, grupoId, null)).Id;

    private static async Task<DesafioResponse> CreateDesafioAsync(
        TestUsuario ator,
        Guid grupoId,
        IReadOnlyList<MetaPorTipoRequest>? metasPorTipo)
    {
        var inicio = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        while (inicio.DayOfWeek != DayOfWeek.Monday)
        {
            inicio = inicio.AddDays(1);
        }

        var response = await ator.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/desafios",
            new Wrexercise.Features.Desafios.CriarDesafioRequest(
                "Competição",
                null,
                inicio,
                inicio.AddDays(27),
                3600,
                4,
                metasPorTipo),
            AppJsonContext.Default.CriarDesafioRequest,
            Token);
        response.EnsureSuccessStatusCode();
        var desafio = await response.Content.ReadFromJsonAsync(
            AppJsonContext.Default.DesafioResponse,
            Token);
        return desafio!;
    }

    /// <summary>Uma atividade por dia da semana atual, a partir de segunda, ao meio-dia no horário do grupo.</summary>
    private async Task DistribuirNaSemanaAsync(Guid grupoId) =>
        await _host.ExecuteAsync($"""
            UPDATE atividades a
            SET started_at = ({SegundaDaSemana} + (o.n - 1)::int + time '12:00') AT TIME ZONE 'America/Sao_Paulo'
            FROM (SELECT id, row_number() OVER (ORDER BY created_at, id) AS n
                  FROM atividades WHERE grupo_id = '{grupoId}') o
            WHERE a.id = o.id
            """);

    private static async Task<ProgressoResponse> GetProgressoAsync(TestUsuario leitor, Guid desafioId) =>
        (await leitor.Client.GetFromJsonAsync(
            $"/api/desafios/{desafioId}/progresso",
            AppJsonContext.Default.ProgressoResponse,
            Token))!;

    private async Task IniciarAsync(Guid desafioId)
    {
        await _host.ExecuteAsync($"""
            UPDATE desafios
            SET data_inicio = {SegundaDaSemana}, data_fim = {SegundaDaSemana} + 27
            WHERE id = '{desafioId}'
            """);
        await _host.ExecuteAsync($"""
            UPDATE membros_grupo
            SET joined_at = now() - interval '60 days'
            WHERE grupo_id = (SELECT grupo_id FROM desafios WHERE id = '{desafioId}')
            """);
    }

    private static async Task<ClassificacaoResponse> GetClassificacaoAsync(TestUsuario leitor, Guid desafioId) =>
        (await leitor.Client.GetFromJsonAsync(
            $"/api/desafios/{desafioId}/classificacao",
            AppJsonContext.Default.ClassificacaoResponse,
            Token))!;

    private static async Task<HistoricoSemanalResponse> GetHistoricoAsync(TestUsuario leitor, Guid desafioId) =>
        (await leitor.Client.GetFromJsonAsync(
            $"/api/desafios/{desafioId}/historico",
            AppJsonContext.Default.HistoricoSemanalResponse,
            Token))!;

    private static async Task<ResumoGrupoResponse> GetResumoAsync(TestUsuario leitor, Guid grupoId) =>
        (await leitor.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/resumo",
            AppJsonContext.Default.ResumoGrupoResponse,
            Token))!;
}
