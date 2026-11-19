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
            HttpStatusCode.NotFound,
            "CARTA_NAO_ENCONTRADA");
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
    public async Task Recusar_NaoDevolveACartaEAvisaQuemEnviou()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);

        var recebida = Assert.Single(await GetJogadasAsync(bia, grupoId));
        Assert.True(recebida.Recebida);
        Assert.True(recebida.Nova);
        Assert.Equal(SituacaoJogada.Enviada, recebida.Situacao);
        // A carta sai da Mão no arremesso e não volta (decisão 0074).
        Assert.Empty((await JogoTestes.GetLojaAsync(ana)).Mao);
        Assert.Equal(1, await ContarNotificacoesAsync(bia, "CartaRecebida"));

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Recusada")).StatusCode);

        Assert.Empty((await JogoTestes.GetLojaAsync(ana)).Mao);
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
            HttpStatusCode.NotFound,
            "CARTA_NAO_ENCONTRADA");
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
        Assert.Equal(35, (await GetEnergiaAsync(ana)).Detalhamento.Jogadas);
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
        Assert.Equal(35, (await GetEnergiaAsync(ana)).Detalhamento.Jogadas);
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
        // A Provocação saiu da Mão da Ana no arremesso: a Jogada devolvida não a traz de volta.
        Assert.DoesNotContain((await JogoTestes.GetLojaAsync(ana)).Mao, c => c.Id == carta.Id);

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

        Assert.Empty((await JogoTestes.GetLojaAsync(ana)).Mao);
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
        // Quem enviou recebe a soma de volta, sem o bônus.
        Assert.Equal(70, (await GetEnergiaAsync(ana)).Detalhamento.Jogadas);
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
    public async Task Fusao_Recusada_PerdeAsDuasCartas()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var variedade = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var antesDas8 = await JogoTestes.ComprarAsync(ana, CodigoCarta.AntesDas8);
        var jogadaId = await JogarAsync(ana, grupoId, variedade.Id, bia.Id, antesDas8.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Recusada")).StatusCode);

        Assert.Empty((await JogoTestes.GetLojaAsync(ana)).Mao);
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
        // As duas Provocações saíram da Mão da Ana no arremesso.
        Assert.DoesNotContain((await JogoTestes.GetLojaAsync(ana)).Mao, c => c.Tipo == TipoCarta.Provocacao);
    }

    [Fact]
    public async Task Variedade_EvitaOTipoDaUltimaVerificada_SemContarPendenteSemMoral()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Corrida, "Verificada", horasAtras: 5);
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Ciclismo, "AguardandoFeedback", horasAtras: 1);

        Assert.Equal(TipoAtividade.Corrida, await JogarVariedadeAsync(ana, bia, grupoId));
    }

    [Theory]
    [InlineData("AguardandoFeedback")]
    [InlineData("EmRevisao")]
    [InlineData("Contestada")]
    public async Task Variedade_ContaOTreinoPendenteEmQueQuemJogaDeuMoral(string situacao)
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Corrida, "Verificada", horasAtras: 5);
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Ciclismo, situacao, horasAtras: 1, moralDe: ana);

        Assert.Equal(TipoAtividade.Ciclismo, await JogarVariedadeAsync(ana, bia, grupoId));
    }

    [Theory]
    [InlineData("Rejeitada")]
    [InlineData("Anulada")]
    public async Task Variedade_IgnoraTreinoRejeitadoOuAnuladoMesmoComMoral(string situacao)
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Corrida, "Verificada", horasAtras: 5);
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Ciclismo, situacao, horasAtras: 1, moralDe: ana);

        Assert.Equal(TipoAtividade.Corrida, await JogarVariedadeAsync(ana, bia, grupoId));
    }

    [Fact]
    public async Task Variedade_EntreVerificadaEComMoral_ValeAQueComecouPorUltimo()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Ciclismo, "AguardandoFeedback", horasAtras: 5, moralDe: ana);
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Corrida, "Verificada", horasAtras: 1);

        Assert.Equal(TipoAtividade.Corrida, await JogarVariedadeAsync(ana, bia, grupoId));
    }

    [Fact]
    public async Task Variedade_SemTreinoDeReferencia_NaoEvitaNenhumTipo()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Ciclismo, "AguardandoFeedback", horasAtras: 1);

        Assert.Null(await JogarVariedadeAsync(ana, bia, grupoId));
    }

    [Fact]
    public async Task DesafioDuplo_Variedade_UsaOTreinoQueQuemDevolveAprovou()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 4);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var duplo = await JogoTestes.ComprarAsync(bia, CodigoCarta.DesafioDuplo);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);
        // A Ana só tem treinos verificados do tipo Outro; a Bia deu moral numa Natação ainda pendente.
        await InserirTreinoAsync(ana, grupoId, TipoAtividade.Natacao, "AguardandoFeedback", horasAtras: 1, moralDe: bia);

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Aceita", duplo.Id)).StatusCode);

        var devolvida = Assert.Single(await GetJogadasAsync(ana, grupoId), j => j.Recebida);
        Assert.Equal(TipoAtividade.Natacao, devolvida.TipoEvitar);
    }

    [Fact]
    public async Task MudancaDePlanos_ParaVariedade_UsaOUltimoTreinoDeQuemResponde()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 3);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.FimDeSemana);
        var mudanca = await JogoTestes.ComprarAsync(bia, CodigoCarta.MudancaDePlanos);
        var jogadaId = await JogarAsync(ana, grupoId, carta.Id, bia.Id);
        // Pendente e sem moral de ninguém: a Bia conhece os próprios treinos, então ele já conta.
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Yoga, "AguardandoFeedback", horasAtras: 1);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await PutRespostaAsync(bia, jogadaId, "Aceita", mudanca.Id, CodigoCarta.Variedade)).StatusCode);

        var trocada = Assert.Single(await GetJogadasAsync(bia, grupoId));
        Assert.Equal(CodigoCarta.Variedade, trocada.Objetivo);
        Assert.Equal(TipoAtividade.Yoga, trocada.TipoEvitar);
    }

    [Fact]
    public async Task Previa_DevolveOQueOArremessoFixa()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Corrida, "Verificada", horasAtras: 5);
        await InserirTreinoAsync(bia, grupoId, TipoAtividade.Ciclismo, "AguardandoFeedback", horasAtras: 1, moralDe: ana);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);

        var previa = await GetPreviaAsync(ana, grupoId, bia.Id, carta.Id);

        Assert.Equal(TipoAtividade.Ciclismo, previa.TipoEvitar);
        Assert.Null(previa.DuracaoAlvoSegundos);
        Assert.Equal(35, previa.RecompensaDestinatario);
        Assert.Equal(35, previa.RecompensaRemetente);
        // A prévia não grava nada.
        Assert.Empty(await GetJogadasAsync(bia, grupoId));

        await JogarAsync(ana, grupoId, carta.Id, bia.Id);
        var jogada = Assert.Single(await GetJogadasAsync(bia, grupoId));
        Assert.Equal(previa.TipoEvitar, jogada.TipoEvitar);
        Assert.Equal(previa.RecompensaDestinatario, jogada.Recompensa);
        Assert.InRange((jogada.ExpiraEm - previa.ExpiraEm).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Previa_DeFusao_SomaOsPrecosEOBonus()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var variedade = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        var antesDas8 = await JogoTestes.ComprarAsync(ana, CodigoCarta.AntesDas8);

        var previa = await GetPreviaAsync(ana, grupoId, bia.Id, variedade.Id, antesDas8.Id);

        // 35 + 40 = 75, mais 20% para quem cumpre; metade da soma para quem joga.
        Assert.Equal(90, previa.RecompensaDestinatario);
        Assert.Equal(75, previa.RecompensaRemetente);
        Assert.Null(previa.TipoEvitar);
    }

    [Fact]
    public async Task Previa_ConfereOGrupoEAsCartasComoOArremesso()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var cara = await _host.CreateUsuarioAsync();
        var duracao = await JogoTestes.ComprarAsync(ana, CodigoCarta.Mais10Min);
        var resposta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Aposta);

        await ApiTestHost.AssertProblemAsync(
            await cara.Client.GetAsync(PreviaUrl(grupoId, bia.Id, duracao.Id), Token),
            HttpStatusCode.NotFound,
            "GRUPO_NAO_ENCONTRADO");
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.GetAsync(PreviaUrl(grupoId, cara.Id, duracao.Id), Token),
            HttpStatusCode.BadRequest,
            "DESTINATARIO_INVALIDO");
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.GetAsync(PreviaUrl(grupoId, bia.Id, Guid.NewGuid()), Token),
            HttpStatusCode.NotFound,
            "CARTA_NAO_ENCONTRADA");
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.GetAsync(PreviaUrl(grupoId, bia.Id, resposta.Id), Token),
            HttpStatusCode.BadRequest,
            "CARTA_INVALIDA");
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.GetAsync(PreviaUrl(grupoId, bia.Id, duracao.Id), Token),
            HttpStatusCode.Conflict,
            "SEM_DESAFIO_ATIVO");
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

    /// <summary>A Ana joga uma VARIEDADE na Bia e devolve o tipo a evitar que ficou na Jogada.</summary>
    private static async Task<string?> JogarVariedadeAsync(TestUsuario ana, TestUsuario bia, Guid grupoId)
    {
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        await JogarAsync(ana, grupoId, carta.Id, bia.Id);
        return Assert.Single(await GetJogadasAsync(bia, grupoId)).TipoEvitar;
    }

    /// <summary>
    /// Um treino gravado direto no banco, já na situação pedida. <paramref name="moralDe"/> deixa um
    /// <c>Kudos</c> sem passar pela regra que verificaria a atividade.
    /// </summary>
    private async Task InserirTreinoAsync(
        TestUsuario autor,
        Guid grupoId,
        string tipo,
        string situacao,
        int horasAtras,
        TestUsuario? moralDe = null)
    {
        var id = Guid.NewGuid();
        await _host.ExecuteAsync($"""
            INSERT INTO atividades (
                id, grupo_id, usuario_id, tipo, duracao_segundos, started_at, tipo_comprovacao,
                situacao_verificacao, verified_at)
            VALUES (
                '{id}', '{grupoId}', '{autor.Id}', '{tipo}', 1800, now() - interval '{horasAtras} hours',
                'RegistroManual', '{situacao}', CASE WHEN '{situacao}' = 'Verificada' THEN now() END)
            """);
        if (moralDe is not null)
        {
            await _host.ExecuteAsync(
                $"INSERT INTO reacoes_atividade (atividade_id, usuario_id, tipo) VALUES ('{id}', '{moralDe.Id}', 'Kudos')");
        }
    }

    private static string PreviaUrl(Guid grupoId, Guid destinatarioId, Guid cartaId, Guid? cartaFusaoId = null) =>
        $"/api/grupos/{grupoId}/jogadas/previa?destinatarioId={destinatarioId}&cartaId={cartaId}"
        + (cartaFusaoId is null ? "" : $"&cartaFusaoId={cartaFusaoId}");

    private static async Task<PreviaJogadaResponse> GetPreviaAsync(
        TestUsuario remetente,
        Guid grupoId,
        Guid destinatarioId,
        Guid cartaId,
        Guid? cartaFusaoId = null) =>
        (await remetente.Client.GetFromJsonAsync(
            PreviaUrl(grupoId, destinatarioId, cartaId, cartaFusaoId),
            AppJsonContext.Default.PreviaJogadaResponse,
            Token))!;

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
