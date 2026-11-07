using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Jogo;
using Wrexercise.Features.Reacoes;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Jogo;

public sealed class JogadasRoutesTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Jogadas_Retorna404ParaQuemNaoEMembro()
    {
        var (_, _, grupoId) = await CriarParAsync();
        var cara = await _host.CreateUsuarioAsync();

        await ApiTestHost.AssertProblemAsync(
            await cara.Client.GetAsync($"/api/grupos/{grupoId}/jogadas", Token),
            HttpStatusCode.NotFound,
            "GRUPO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Cena_MostraQuemRecebeCartas()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await _host.ExecuteAsync($"UPDATE usuarios SET modo_inicio = 'App' WHERE id = '{bia.Id}'");

        var cena = await ana.Client.GetFromJsonAsync($"/api/grupos/{grupoId}/cena", AppJsonContext.Default.CenaResponse, Token);

        Assert.True(cena!.Membros.Single(m => m.UsuarioId == ana.Id).RecebeCartas);
        Assert.False(cena.Membros.Single(m => m.UsuarioId == bia.Id).RecebeCartas);
    }

    [Fact]
    public async Task Jogar_ParaQuemEstaNoModoApp_Recusa()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await _host.ExecuteAsync($"UPDATE usuarios SET modo_inicio = 'App' WHERE id = '{bia.Id}'");
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);

        await ApiTestHost.AssertProblemAsync(
            await PostJogadaAsync(ana, grupoId, carta.Id, bia.Id),
            HttpStatusCode.Conflict,
            "DESTINATARIO_FORA_DO_JOGO");
    }

    [Fact]
    public async Task Jogar_ParaQuemNaoEMembro_Recusa()
    {
        var (ana, _, grupoId) = await CriarParAsync();
        var cara = await _host.CreateUsuarioAsync();
        await JogoTestes.AtivarJogoAsync(_host, cara);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);

        await ApiTestHost.AssertProblemAsync(
            await PostJogadaAsync(ana, grupoId, carta.Id, cara.Id),
            HttpStatusCode.BadRequest,
            "DESTINATARIO_INVALIDO");
    }

    [Fact]
    public async Task Jogar_CartaDeDuracaoSemDesafio_Recusa()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Mais10Min);

        await ApiTestHost.AssertProblemAsync(
            await PostJogadaAsync(ana, grupoId, carta.Id, bia.Id),
            HttpStatusCode.Conflict,
            "SEM_DESAFIO_ATIVO");
    }

    [Fact]
    public async Task Jogar_DuasVezesParaAMesmaPessoa_RespeitaOsLimites()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var primeira = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var segunda = await JogoTestes.ComprarAsync(ana, CodigoCarta.FimDeSemana);
        await JogarAsync(ana, grupoId, primeira.Id, bia.Id);

        await ApiTestHost.AssertProblemAsync(
            await PostJogadaAsync(ana, grupoId, primeira.Id, bia.Id),
            HttpStatusCode.Conflict,
            "CARTA_EM_JOGO");
        await ApiTestHost.AssertProblemAsync(
            await PostJogadaAsync(ana, grupoId, segunda.Id, bia.Id),
            HttpStatusCode.Conflict,
            LimitesJogada.ErroPar);
    }

    [Fact]
    public async Task Recusar_DevolveACartaEAvisaQuemEnviou()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);

        var recebida = Assert.Single(await GetJogadasAsync(bia, grupoId));
        Assert.True(recebida.Recebida);
        Assert.True(recebida.Nova);
        Assert.Equal(SituacaoJogada.Enviada, recebida.Situacao);
        Assert.True(Assert.Single((await JogoTestes.GetLojaAsync(ana)).Mao).EmJogo);
        Assert.Equal(1, await ContarNotificacoesAsync(bia, "CartaRecebida"));

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Recusada")).StatusCode);

        Assert.False(Assert.Single((await JogoTestes.GetLojaAsync(ana)).Mao).EmJogo);
        Assert.Equal(1, await ContarNotificacoesAsync(ana, "CartaRecusada"));
        var vista = Assert.Single(await GetJogadasAsync(ana, grupoId));
        Assert.Equal(SituacaoJogada.Recusada, vista.Situacao);
        Assert.True(vista.Nova);
        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(bia, jogadaId, "Aceita"),
            HttpStatusCode.Conflict,
            "JOGADA_ENCERRADA");
        await ApiTestHost.AssertProblemAsync(
            await PostJogadaAsync(ana, grupoId, carta.Id, bia.Id),
            HttpStatusCode.Conflict,
            LimitesJogada.ErroRecente);
    }

    [Fact]
    public async Task Responder_SoODestinatario()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);

        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(ana, jogadaId, "Aceita"),
            HttpStatusCode.NotFound,
            "JOGADA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task MarcarVistas_TiraANovidade()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);

        var response = await bia.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/jogadas/vistas",
            new MarcarJogadasVistasRequest([jogadaId]),
            AppJsonContext.Default.MarcarJogadasVistasRequest,
            Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(Assert.Single(await GetJogadasAsync(bia, grupoId)).Nova);
    }

    [Fact]
    public async Task Aceitar_ECumprir_RecompensaOsDoisEGastaACarta()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Aceita")).StatusCode);
        Assert.Equal(1, await ContarNotificacoesAsync(ana, "CartaAceita"));

        // Ana é a única revisora: a atividade da Bia aguarda verificação até a reação dela.
        var atividade = await PostAtividadeAsync(bia, grupoId);
        Assert.Equal(SituacaoJogada.AguardandoVerificacao, Assert.Single(await GetJogadasAsync(bia, grupoId)).Situacao);

        var reacao = await ana.Client.PutAsJsonAsync(
            $"/api/atividades/{atividade.Id}/reacao",
            new ReacaoRequest(TipoReacao.Kudos),
            AppJsonContext.Default.ReacaoRequest,
            Token);
        reacao.EnsureSuccessStatusCode();

        Assert.Equal(SituacaoJogada.Concluida, Assert.Single(await GetJogadasAsync(bia, grupoId)).Situacao);
        Assert.Equal(35, (await GetEnergiaAsync(bia)).Detalhamento.Jogadas);
        Assert.Equal(17, (await GetEnergiaAsync(ana)).Detalhamento.Jogadas);
        Assert.Empty((await JogoTestes.GetLojaAsync(ana)).Mao);
        Assert.Equal(1, await ContarNotificacoesAsync(ana, "JogadaConcluida"));
        Assert.Equal(1, await ContarNotificacoesAsync(bia, "JogadaConcluida"));
    }

    /// <summary>Ana e Bia no modo Jogo, num grupo só delas; Ana com Energia para algumas cartas.</summary>
    private async Task<(TestUsuario Ana, TestUsuario Bia, Guid GrupoId)> CriarParAsync()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await JogoTestes.AtivarJogoAsync(_host, ana);
        await JogoTestes.AtivarJogoAsync(_host, bia);
        await JogoTestes.DarEnergiaAsync(_host, ana, grupoId, 12);
        return (ana, bia, grupoId);
    }

    private static Task<HttpResponseMessage> PostJogadaAsync(TestUsuario remetente, Guid grupoId, Guid cartaId, Guid destinatarioId) =>
        remetente.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/jogadas",
            new JogarCartaRequest(cartaId, destinatarioId),
            AppJsonContext.Default.JogarCartaRequest,
            Token);

    private static async Task<Guid> JogarAsync(TestUsuario remetente, Guid grupoId, Guid cartaId, Guid destinatarioId)
    {
        var response = await PostJogadaAsync(remetente, grupoId, cartaId, destinatarioId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.JogadaCriadaResponse, Token))!.Id;
    }

    private static Task<HttpResponseMessage> PutRespostaAsync(TestUsuario usuario, Guid jogadaId, string resposta) =>
        usuario.Client.PutAsJsonAsync(
            $"/api/jogadas/{jogadaId}/resposta",
            new ResponderJogadaRequest(resposta),
            AppJsonContext.Default.ResponderJogadaRequest,
            Token);

    private static async Task<IReadOnlyList<JogadaResponse>> GetJogadasAsync(TestUsuario usuario, Guid grupoId) =>
        (await usuario.Client.GetFromJsonAsync($"/api/grupos/{grupoId}/jogadas", AppJsonContext.Default.JogadasResponse, Token))!
            .Jogadas;

    private static async Task<EnergiaResponse> GetEnergiaAsync(TestUsuario usuario) =>
        (await usuario.Client.GetFromJsonAsync("/api/jogo/energia", AppJsonContext.Default.EnergiaResponse, Token))!;

    private Task<long> ContarNotificacoesAsync(TestUsuario usuario, string tipo) =>
        _host.CountAsync($"SELECT count(*) FROM notificacoes WHERE usuario_id = '{usuario.Id}' AND tipo = '{tipo}'");

    private static async Task<AtividadeResponse> PostAtividadeAsync(TestUsuario usuario, Guid grupoId)
    {
        var response = await usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                TipoAtividade.Corrida,
                3600,
                DateTimeOffset.UtcNow,
                "Treino",
                TipoComprovacao.RegistroManual),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token))!;
    }
}
