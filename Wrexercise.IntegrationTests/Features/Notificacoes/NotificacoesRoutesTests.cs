using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Notificacoes;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Features.Reacoes;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Notificacoes;

public sealed class NotificacoesRoutesTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task NovaAtividade_AvisaOsRevisores_NaoOAutor()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");

        var atividadeId = await CriarAtividadeAsync(ana, grupoId);

        var notificacao = Assert.Single(await ListarAsync(bia));
        Assert.Equal("NovaAtividade", notificacao.Tipo);
        Assert.Equal(atividadeId, notificacao.AtividadeId);
        Assert.Equal(grupoId, notificacao.GrupoId);
        Assert.Equal("Ana", notificacao.AtorNome);
        Assert.False(notificacao.Lida);
        Assert.Empty(await ListarAsync(ana));
    }

    [Fact]
    public async Task Reacoes_SomamEnquantoNaoLidas_ETrocarNaoConta()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");
        var dani = await _host.AddMembroAsync(grupoId, nome: "Dani");
        var atividadeId = await CriarAtividadeAsync(ana, grupoId);

        await ReagirAsync(bia, atividadeId, TipoReacao.Kudos);
        await ReagirAsync(bia, atividadeId, TipoReacao.Kudos);
        await ReagirAsync(caio, atividadeId, TipoReacao.Kudos);

        var reacao = Assert.Single(await ListarAsync(ana), n => n.Tipo == "Reacao");
        Assert.Equal(2, reacao.Total);
        Assert.Equal("Caio", reacao.AtorNome);

        Assert.Equal(HttpStatusCode.NoContent, (await MarcarAsync(ana, reacao.Id)).StatusCode);
        await ReagirAsync(dani, atividadeId, TipoReacao.Kudos);

        var reacoes = (await ListarAsync(ana)).Where(n => n.Tipo == "Reacao").ToList();
        Assert.Equal(2, reacoes.Count);
        Assert.Equal(1, Assert.Single(reacoes, n => !n.Lida).Total);
    }

    [Fact]
    public async Task Suspeito_AvisaOAutorEOsAdministradores()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var admin = await _host.AddMembroAsync(grupoId, PapelMembro.Administrador, "Admin");
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");
        var atividadeId = await CriarAtividadeAsync(bia, grupoId);

        await ReagirAsync(caio, atividadeId, TipoReacao.Suspeito);

        var situacao = Assert.Single(await ListarAsync(bia), n => n.Tipo == "SituacaoAlterada");
        Assert.Equal("EmRevisao", situacao.Situacao);
        Assert.Contains(await ListarAsync(ana), n => n.Tipo == "Disputa" && n.AtorNome == "Bia");
        Assert.Contains(await ListarAsync(admin), n => n.Tipo == "Disputa");
        Assert.DoesNotContain(await ListarAsync(caio), n => n.Tipo == "Disputa");
        Assert.DoesNotContain(await ListarAsync(bia), n => n.Tipo == "Disputa");
    }

    [Fact]
    public async Task DecisaoAdmin_AvisaOAutor()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");
        var atividadeId = await CriarAtividadeAsync(bia, grupoId);
        await ReagirAsync(caio, atividadeId, TipoReacao.Suspeito);

        var response = await ana.Client.PostAsJsonAsync(
            $"/api/atividades/{atividadeId}/revisao",
            new RevisaoRequest(Wrexercise.Features.Reacoes.SituacaoVerificacao.Rejeitada, "Sem comprovação."),
            AppJsonContext.Default.RevisaoRequest,
            Token);
        response.EnsureSuccessStatusCode();

        Assert.Contains(await ListarAsync(bia), n => n.Tipo == "SituacaoAlterada" && n.Situacao == "Rejeitada");
    }

    [Fact]
    public async Task Encerramento_AvisaQueFoiVerificada()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await _host.AddMembroAsync(grupoId, nome: "Bia");
        var atividadeId = await CriarAtividadeAsync(ana, grupoId);
        await _host.ExecuteAsync(
            $"UPDATE atividades SET submitted_at = now() - interval '49 hours' WHERE id = '{atividadeId}'");

        await VerificacaoAtividade.EncerrarJanelasExpiradasAsync(_host.DataSource, Token);

        Assert.Contains(await ListarAsync(ana), n => n.Tipo == "SituacaoAlterada" && n.Situacao == "Verificada");
    }

    [Fact]
    public async Task Comentario_AvisaAutorEQuemComentou_MenosQuemEscreveu()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");
        var dani = await _host.AddMembroAsync(grupoId, nome: "Dani");
        var atividadeId = await CriarAtividadeAsync(bia, grupoId);
        await ReagirAsync(caio, atividadeId, TipoReacao.Suspeito);

        await ComentarAsync(caio, atividadeId, "Parece estranho.");
        await ComentarAsync(bia, atividadeId, "Foi na esteira.");

        Assert.Equal(1, Assert.Single(await ListarAsync(bia), n => n.Tipo == "Comentario").Total);
        // Ana é a proprietária, mas não comentou: comentários não avisam administradores.
        Assert.DoesNotContain(await ListarAsync(ana), n => n.Tipo == "Comentario");
        Assert.Equal("Bia", Assert.Single(await ListarAsync(caio), n => n.Tipo == "Comentario").AtorNome);
        Assert.DoesNotContain(await ListarAsync(dani), n => n.Tipo == "Comentario");
    }

    [Fact]
    public async Task QuemSaiuDoGrupo_NaoVeMaisNotificacoesDele()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await CriarAtividadeAsync(ana, grupoId);
        Assert.Equal(1, await ContarAsync(bia));

        var saida = await bia.Client.PostAsync($"/api/grupos/{grupoId}/sair", null, Token);
        Assert.Equal(HttpStatusCode.NoContent, saida.StatusCode);

        Assert.Empty(await ListarAsync(bia));
        Assert.Equal(0, await ContarAsync(bia));
    }

    [Fact]
    public async Task MarcarLida_SoAsProprias_ETodasDeUmaVez()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await CriarAtividadeAsync(ana, grupoId);
        await CriarAtividadeAsync(ana, grupoId);
        var primeira = (await ListarAsync(bia))[0];

        await ApiTestHost.AssertProblemAsync(
            await MarcarAsync(ana, primeira.Id),
            HttpStatusCode.NotFound,
            "NOTIFICACAO_NAO_ENCONTRADA");
        Assert.Equal(2, await ContarAsync(bia));

        Assert.Equal(HttpStatusCode.NoContent, (await MarcarAsync(bia, primeira.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await MarcarAsync(bia, primeira.Id)).StatusCode);
        Assert.Equal(1, await ContarAsync(bia));

        var todas = await bia.Client.PostAsync("/api/notificacoes/lidas", null, Token);
        Assert.Equal(HttpStatusCode.NoContent, todas.StatusCode);
        Assert.Equal(0, await ContarAsync(bia));
    }

    [Fact]
    public async Task ExcluirAntigas_ApagaDepoisDe90Dias()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");
        await CriarAtividadeAsync(ana, grupoId);
        await _host.ExecuteAsync(
            $"UPDATE notificacoes SET updated_at = now() - interval '91 days' WHERE usuario_id = '{bia.Id}'");

        Assert.Equal(1, await NotificacoesAtividade.ExcluirAntigasAsync(_host.DataSource, Token));

        Assert.Empty(await ListarAsync(bia));
        Assert.Single(await ListarAsync(caio));
    }

    [Fact]
    public async Task Rotas_ExigemSessao()
    {
        var anonimo = _host.CreateAnonimo();

        await ApiTestHost.AssertProblemAsync(
            await anonimo.GetAsync("/api/notificacoes", Token),
            HttpStatusCode.Unauthorized,
            "NAO_AUTENTICADO");
        await ApiTestHost.AssertProblemAsync(
            await anonimo.GetAsync("/api/notificacoes/nao-lidas", Token),
            HttpStatusCode.Unauthorized,
            "NAO_AUTENTICADO");
    }

    private static async Task<IReadOnlyList<NotificacaoResponse>> ListarAsync(TestUsuario usuario) =>
        (await usuario.Client.GetFromJsonAsync(
            "/api/notificacoes",
            AppJsonContext.Default.NotificacoesResponse,
            Token))!.Notificacoes;

    private static async Task<int> ContarAsync(TestUsuario usuario) =>
        (await usuario.Client.GetFromJsonAsync(
            "/api/notificacoes/nao-lidas",
            AppJsonContext.Default.NotificacoesNaoLidasResponse,
            Token))!.Total;

    private static Task<HttpResponseMessage> MarcarAsync(TestUsuario usuario, Guid id) =>
        usuario.Client.PostAsync($"/api/notificacoes/{id}/lida", null, Token);

    private static async Task<Guid> CriarAtividadeAsync(TestUsuario usuario, Guid grupoId)
    {
        var response = await usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                TipoAtividade.Corrida,
                3600,
                DateTimeOffset.UtcNow.AddMinutes(-30),
                null,
                TipoComprovacao.RegistroManual),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        response.EnsureSuccessStatusCode();
        var atividade = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        return atividade!.Id;
    }

    private static async Task ReagirAsync(TestUsuario usuario, Guid atividadeId, string tipo)
    {
        var response = await usuario.Client.PutAsJsonAsync(
            $"/api/atividades/{atividadeId}/reacao",
            new ReacaoRequest(tipo),
            AppJsonContext.Default.ReacaoRequest,
            Token);
        response.EnsureSuccessStatusCode();
    }

    private static async Task ComentarAsync(TestUsuario usuario, Guid atividadeId, string texto)
    {
        var response = await usuario.Client.PostAsJsonAsync(
            $"/api/atividades/{atividadeId}/comentarios",
            new ComentarioRequest(texto),
            AppJsonContext.Default.ComentarioRequest,
            Token);
        response.EnsureSuccessStatusCode();
    }
}
