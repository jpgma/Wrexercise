using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Reacoes;
using Wrexercise.Infrastructure.Json;
using Situacao = Wrexercise.Features.Reacoes.SituacaoVerificacao;

namespace Wrexercise.IntegrationTests.Features.Reacoes;

public sealed class ReacoesRoutesTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Create_SemRevisores_VerificadaImediata()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);

        var criada = await PostAtividadeAsync(ana, grupoId);
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        Assert.Equal(Situacao.Verificada, atividade!.SituacaoVerificacao);
    }

    [Fact]
    public async Task PutReacao_AutorRecebe403()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");

        var criada = await PostAtividadeAsync(ana, grupoId);
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        var response = await PutReacaoAsync(ana, atividade!.Id, TipoReacao.Kudos);
        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTOR_NAO_REAGE");
    }

    [Fact]
    public async Task PutReacao_TodosKudos_Verificada()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");

        var criada = await PostAtividadeAsync(ana, grupoId);
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        var response = await PutReacaoAsync(bia, atividade!.Id, TipoReacao.Kudos);
        response.EnsureSuccessStatusCode();

        var detalhe = await ana.Client.GetAsync($"/api/atividades/{atividade.Id}", Token);
        var atualizada = await detalhe.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        Assert.Equal(Situacao.Verificada, atualizada!.SituacaoVerificacao);
    }

    [Fact]
    public async Task PutReacao_Suspeito_EmRevisao()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");

        var criada = await PostAtividadeAsync(ana, grupoId);
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        await PutReacaoAsync(bia, atividade!.Id, TipoReacao.Suspeito);

        var detalhe = await ana.Client.GetAsync($"/api/atividades/{atividade.Id}", Token);
        var atualizada = await detalhe.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        Assert.Equal(Situacao.EmRevisao, atualizada!.SituacaoVerificacao);
    }

    [Fact]
    public async Task PostRevisao_AdminResolve()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");

        var criada = await PostAtividadeAsync(ana, grupoId);
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        await PutReacaoAsync(bia, atividade!.Id, TipoReacao.Suspeito);

        // Quem é autor não decide a própria disputa, mesmo sendo dono do grupo (ADR 0052).
        await ApiTestHost.AssertProblemAsync(
            await PostRevisaoAsync(ana, atividade.Id, Situacao.Verificada, "Fui eu mesma."),
            HttpStatusCode.Forbidden,
            "REVISAO_PROPRIA_NAO_PERMITIDA");

        var duda = await _host.AddMembroAsync(grupoId, PapelMembro.Administrador, "Duda");
        var response = await PostRevisaoAsync(duda, atividade.Id, Situacao.Verificada, "Evidência consistente.");
        response.EnsureSuccessStatusCode();

        var detalhe = await ana.Client.GetAsync($"/api/atividades/{atividade.Id}", Token);
        var atualizada = await detalhe.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        Assert.Equal(Situacao.Verificada, atualizada!.SituacaoVerificacao);
    }

    [Fact]
    public async Task PostRevisao_Anular_AtividadeAindaNaoDecidida()
    {
        var (dono, atividadeId, revisores, admin) = await CriarComRevisoresEAdminAsync(2);

        // Aprovar ou rejeitar continua só na disputa; anular vale antes dela.
        await ApiTestHost.AssertProblemAsync(
            await PostRevisaoAsync(admin, atividadeId, Situacao.Rejeitada, "Registro em dobro."),
            HttpStatusCode.Conflict,
            "REVISAO_NAO_PENDENTE");
        await ApiTestHost.AssertProblemAsync(
            await PostRevisaoAsync(dono, atividadeId, Situacao.Anulada, "Registrei duas vezes."),
            HttpStatusCode.Forbidden,
            "REVISAO_PROPRIA_NAO_PERMITIDA");

        (await PostRevisaoAsync(admin, atividadeId, Situacao.Anulada, "Registro em dobro.")).EnsureSuccessStatusCode();

        Assert.Equal(Situacao.Anulada, await SituacaoAsync(dono, atividadeId));
        await ApiTestHost.AssertProblemAsync(
            await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Kudos),
            HttpStatusCode.Conflict,
            "REACAO_ENCERRADA");
        Assert.Equal(1L, await _host.ScalarAsync<long>(
            $"SELECT count(*) FROM notificacoes WHERE atividade_id = '{atividadeId}' AND situacao = 'Anulada'"));
    }

    [Fact]
    public async Task PostRevisao_AnularVerificada_Recusada()
    {
        var (dono, atividadeId, revisores, admin) = await CriarComRevisoresEAdminAsync(1);
        (await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Kudos)).EnsureSuccessStatusCode();
        Assert.Equal(Situacao.Verificada, await SituacaoAsync(dono, atividadeId));

        await ApiTestHost.AssertProblemAsync(
            await PostRevisaoAsync(admin, atividadeId, Situacao.Anulada, "Tipo errado."),
            HttpStatusCode.Conflict,
            "REVISAO_NAO_PENDENTE");
    }

    [Fact]
    public async Task Encerramento_PrazoExpirado_Verificada()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");

        var criada = await PostAtividadeAsync(ana, grupoId);
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        await _host.ExecuteAsync(
            $"UPDATE atividades SET submitted_at = now() - interval '49 hours' WHERE id = '{atividade!.Id}'");

        await VerificacaoAtividade.EncerrarJanelasExpiradasAsync(_host.DataSource, Token);

        var detalhe = await ana.Client.GetAsync($"/api/atividades/{atividade.Id}", Token);
        var atualizada = await detalhe.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        Assert.Equal(Situacao.Verificada, atualizada!.SituacaoVerificacao);
    }

    [Fact]
    public async Task PutReacao_EmRevisao_LimiteAtingidoRejeita()
    {
        var (ana, atividadeId, revisores) = await CriarComRevisoresAsync(3);

        (await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Rejeitar)).EnsureSuccessStatusCode();
        Assert.Equal(Situacao.EmRevisao, await SituacaoAsync(ana, atividadeId));

        var resposta = await PutReacaoAsync(revisores[1], atividadeId, TipoReacao.Rejeitar);
        resposta.EnsureSuccessStatusCode();
        var reacoes = await resposta.Content.ReadFromJsonAsync(AppJsonContext.Default.ReacoesResponse, Token);
        Assert.Equal(2, reacoes!.RejeicoesNecessarias);
        Assert.Equal(Situacao.Rejeitada, await SituacaoAsync(ana, atividadeId));
    }

    [Fact]
    public async Task PutReacao_TrocarUnicaNegativa_VoltaAAguardar()
    {
        var (ana, atividadeId, revisores) = await CriarComRevisoresAsync(2);

        (await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Suspeito)).EnsureSuccessStatusCode();
        Assert.Equal(Situacao.EmRevisao, await SituacaoAsync(ana, atividadeId));

        (await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Kudos)).EnsureSuccessStatusCode();
        Assert.Equal(Situacao.AguardandoFeedback, await SituacaoAsync(ana, atividadeId));
    }

    [Fact]
    public async Task PutReacao_EmRevisaoAposPrazo_Recusada()
    {
        var (ana, atividadeId, revisores) = await CriarComRevisoresAsync(2);
        (await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Suspeito)).EnsureSuccessStatusCode();

        await _host.ExecuteAsync(
            $"UPDATE atividades SET submitted_at = now() - interval '49 hours' WHERE id = '{atividadeId}'");

        var response = await PutReacaoAsync(revisores[1], atividadeId, TipoReacao.Kudos);
        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Conflict, "JANELA_ENCERRADA");
        Assert.Equal(Situacao.EmRevisao, await SituacaoAsync(ana, atividadeId));
    }

    [Fact]
    public async Task PutReacao_AposDecisaoAdmin_Recusada()
    {
        var (_, atividadeId, revisores, admin) = await CriarComRevisoresEAdminAsync(2);
        (await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Suspeito)).EnsureSuccessStatusCode();
        (await PostRevisaoAsync(admin, atividadeId, Situacao.Verificada, "Conferido.")).EnsureSuccessStatusCode();

        var response = await PutReacaoAsync(revisores[1], atividadeId, TipoReacao.Rejeitar);
        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Conflict, "REACAO_ENCERRADA");
    }

    [Fact]
    public async Task Comentarios_AguardandoFeedback_Aceitos()
    {
        var (ana, atividadeId, revisores) = await CriarComRevisoresAsync(2);

        var response = await PostComentarioAsync(revisores[0], atividadeId, "Boa, mandou bem!");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(Situacao.AguardandoFeedback, await SituacaoAsync(ana, atividadeId));
    }

    [Fact]
    public async Task Comentarios_AposDecisao_ContinuamAbertos()
    {
        var (ana, atividadeId, revisores, admin) = await CriarComRevisoresEAdminAsync(2);
        (await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Suspeito)).EnsureSuccessStatusCode();

        var criado = await PostComentarioAsync(ana, atividadeId, "  Foi na esteira.  ");
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        (await PostRevisaoAsync(admin, atividadeId, Situacao.Verificada, "Conferido.")).EnsureSuccessStatusCode();

        var depois = await PostComentarioAsync(revisores[1], atividadeId, "Faz sentido.");
        Assert.Equal(HttpStatusCode.Created, depois.StatusCode);

        var lista = await revisores[0].Client.GetFromJsonAsync(
            $"/api/atividades/{atividadeId}/comentarios",
            AppJsonContext.Default.ComentariosResponse,
            Token);
        Assert.Equal(["Foi na esteira.", "Faz sentido."], lista!.Comentarios.Select(c => c.Texto));
    }

    [Fact]
    public async Task Comentarios_TotalAparecemNoFeed()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Membro, "Bia");
        var criada = await PostAtividadeAsync(ana, grupoId);
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        (await PostComentarioAsync(bia, atividade!.Id, "Boa!")).EnsureSuccessStatusCode();
        (await PostComentarioAsync(ana, atividade.Id, "Valeu!")).EnsureSuccessStatusCode();

        var feed = await bia.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            AppJsonContext.Default.AtividadesPaginadasResponse,
            Token);
        Assert.Equal(2, Assert.Single(feed!.Atividades).Comentarios);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Comentarios_TextoVazio_Recusado(string? texto)
    {
        var (ana, atividadeId, revisores) = await CriarComRevisoresAsync(1);
        (await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Suspeito)).EnsureSuccessStatusCode();

        var response = await PostComentarioAsync(ana, atividadeId, texto);
        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task Comentarios_TextoLongo_Recusado()
    {
        var (ana, atividadeId, revisores) = await CriarComRevisoresAsync(1);
        (await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Suspeito)).EnsureSuccessStatusCode();

        var response = await PostComentarioAsync(ana, atividadeId, new string('a', 501));
        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task Comentarios_ForaDoGrupo_NaoEncontrada()
    {
        var (ana, atividadeId, revisores) = await CriarComRevisoresAsync(1);
        (await PutReacaoAsync(revisores[0], atividadeId, TipoReacao.Suspeito)).EnsureSuccessStatusCode();
        var estranha = await _host.CreateUsuarioAsync("Estranha");

        var get = await estranha.Client.GetAsync($"/api/atividades/{atividadeId}/comentarios", Token);
        await ApiTestHost.AssertProblemAsync(get, HttpStatusCode.NotFound, "ATIVIDADE_NAO_ENCONTRADA");
        var post = await PostComentarioAsync(estranha, atividadeId, "Oi");
        await ApiTestHost.AssertProblemAsync(post, HttpStatusCode.NotFound, "ATIVIDADE_NAO_ENCONTRADA");
    }

    private static Task<HttpResponseMessage> PostComentarioAsync(TestUsuario usuario, Guid atividadeId, string? texto) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/atividades/{atividadeId}/comentarios",
            new ComentarioRequest(texto),
            AppJsonContext.Default.ComentarioRequest,
            Token);

    private async Task<(TestUsuario Dono, Guid AtividadeId, List<TestUsuario> Revisores)>
        CriarComRevisoresAsync(int quantidade)
    {
        var dono = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(dono);
        var revisores = new List<TestUsuario>();
        for (var i = 0; i < quantidade; i++)
        {
            revisores.Add(await _host.AddMembroAsync(grupoId, PapelMembro.Membro, $"Revisor {i}"));
        }

        var criada = await PostAtividadeAsync(dono, grupoId);
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        return (dono, atividade!.Id, revisores);
    }

    /// <summary>
    /// Como <see cref="CriarComRevisoresAsync"/>, mais um administrador que entra depois da atividade:
    /// fica fora dos revisores e pode decidir a disputa, que o autor não decide.
    /// </summary>
    private async Task<(TestUsuario Dono, Guid AtividadeId, List<TestUsuario> Revisores, TestUsuario Admin)>
        CriarComRevisoresEAdminAsync(int quantidade)
    {
        var (dono, atividadeId, revisores) = await CriarComRevisoresAsync(quantidade);
        var grupoId = await _host.ScalarAsync<Guid>(
            $"SELECT grupo_id FROM atividades WHERE id = '{atividadeId}'");
        var admin = await _host.AddMembroAsync(grupoId, PapelMembro.Administrador, "Admin");
        return (dono, atividadeId, revisores, admin);
    }

    private static async Task<string> SituacaoAsync(TestUsuario usuario, Guid atividadeId)
    {
        var detalhe = await usuario.Client.GetAsync($"/api/atividades/{atividadeId}", Token);
        var atividade = await detalhe.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        return atividade!.SituacaoVerificacao;
    }

    private static CriarAtividadeRequest Valid() => new(
        TipoAtividade.Corrida,
        3600,
        DateTimeOffset.UtcNow.AddMinutes(-30),
        null,
        TipoComprovacao.RegistroManual);

    private static Task<HttpResponseMessage> PostAtividadeAsync(TestUsuario usuario, Guid grupoId) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            Valid(),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);

    private static Task<HttpResponseMessage> PutReacaoAsync(TestUsuario usuario, Guid atividadeId, string tipo) =>
        usuario.Client.PutAsJsonAsync(
            $"/api/atividades/{atividadeId}/reacao",
            new ReacaoRequest(tipo),
            AppJsonContext.Default.ReacaoRequest,
            Token);

    private static Task<HttpResponseMessage> PostRevisaoAsync(
        TestUsuario usuario,
        Guid atividadeId,
        string decisao,
        string justificativa) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/atividades/{atividadeId}/revisao",
            new RevisaoRequest(decisao, justificativa),
            AppJsonContext.Default.RevisaoRequest,
            Token);
}
