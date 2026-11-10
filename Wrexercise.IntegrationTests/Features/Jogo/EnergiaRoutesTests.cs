using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Jogo;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Jogo;

public sealed class EnergiaRoutesTests : IAsyncLifetime
{
    private const string HojeNoGrupo = "(now() AT TIME ZONE 'America/Sao_Paulo')::date";
    private const string SegundaDaSemana = $"date_trunc('week', {HojeNoGrupo})::date";

    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Energia_Retorna401ParaAnonimo()
    {
        var response = await _host.CreateAnonimo().GetAsync("/api/jogo/energia", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SemAtividades_SaldoZero()
    {
        var ana = await _host.CreateUsuarioAsync();

        var energia = await GetEnergiaAsync(ana);

        Assert.Equal(0, energia.Saldo);
    }

    [Fact]
    public async Task MesmoTreinoEmDoisGrupos_SomaABaseSemDobrarOsBonus()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoA = await _host.CreateGrupoAsync(ana);
        var grupoB = await _host.CreateGrupoAsync(ana);
        await PostAtividadeAsync(ana, grupoA);
        await PostAtividadeAsync(ana, grupoA);
        await PostAtividadeAsync(ana, grupoB);

        // Uma terça à tarde, para não depender do dia nem do horário da execução.
        await _host.ExecuteAsync($"""
            UPDATE atividades
            SET started_at = timestamp '2026-11-03 15:00' AT TIME ZONE 'America/Sao_Paulo',
                situacao_verificacao = 'Verificada', verified_at = now()
            WHERE usuario_id = '{ana.Id}'
            """);
        // A primeira registrada é rejeitada: a seguinte passa a ser a primeira da semana.
        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'Rejeitada'
            WHERE id = (SELECT id FROM atividades WHERE usuario_id = '{ana.Id}' ORDER BY created_at, id LIMIT 1)
            """);

        var energia = await GetEnergiaAsync(ana);

        Assert.Equal(new EnergiaDetalhamentoResponse(20, 6, 0, 0, 0, 0, 0), energia.Detalhamento);
        Assert.Equal(26, energia.Saldo);
    }

    [Fact]
    public async Task MetaCumprida_RendeEnergiaDaSemana()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        await IniciarDesafioAsync(ana, grupoId);
        await PostAtividadeAsync(ana, grupoId);
        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'Verificada', verified_at = now()
            WHERE usuario_id = '{ana.Id}'
            """);

        var energia = await GetEnergiaAsync(ana);

        Assert.Equal(CalculoEnergia.PorMeta, energia.Detalhamento.Metas);
    }

    [Fact]
    public async Task SoContaAsAtividadesDeQuemConsulta()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId);
        await PostAtividadeAsync(bia, grupoId);
        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'Verificada', verified_at = now()
            WHERE usuario_id = '{bia.Id}'
            """);

        Assert.Equal(0, (await GetEnergiaAsync(ana)).Saldo);
        Assert.Equal(10, (await GetEnergiaAsync(bia)).Detalhamento.Atividades);
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
            new Wrexercise.Features.Desafios.CriarDesafioRequest("Competição", null, inicio, inicio.AddDays(27), 3600, 1),
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

    private static async Task PostAtividadeAsync(TestUsuario usuario, Guid grupoId)
    {
        var response = await usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                TipoAtividade.Corrida,
                3600,
                DateTimeOffset.UtcNow.AddMinutes(-30),
                "Treino",
                TipoComprovacao.RegistroManual),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<EnergiaResponse> GetEnergiaAsync(TestUsuario leitor) =>
        (await leitor.Client.GetFromJsonAsync("/api/jogo/energia", AppJsonContext.Default.EnergiaResponse, Token))!;
}
