using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
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

        await _host.AddMembroAsync(grupoId, nome: "Bia");

        var progresso = await ana.Client.GetFromJsonAsync(
            $"/api/desafios/{desafioId}/progresso",
            AppJsonContext.Default.ProgressoResponse,
            Token);
        var anaProgresso = Assert.Single(progresso!.Participantes, p => p.NomeExibicao == "Ana");
        Assert.Equal(4, anaProgresso.SessoesVerificadas);
        Assert.True(anaProgresso.MetaCumprida);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3).AddMinutes(-30));
        Assert.Equal([hoje], progresso.SeusDias);

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

    private static CriarAtividadeRequest Atividade(int duracao) => new(
        TipoAtividade.Corrida,
        duracao,
        DateTimeOffset.UtcNow.AddMinutes(-30),
        "Treino",
        TipoComprovacao.RegistroManual);

    private async Task PostAtividadeAsync(TestUsuario usuario, Guid grupoId, int duracao)
    {
        var response = await usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            Atividade(duracao),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateDesafioAsync(TestUsuario ator, Guid grupoId)
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
                4),
            AppJsonContext.Default.CriarDesafioRequest,
            Token);
        response.EnsureSuccessStatusCode();
        var desafio = await response.Content.ReadFromJsonAsync(
            AppJsonContext.Default.DesafioResponse,
            Token);
        return desafio!.Id;
    }

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
