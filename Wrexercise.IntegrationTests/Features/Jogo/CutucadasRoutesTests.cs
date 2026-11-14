using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Jogo;
using Wrexercise.Features.Reacoes;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Jogo;

public sealed class CutucadasRoutesTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Cutucar_QuemDorme_NotificaEApareceNaCena()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");
        await AdormecerAsync(grupoId, bia);

        var response = await CutucarAsync(ana, grupoId, bia.Id, "Sofa");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await _host.CountAsync($"""
            SELECT count(*) FROM notificacoes
            WHERE usuario_id = '{bia.Id}' AND tipo = 'Cutucada' AND ator_id = '{ana.Id}' AND cutucada_id IS NOT NULL
            """));

        var cena = await (await ana.Client.GetAsync($"/api/grupos/{grupoId}/cena", Token))
            .Content.ReadFromJsonAsync(AppJsonContext.Default.CenaResponse, Token);
        var membro = cena!.Membros.Single(m => m.UsuarioId == bia.Id);
        Assert.Equal(new CutucadasMembroResponse(1, "Sofa"), membro.Cutucadas);
        Assert.NotNull(membro.CutucadoPorMimEm);

        var notificacoes = await (await bia.Client.GetAsync("/api/notificacoes", Token))
            .Content.ReadFromJsonAsync(AppJsonContext.Default.NotificacoesResponse, Token);
        Assert.Equal("Sofa", notificacoes!.Notificacoes.Single(n => n.Tipo == "Cutucada").Cutucada);
    }

    [Fact]
    public async Task Cutucar_QuemEstaAcordado_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");

        await ApiTestHost.AssertProblemAsync(
            await CutucarAsync(ana, grupoId, bia.Id, "Sofa"),
            HttpStatusCode.Conflict,
            "MEMBRO_ACORDADO");
    }

    [Fact]
    public async Task Cutucar_DuasVezesEm24Horas_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");
        var caio = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Caio");
        await AdormecerAsync(grupoId, bia);

        (await CutucarAsync(ana, grupoId, bia.Id, "Sofa")).EnsureSuccessStatusCode();

        await ApiTestHost.AssertProblemAsync(
            await CutucarAsync(ana, grupoId, bia.Id, "Frango"),
            HttpStatusCode.Conflict,
            "CUTUCADA_RECENTE");

        // Outro colega ainda pode cutucar, e passadas 24 horas Ana também.
        (await CutucarAsync(caio, grupoId, bia.Id, "Frango")).EnsureSuccessStatusCode();
        await _host.ExecuteAsync(
            $"UPDATE cutucadas SET created_at = now() - interval '25 hours' WHERE autor_id = '{ana.Id}'");
        (await CutucarAsync(ana, grupoId, bia.Id, "Meta")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Cutucar_DadosInvalidos_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");
        await AdormecerAsync(grupoId, bia);
        await AdormecerAsync(grupoId, ana);

        await ApiTestHost.AssertProblemAsync(
            await CutucarAsync(ana, grupoId, ana.Id, "Sofa"), HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
        await ApiTestHost.AssertProblemAsync(
            await CutucarAsync(ana, grupoId, bia.Id, "Xingamento"), HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
        await ApiTestHost.AssertProblemAsync(
            await CutucarAsync(ana, grupoId, Guid.NewGuid(), "Sofa"), HttpStatusCode.NotFound, "MEMBRO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Cutucar_QuemNaoEMembro_Recebe404()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");
        await AdormecerAsync(grupoId, bia);
        var cara = await _host.CreateUsuarioAsync("Cara");

        await ApiTestHost.AssertProblemAsync(
            await CutucarAsync(cara, grupoId, bia.Id, "Sofa"), HttpStatusCode.NotFound, "GRUPO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Despertador_QuemCutucouGanha5QuandoOColegaVolta()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");
        await AdormecerAsync(grupoId, bia);

        (await CutucarAsync(ana, grupoId, bia.Id, "Despertador")).EnsureSuccessStatusCode();

        var criada = await bia.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                TipoAtividade.Corrida, 1800, DateTimeOffset.UtcNow.AddMinutes(-10), null, TipoComprovacao.RegistroManual),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        // Pendente ainda não rende.
        Assert.Equal(0, (await GetEnergiaAsync(ana)).Detalhamento.Despertador);

        (await ana.Client.PutAsJsonAsync(
            $"/api/atividades/{atividade!.Id}/reacao",
            new ReacaoRequest(TipoReacao.Kudos),
            AppJsonContext.Default.ReacaoRequest,
            Token)).EnsureSuccessStatusCode();

        var energia = await GetEnergiaAsync(ana);
        Assert.Equal(5, energia.Detalhamento.Despertador);
        Assert.Equal(5, energia.Saldo);
    }

    private Task AdormecerAsync(Guid grupoId, TestUsuario usuario) =>
        _host.ExecuteAsync($"""
            UPDATE membros_grupo SET joined_at = now() - interval '3 days'
            WHERE grupo_id = '{grupoId}' AND usuario_id = '{usuario.Id}'
            """);

    private static Task<HttpResponseMessage> CutucarAsync(TestUsuario usuario, Guid grupoId, Guid destinatarioId, string tipo) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/cutucadas",
            new CutucarRequest(destinatarioId, tipo),
            AppJsonContext.Default.CutucarRequest,
            Token);

    private static async Task<EnergiaResponse> GetEnergiaAsync(TestUsuario usuario)
    {
        var response = await usuario.Client.GetAsync("/api/jogo/energia", Token);
        return (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.EnergiaResponse, Token))!;
    }
}
