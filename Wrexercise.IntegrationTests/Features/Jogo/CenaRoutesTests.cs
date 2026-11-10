using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Jogo;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Jogo;

public sealed class CenaRoutesTests : IAsyncLifetime
{
    private const string HojeNoGrupo = "(now() AT TIME ZONE 'America/Sao_Paulo')::date";
    private const string SegundaDaSemana = $"date_trunc('week', {HojeNoGrupo})::date";

    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Cena_Retorna404ParaQuemNaoEMembro()
    {
        var ana = await _host.CreateUsuarioAsync();
        var cara = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);

        await ApiTestHost.AssertProblemAsync(
            await cara.Client.GetAsync($"/api/grupos/{grupoId}/cena", Token),
            HttpStatusCode.NotFound,
            "GRUPO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Cena_Retorna401ParaAnonimo()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);

        var response = await _host.CreateAnonimo().GetAsync($"/api/grupos/{grupoId}/cena", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SemDesafio_TodosFicamParados()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await _host.AddMembroAsync(grupoId, nome: "Bia");

        var cena = await GetCenaAsync(ana, grupoId);

        Assert.Equal(2, cena.Membros.Count);
        Assert.All(cena.Membros, m => Assert.Equal(PoseAvatar.Parado, m.Pose));
    }

    [Fact]
    public async Task AtividadeAguardandoFeedback_DeixaOMembroTreinando()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await _host.AddMembroAsync(grupoId, nome: "Bia");
        await PostAtividadeAsync(ana, grupoId);
        await DefinirSituacaoAsync(ana, "AguardandoFeedback");

        var cena = await GetCenaAsync(ana, grupoId);

        Assert.Equal(PoseAvatar.Treinando, PoseDe(cena, "Ana"));
        Assert.Equal(PoseAvatar.Parado, PoseDe(cena, "Bia"));
    }

    [Fact]
    public async Task MetaCumprida_DeixaOMembroComemorando()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await IniciarDesafioAsync(ana, grupoId, sessoesPorSemana: 1);
        await PostAtividadeAsync(ana, grupoId);
        await DefinirSituacaoAsync(ana, "Verificada");

        var cena = await GetCenaAsync(ana, grupoId);

        Assert.Equal(PoseAvatar.Comemorando, PoseDe(cena, "Ana"));
    }

    [Fact]
    public async Task AtividadePendenteComMetaCumprida_DeixaOMembroTreinando()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await IniciarDesafioAsync(ana, grupoId, sessoesPorSemana: 1);
        await PostAtividadeAsync(ana, grupoId);
        await PostAtividadeAsync(ana, grupoId);
        // Uma atividade verificada cumpre a meta; a outra segue em revisão.
        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'Verificada', verified_at = now()
            WHERE usuario_id = '{ana.Id}'
            """);
        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'EmRevisao'
            WHERE id = (SELECT id FROM atividades WHERE usuario_id = '{ana.Id}' ORDER BY id DESC LIMIT 1)
            """);

        var cena = await GetCenaAsync(ana, grupoId);

        Assert.Equal(PoseAvatar.Treinando, PoseDe(cena, "Ana"));
    }

    [Fact]
    public async Task MembroRemovido_NaoApareceNaCena()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await _host.ExecuteAsync($"""
            UPDATE membros_grupo SET removed_at = now()
            WHERE grupo_id = '{grupoId}' AND usuario_id = '{bia.Id}'
            """);

        var cena = await GetCenaAsync(ana, grupoId);

        Assert.Equal(["Ana"], cena.Membros.Select(m => m.NomeExibicao));
    }

    [Fact]
    public async Task AtividadePendenteEmOutroGrupo_NaoConta()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoA = await _host.CreateGrupoAsync(ana);
        var grupoB = await _host.CreateGrupoAsync(ana);
        await PostAtividadeAsync(ana, grupoB);
        await DefinirSituacaoAsync(ana, "AguardandoFeedback");

        var cena = await GetCenaAsync(ana, grupoA);

        Assert.Equal(PoseAvatar.Parado, PoseDe(cena, "Ana"));
    }

    private static string PoseDe(CenaResponse cena, string nome) =>
        Assert.Single(cena.Membros, m => m.NomeExibicao == nome).Pose;

    private async Task DefinirSituacaoAsync(TestUsuario usuario, string situacao) =>
        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = '{situacao}', verified_at = now()
            WHERE usuario_id = '{usuario.Id}'
            """);

    private async Task IniciarDesafioAsync(TestUsuario ator, Guid grupoId, int sessoesPorSemana)
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
                sessoesPorSemana),
            AppJsonContext.Default.CriarDesafioRequest,
            Token);
        response.EnsureSuccessStatusCode();
        var desafio = await response.Content.ReadFromJsonAsync(
            AppJsonContext.Default.DesafioResponse,
            Token);

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

    private async Task PostAtividadeAsync(TestUsuario usuario, Guid grupoId)
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

    private static async Task<CenaResponse> GetCenaAsync(TestUsuario leitor, Guid grupoId) =>
        (await leitor.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/cena",
            AppJsonContext.Default.CenaResponse,
            Token))!;
}
