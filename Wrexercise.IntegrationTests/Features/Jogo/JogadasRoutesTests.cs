using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
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
    public async Task Jogar_EmQuemJaJogouEmVoce_Revida()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 12);
        var revide = await JogoTestes.ComprarAsync(bia, CodigoCarta.FimDeSemana);
        await JogarAsync(ana, grupoId, carta.Id, bia.Id);

        await JogarAsync(bia, grupoId, revide.Id, ana.Id);

        var jogadas = await GetJogadasAsync(bia, grupoId);
        Assert.Equal(2, jogadas.Count);
        Assert.Single(jogadas, j => j.Recebida);
        Assert.Single(jogadas, j => !j.Recebida);
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
        // Cumprir não dá FADIGA a ninguém.
        Assert.Equal(0, (await GetEnergiaAsync(bia)).FadigaRestante);
        Assert.Equal(0, (await GetEnergiaAsync(ana)).FadigaRestante);
        Assert.Empty((await JogoTestes.GetLojaAsync(ana)).Mao);
        Assert.Equal(1, await ContarNotificacoesAsync(ana, "JogadaConcluida"));
        Assert.Equal(0, await ContarNotificacoesAsync(bia, "JogadaConcluida"));
    }

    [Fact]
    public async Task Aposta_ConcluidaPagaODobroEGastaAResposta()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 3);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var aposta = await JogoTestes.ComprarAsync(bia, CodigoCarta.Aposta);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Aceita", aposta.Id)).StatusCode);
        Assert.Empty((await JogoTestes.GetLojaAsync(bia)).Mao);
        Assert.Equal(CodigoCarta.Aposta, Assert.Single(await GetJogadasAsync(bia, grupoId)).CartaResposta?.Codigo);

        var atividade = await PostAtividadeAsync(bia, grupoId);
        (await ana.Client.PutAsJsonAsync(
            $"/api/atividades/{atividade.Id}/reacao",
            new ReacaoRequest(TipoReacao.Kudos),
            AppJsonContext.Default.ReacaoRequest,
            Token)).EnsureSuccessStatusCode();

        Assert.Equal(70, (await GetEnergiaAsync(bia)).Detalhamento.Jogadas);
        Assert.Equal(17, (await GetEnergiaAsync(ana)).Detalhamento.Jogadas);
    }

    [Fact]
    public async Task PrazoExtra_SomaVinteEQuatroHoras()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 2);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var prazoExtra = await JogoTestes.ComprarAsync(bia, CodigoCarta.PrazoExtra);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);
        var antes = Assert.Single(await GetJogadasAsync(bia, grupoId)).ExpiraEm;

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Aceita", prazoExtra.Id)).StatusCode);

        var depois = Assert.Single(await GetJogadasAsync(bia, grupoId));
        Assert.Equal(antes.AddHours(24), depois.ExpiraEm);
        Assert.Equal(SituacaoJogada.Aceita, depois.Situacao);
    }

    [Fact]
    public async Task MudancaDePlanos_TrocaPorUmEquivalente()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 3);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var mudanca = await JogoTestes.ComprarAsync(bia, CodigoCarta.MudancaDePlanos);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);
        var recebida = Assert.Single(await GetJogadasAsync(bia, grupoId));

        // Sem desafio ativo, a troca para +10 MIN não aparece e é recusada.
        Assert.Equal([CodigoCarta.FimDeSemana], recebida.ObjetivosEquivalentes);
        Assert.Empty(Assert.Single(await GetJogadasAsync(ana, grupoId)).ObjetivosEquivalentes);
        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(bia, jogadaId, "Aceita", mudanca.Id, CodigoCarta.Mais10Min),
            HttpStatusCode.Conflict,
            "SEM_DESAFIO_ATIVO");
        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(bia, jogadaId, "Aceita", mudanca.Id, CodigoCarta.AntesDas8),
            HttpStatusCode.BadRequest,
            "OBJETIVO_INVALIDO");

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await PutRespostaAsync(bia, jogadaId, "Aceita", mudanca.Id, CodigoCarta.FimDeSemana)).StatusCode);

        var trocada = Assert.Single(await GetJogadasAsync(bia, grupoId));
        Assert.Equal(CodigoCarta.FimDeSemana, trocada.Objetivo);
        Assert.Equal(CodigoCarta.Variedade, trocada.Carta.Codigo);
        Assert.Null(trocada.TipoEvitar);
        Assert.True(trocada.ExpiraEm >= recebida.ExpiraEm);
        Assert.Empty((await JogoTestes.GetLojaAsync(bia)).Mao);
    }

    [Fact]
    public async Task DesafioDuplo_DevolveOObjetivoSemConferirOsLimites()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 4);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var duploDaAna = await JogoTestes.ComprarAsync(ana, CodigoCarta.DesafioDuplo);
        var duplo = await JogoTestes.ComprarAsync(bia, CodigoCarta.DesafioDuplo);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Aceita", duplo.Id)).StatusCode);

        var devolvida = Assert.Single(await GetJogadasAsync(ana, grupoId), j => j.Recebida);
        Assert.True(devolvida.DesafioDuplo);
        Assert.Equal(bia.Id, devolvida.RemetenteId);
        Assert.Equal(CodigoCarta.Variedade, devolvida.Carta.Codigo);
        Assert.Equal(SituacaoJogada.Enviada, devolvida.Situacao);
        Assert.Equal(1, await ContarNotificacoesAsync(ana, "CartaRecebida"));
        Assert.Equal(1, await ContarNotificacoesAsync(ana, "CartaAceita"));
        Assert.Empty((await JogoTestes.GetLojaAsync(bia)).Mao);
        // A Provocação continua na Mão da Ana, em jogo pela Jogada original.
        Assert.True((await JogoTestes.GetLojaAsync(ana)).Mao.Single(c => c.Id == carta.Id).EmJogo);

        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(ana, devolvida.Id, "Aceita", duploDaAna.Id),
            HttpStatusCode.Conflict,
            "DESAFIO_DUPLO_INVALIDO");
    }

    [Fact]
    public async Task DesafioDuplo_ParaQuemEstaNoModoApp_RecusaSemGastarACarta()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 4);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var duplo = await JogoTestes.ComprarAsync(bia, CodigoCarta.DesafioDuplo);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);
        await _host.ExecuteAsync($"UPDATE usuarios SET modo_inicio = 'App' WHERE id = '{ana.Id}'");

        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(bia, jogadaId, "Aceita", duplo.Id),
            HttpStatusCode.Conflict,
            "DESTINATARIO_FORA_DO_JOGO");
        Assert.Single((await JogoTestes.GetLojaAsync(bia)).Mao);
        Assert.Equal(SituacaoJogada.Enviada, Assert.Single(await GetJogadasAsync(bia, grupoId)).Situacao);
    }

    [Fact]
    public async Task Responder_ComCartaInvalida_Recusa()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 6);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var outraDaAna = await JogoTestes.ComprarAsync(ana, CodigoCarta.Aposta);
        var provocacaoDaBia = await JogoTestes.ComprarAsync(bia, CodigoCarta.FimDeSemana);
        var aposta = await JogoTestes.ComprarAsync(bia, CodigoCarta.Aposta);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);

        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(bia, jogadaId, "Recusada", aposta.Id),
            HttpStatusCode.BadRequest,
            "DADOS_INVALIDOS");
        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(bia, jogadaId, "Aceita", outraDaAna.Id),
            HttpStatusCode.NotFound,
            "CARTA_NAO_ENCONTRADA");
        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(bia, jogadaId, "Aceita", provocacaoDaBia.Id),
            HttpStatusCode.BadRequest,
            "CARTA_INVALIDA");
        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(bia, jogadaId, "Aceita", aposta.Id, CodigoCarta.FimDeSemana),
            HttpStatusCode.BadRequest,
            "CARTA_INVALIDA");
    }

    [Fact]
    public async Task Fusao_CumpridaNoMesmoTreino_PagaASomaComBonusEGastaAsDuasCartas()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var variedade = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var fimDeSemana = await JogoTestes.ComprarAsync(ana, CodigoCarta.FimDeSemana);

        var jogadaId = await JogarAsync(ana, grupoId, variedade.Id, bia.Id, fimDeSemana.Id);

        Assert.All((await JogoTestes.GetLojaAsync(ana)).Mao, c => Assert.True(c.EmJogo));
        var recebida = Assert.Single(await GetJogadasAsync(bia, grupoId));
        Assert.Equal(CodigoCarta.Variedade, recebida.Carta.Codigo);
        Assert.Equal(CodigoCarta.FimDeSemana, recebida.CartaFusao?.Codigo);
        // 35 + 35 = 70, mais 20%.
        Assert.Equal(84, recebida.Recompensa);
        Assert.Empty(recebida.ObjetivosEquivalentes);
        Assert.Equal(1, await ContarNotificacoesAsync(bia, "CartaRecebida"));

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Aceita")).StatusCode);

        // O treino vai para o meio do domingo em que a Jogada acaba: dentro do prazo e no fim de semana.
        var atividade = await PostAtividadeAsync(bia, grupoId);
        await _host.ExecuteAsync(
            $"UPDATE atividades SET started_at = (SELECT expira_em - interval '12 hours' FROM jogadas WHERE id = '{jogadaId}') WHERE id = '{atividade.Id}'");
        (await ana.Client.PutAsJsonAsync(
            $"/api/atividades/{atividade.Id}/reacao",
            new ReacaoRequest(TipoReacao.Kudos),
            AppJsonContext.Default.ReacaoRequest,
            Token)).EnsureSuccessStatusCode();

        Assert.Equal(SituacaoJogada.Concluida, Assert.Single(await GetJogadasAsync(bia, grupoId)).Situacao);
        Assert.Equal(84, (await GetEnergiaAsync(bia)).Detalhamento.Jogadas);
        Assert.Equal(35, (await GetEnergiaAsync(ana)).Detalhamento.Jogadas);
        Assert.Empty((await JogoTestes.GetLojaAsync(ana)).Mao);
        Assert.Equal(1, await ContarNotificacoesAsync(ana, "JogadaConcluida"));
    }

    [Fact]
    public async Task Fusao_SoUmObjetivoCumprido_NaoConclui()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var variedade = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var fimDeSemana = await JogoTestes.ComprarAsync(ana, CodigoCarta.FimDeSemana);
        var jogadaId = await JogarAsync(ana, grupoId, variedade.Id, bia.Id, fimDeSemana.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Aceita")).StatusCode);

        // Um treino na segunda-feira antes do domingo do prazo: cumpre a VARIEDADE, mas não o FIM DE SEMANA.
        var atividade = await PostAtividadeAsync(bia, grupoId);
        await _host.ExecuteAsync(
            $"UPDATE jogadas SET enviada_em = expira_em - interval '8 days' WHERE id = '{jogadaId}'");
        await _host.ExecuteAsync(
            $"UPDATE atividades SET started_at = (SELECT expira_em - interval '6 days 12 hours' FROM jogadas WHERE id = '{jogadaId}') WHERE id = '{atividade.Id}'");
        (await ana.Client.PutAsJsonAsync(
            $"/api/atividades/{atividade.Id}/reacao",
            new ReacaoRequest(TipoReacao.Kudos),
            AppJsonContext.Default.ReacaoRequest,
            Token)).EnsureSuccessStatusCode();

        Assert.Equal(SituacaoJogada.Aceita, Assert.Single(await GetJogadasAsync(bia, grupoId)).Situacao);
        Assert.Equal(0, (await GetEnergiaAsync(bia)).Detalhamento.Jogadas);
        Assert.Equal(0, await ContarNotificacoesAsync(ana, "JogadaConcluida"));
    }

    [Fact]
    public async Task Fusao_Invalida_Recusa()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var variedade = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var outraVariedade = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var aposta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Aposta);

        await ApiTestHost.AssertProblemAsync(
            await PostJogadaAsync(ana, grupoId, variedade.Id, bia.Id, outraVariedade.Id),
            HttpStatusCode.BadRequest,
            "FUSAO_INVALIDA");
        await ApiTestHost.AssertProblemAsync(
            await PostJogadaAsync(ana, grupoId, variedade.Id, bia.Id, variedade.Id),
            HttpStatusCode.BadRequest,
            "FUSAO_INVALIDA");
        await ApiTestHost.AssertProblemAsync(
            await PostJogadaAsync(ana, grupoId, variedade.Id, bia.Id, aposta.Id),
            HttpStatusCode.BadRequest,
            "FUSAO_INVALIDA");
        await ApiTestHost.AssertProblemAsync(
            await PostJogadaAsync(ana, grupoId, variedade.Id, bia.Id, Guid.NewGuid()),
            HttpStatusCode.NotFound,
            "CARTA_NAO_ENCONTRADA");
        Assert.Empty(await GetJogadasAsync(bia, grupoId));
    }

    [Fact]
    public async Task Fusao_Recusada_DevolveAsDuasCartas()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var variedade = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var antesDas8 = await JogoTestes.ComprarAsync(ana, CodigoCarta.AntesDas8);
        var jogadaId = await JogarAsync(ana, grupoId, variedade.Id, bia.Id, antesDas8.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Recusada")).StatusCode);

        var mao = (await JogoTestes.GetLojaAsync(ana)).Mao;
        Assert.Equal(2, mao.Count);
        Assert.All(mao, c => Assert.False(c.EmJogo));
        Assert.Equal(Fadiga.Cargas, (await GetEnergiaAsync(bia)).FadigaRestante);
    }

    [Fact]
    public async Task Fusao_NaoAceitaMudancaDePlanos_MasODesafioDuploDevolveAsDuas()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 7);
        var variedade = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var antesDas8 = await JogoTestes.ComprarAsync(ana, CodigoCarta.AntesDas8);
        var mudanca = await JogoTestes.ComprarAsync(bia, CodigoCarta.MudancaDePlanos);
        var duplo = await JogoTestes.ComprarAsync(bia, CodigoCarta.DesafioDuplo);
        var jogadaId = await JogarAsync(ana, grupoId, variedade.Id, bia.Id, antesDas8.Id);

        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(bia, jogadaId, "Aceita", mudanca.Id, CodigoCarta.FimDeSemana),
            HttpStatusCode.BadRequest,
            "CARTA_INVALIDA");

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Aceita", duplo.Id)).StatusCode);

        var devolvida = Assert.Single(await GetJogadasAsync(ana, grupoId), j => j.Recebida);
        Assert.True(devolvida.DesafioDuplo);
        Assert.Equal(CodigoCarta.Variedade, devolvida.Carta.Codigo);
        Assert.Equal(CodigoCarta.AntesDas8, devolvida.CartaFusao?.Codigo);
        // 35 + 40 = 75, mais 20%.
        Assert.Equal(90, devolvida.Recompensa);
        // As duas Provocações continuam na Mão da Ana, em jogo pela Jogada original.
        Assert.All(
            (await JogoTestes.GetLojaAsync(ana)).Mao.Where(c => c.Tipo == TipoCarta.Provocacao),
            c => Assert.True(c.EmJogo));
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

    private static Task<HttpResponseMessage> PostJogadaAsync(
        TestUsuario remetente,
        Guid grupoId,
        Guid cartaId,
        Guid destinatarioId,
        Guid? cartaFusaoId = null) =>
        remetente.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/jogadas",
            new JogarCartaRequest(cartaId, destinatarioId, cartaFusaoId),
            AppJsonContext.Default.JogarCartaRequest,
            Token);

    private static async Task<Guid> JogarAsync(
        TestUsuario remetente,
        Guid grupoId,
        Guid cartaId,
        Guid destinatarioId,
        Guid? cartaFusaoId = null)
    {
        var response = await PostJogadaAsync(remetente, grupoId, cartaId, destinatarioId, cartaFusaoId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.JogadaCriadaResponse, Token))!.Id;
    }

    private static Task<HttpResponseMessage> PutRespostaAsync(
        TestUsuario usuario,
        Guid jogadaId,
        string resposta,
        Guid? cartaRespostaId = null,
        string? objetivo = null) =>
        usuario.Client.PutAsJsonAsync(
            $"/api/jogadas/{jogadaId}/resposta",
            new ResponderJogadaRequest(resposta, cartaRespostaId, objetivo),
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
