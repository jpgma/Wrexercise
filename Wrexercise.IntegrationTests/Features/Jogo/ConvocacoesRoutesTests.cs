using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Jogo;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Jogo;

/// <summary>As cartas Globais e a Convocação do TODO MUNDO (decisão 0091).</summary>
public sealed class ConvocacoesRoutesTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Comprar_CustaCentoECinquentaERespeitaOSaldo()
    {
        var (ana, bia, _) = await CriarGrupoAsync();
        var antes = (await JogoTestes.GetLojaAsync(ana)).Saldo;

        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.TodoMundo);

        Assert.Equal(TipoCarta.Global, carta.Tipo);
        Assert.Equal(antes - 150, (await JogoTestes.GetLojaAsync(ana)).Saldo);
        await ApiTestHost.AssertProblemAsync(
            await JogoTestes.PostCompraAsync(bia, CodigoCarta.TodoMundo),
            HttpStatusCode.Conflict,
            "SALDO_INSUFICIENTE");
    }

    [Fact]
    public async Task Convocar_GastaACartaSemCobrarDeNovoEConvidaQuemEstaNoJogo()
    {
        var (ana, bia, grupoId) = await CriarGrupoAsync();
        var carol = await _host.AddMembroAsync(grupoId, nome: "Carol"); // no modo App
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.TodoMundo);
        var saldo = (await JogoTestes.GetLojaAsync(ana)).Saldo;

        var previa = await GetPreviaAsync(ana, grupoId, carta.Id);
        Assert.Null(previa.Bloqueio);
        Assert.Equal(1, previa.Convidados);
        Assert.Equal(150, previa.DevolucaoPorPessoa);
        Assert.DoesNotContain(TipoAtividade.Outro, previa.Tipos);
        Assert.DoesNotContain(TipoAtividade.Esporte, previa.Tipos);

        var convocacao = await ConvocarAsync(ana, grupoId, carta.Id);

        Assert.Equal(saldo, (await JogoTestes.GetLojaAsync(ana)).Saldo);
        Assert.Empty((await JogoTestes.GetLojaAsync(ana)).Mao);
        Assert.Equal(SituacaoConvocacao.Ativa, convocacao.Situacao);
        Assert.Equal(TimeSpan.FromDays(7), convocacao.ExpiraEm - convocacao.CriadaEm);
        Assert.Equal(
            new[] { ana.Id, bia.Id }.Order(),
            convocacao.Participantes.Select(p => p.UsuarioId).Order());
        Assert.Equal(SituacaoConvocacao.Participando, convocacao.MinhaSituacao);
        Assert.DoesNotContain(convocacao.Participantes, p => p.UsuarioId == carol.Id);

        // Quem entra depois fica de fora.
        await JogoTestes.AtivarJogoAsync(_host, carol);
        var detalhe = await GetConvocacaoAsync(carol, grupoId, convocacao.Id);
        Assert.Null(detalhe.MinhaSituacao);
        await ApiTestHost.AssertProblemAsync(
            await PutParticipacaoAsync(carol, grupoId, convocacao.Id, "Aceita"),
            HttpStatusCode.NotFound,
            "CONVOCACAO_NAO_ENCONTRADA");

        Assert.Equal(1, await _host.CountAsync(
            $"SELECT count(*) FROM notificacoes WHERE convocacao_id = '{convocacao.Id}' AND usuario_id = '{bia.Id}'"));
        Assert.Equal(0, await _host.CountAsync(
            $"SELECT count(*) FROM notificacoes WHERE convocacao_id = '{convocacao.Id}' AND usuario_id = '{ana.Id}'"));
    }

    [Fact]
    public async Task Convocar_RecusaPedidoInvalidoSemGastarACarta()
    {
        var (ana, bia, grupoId) = await CriarGrupoAsync();
        var outroGrupoId = await _host.CreateGrupoAsync(bia, "Outro");
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.TodoMundo);

        await ApiTestHost.AssertProblemAsync(
            await PostConvocarAsync(ana, outroGrupoId, carta.Id),
            HttpStatusCode.NotFound,
            "GRUPO_NAO_ENCONTRADO");
        foreach (var tipo in new[] { TipoAtividade.Outro, TipoAtividade.Esporte, "Xadrez" })
        {
            await ApiTestHost.AssertProblemAsync(
                await PostConvocarAsync(ana, grupoId, carta.Id, tipo),
                HttpStatusCode.BadRequest,
                "DADOS_INVALIDOS");
        }

        await ApiTestHost.AssertProblemAsync(
            await PostConvocarAsync(bia, grupoId, carta.Id),
            HttpStatusCode.NotFound,
            "CARTA_NAO_ENCONTRADA");

        // A carta Global não vira Provocação.
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.PostAsJsonAsync(
                $"/api/grupos/{grupoId}/jogadas",
                new JogarCartaRequest(carta.Id, bia.Id),
                AppJsonContext.Default.JogarCartaRequest,
                Token),
            HttpStatusCode.BadRequest,
            "CARTA_INVALIDA");

        Assert.Single((await JogoTestes.GetLojaAsync(ana)).Mao);
    }

    [Fact]
    public async Task Convocar_SemNinguemNoJogoOuComFadiga_EBloqueado()
    {
        var (ana, bia, grupoId) = await CriarGrupoAsync();
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.TodoMundo);

        await _host.ExecuteAsync($"UPDATE usuarios SET modo_inicio = 'App' WHERE id = '{bia.Id}'");
        Assert.Equal("CONVOCACAO_SEM_CONVIDADOS", (await GetPreviaAsync(ana, grupoId, carta.Id)).Bloqueio);
        await ApiTestHost.AssertProblemAsync(
            await PostConvocarAsync(ana, grupoId, carta.Id),
            HttpStatusCode.Conflict,
            "CONVOCACAO_SEM_CONVIDADOS");

        // A FADIGA de quem recusou uma Provocação impede convocar.
        await JogoTestes.AtivarJogoAsync(_host, bia);
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 6);
        var provocacao = await JogoTestes.ComprarAsync(bia, CodigoCarta.Variedade);
        var jogada = await bia.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/jogadas",
            new JogarCartaRequest(provocacao.Id, ana.Id),
            AppJsonContext.Default.JogarCartaRequest,
            Token);
        Assert.Equal(HttpStatusCode.Created, jogada.StatusCode);
        var jogadaId = (await jogada.Content.ReadFromJsonAsync(AppJsonContext.Default.JogadaCriadaResponse, Token))!.Id;
        Assert.Equal(HttpStatusCode.NoContent, (await ana.Client.PutAsJsonAsync(
            $"/api/jogadas/{jogadaId}/resposta",
            new ResponderJogadaRequest("Recusada"),
            AppJsonContext.Default.ResponderJogadaRequest,
            Token)).StatusCode);

        Assert.Equal("FADIGA_ATIVA", (await GetPreviaAsync(ana, grupoId, carta.Id)).Bloqueio);
        await ApiTestHost.AssertProblemAsync(
            await PostConvocarAsync(ana, grupoId, carta.Id),
            HttpStatusCode.Conflict,
            "FADIGA_ATIVA");
        Assert.Single((await JogoTestes.GetLojaAsync(ana)).Mao, c => c.Codigo == CodigoCarta.TodoMundo);
    }

    [Fact]
    public async Task Convocar_AoMesmoTempo_CriaUmaSo()
    {
        var (ana, _, grupoId) = await CriarGrupoAsync(energia: 32);
        var primeira = await JogoTestes.ComprarAsync(ana, CodigoCarta.TodoMundo);
        var segunda = await JogoTestes.ComprarAsync(ana, CodigoCarta.TodoMundo);

        var respostas = await Task.WhenAll(
            PostConvocarAsync(ana, grupoId, primeira.Id),
            PostConvocarAsync(ana, grupoId, segunda.Id));

        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Created);
        await ApiTestHost.AssertProblemAsync(
            Assert.Single(respostas, r => r.StatusCode != HttpStatusCode.Created),
            HttpStatusCode.Conflict,
            "CONVOCACAO_ATIVA");
        Assert.Equal(1, await _host.CountAsync($"SELECT count(*) FROM convocacoes WHERE grupo_id = '{grupoId}'"));
        Assert.Single((await JogoTestes.GetLojaAsync(ana)).Mao);

        // Com o prazo vencido, mesmo esperando verificação, outra Convocação pode começar.
        await _host.ExecuteAsync($"""
            UPDATE convocacoes SET created_at = now() - interval '8 days', expira_em = now() - interval '1 day'
            WHERE grupo_id = '{grupoId}'
            """);
        var restante = Assert.Single((await JogoTestes.GetLojaAsync(ana)).Mao);
        Assert.Equal(HttpStatusCode.Created, (await PostConvocarAsync(ana, grupoId, restante.Id)).StatusCode);
    }

    [Fact]
    public async Task Participacao_RecusarESairSaoFinaisEDaoFadiga()
    {
        var (ana, bia, grupoId) = await CriarGrupoAsync();
        var carol = await _host.AddMembroAsync(grupoId, nome: "Carol");
        await JogoTestes.AtivarJogoAsync(_host, carol);
        var convocacao = await ConvocarAsync(ana, grupoId, (await JogoTestes.ComprarAsync(ana, CodigoCarta.TodoMundo)).Id);
        var energiaAna = (await GetEnergiaAsync(ana)).Saldo;

        await ApiTestHost.AssertProblemAsync(
            await PutParticipacaoAsync(ana, grupoId, convocacao.Id, "Desistiu"),
            HttpStatusCode.Conflict,
            "DECISAO_INVALIDA");

        var recusa = await PutParticipacaoAsync(bia, grupoId, convocacao.Id, "Recusada");
        Assert.Equal(HttpStatusCode.OK, recusa.StatusCode);
        await ApiTestHost.AssertProblemAsync(
            await PutParticipacaoAsync(bia, grupoId, convocacao.Id, "Aceita"),
            HttpStatusCode.Conflict,
            "DECISAO_INVALIDA");
        Assert.Equal(Fadiga.Cargas, (await GetEnergiaAsync(bia)).FadigaRestante);
        // Dois convidados: cada resultado final devolve metade do preço.
        Assert.Equal(energiaAna + 75, (await GetEnergiaAsync(ana)).Saldo);

        var aceite = await PutParticipacaoAsync(carol, grupoId, convocacao.Id, "Aceita");
        Assert.Equal(SituacaoConvocacao.Participando,
            (await aceite.Content.ReadFromJsonAsync(AppJsonContext.Default.ConvocacaoResponse, Token))!.MinhaSituacao);
        Assert.Equal(0, (await GetEnergiaAsync(carol)).FadigaRestante);

        var saida = await PutParticipacaoAsync(carol, grupoId, convocacao.Id, "Desistiu");
        Assert.Equal(HttpStatusCode.OK, saida.StatusCode);
        Assert.Equal(Fadiga.CargasFalha, (await GetEnergiaAsync(carol)).FadigaRestante);
        Assert.Equal(energiaAna + 150, (await GetEnergiaAsync(ana)).Saldo);
        Assert.Equal(0, await _host.CountAsync(
            $"SELECT count(*) FROM notificacoes WHERE convocacao_id = '{convocacao.Id}' AND read_at IS NULL"));
    }

    [Fact]
    public async Task Concluir_TreinoVerificadoDoTipoRendeTrintaEDevolveAoAnfitriao()
    {
        var (ana, bia, grupoId) = await CriarGrupoAsync();
        var convocacao = await ConvocarAsync(ana, grupoId, (await JogoTestes.ComprarAsync(ana, CodigoCarta.TodoMundo)).Id);
        var energiaAna = (await GetEnergiaAsync(ana)).Detalhamento;
        var energiaBia = (await GetEnergiaAsync(bia)).Detalhamento;

        // Com o convite sem resposta, o treino continua liberado, e o de outro tipo não conta nem faz falhar.
        Assert.Equal(HttpStatusCode.Created, (await PostAtividadeAsync(bia, grupoId, TipoAtividade.Corrida)).StatusCode);
        await ApiTestHost.AvaliarFilaAsync(ana, grupoId);
        Assert.Equal(HttpStatusCode.Created, (await PostAtividadeAsync(bia, grupoId, TipoAtividade.Ciclismo)).StatusCode);
        Assert.Equal(SituacaoConvocacao.Pendente, (await GetConvocacaoAsync(bia, grupoId, convocacao.Id)).MinhaSituacao);

        // Aceita depois do treino: ele conta (como na decisão 0076), mas só verificado.
        var aceite = await PutParticipacaoAsync(bia, grupoId, convocacao.Id, "Aceita");
        Assert.Equal(SituacaoConvocacao.AguardandoVerificacao,
            (await aceite.Content.ReadFromJsonAsync(AppJsonContext.Default.ConvocacaoResponse, Token))!.MinhaSituacao);
        Assert.Equal(0, (await GetEnergiaAsync(bia)).Detalhamento.Convocacoes);
        await ApiTestHost.AssertProblemAsync(
            await PutParticipacaoAsync(bia, grupoId, convocacao.Id, "Desistiu"),
            HttpStatusCode.Conflict,
            "TREINO_JA_CONTA");

        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'Verificada', verified_at = now()
            WHERE usuario_id = '{bia.Id}' AND grupo_id = '{grupoId}'
            """);

        var bia2 = await GetConvocacaoAsync(bia, grupoId, convocacao.Id);
        Assert.Equal(SituacaoConvocacao.Concluida, bia2.MinhaSituacao);
        Assert.Equal(30, bia2.MinhaRecompensa);
        Assert.Equal(1, bia2.Concluidas);
        Assert.Null(bia2.Devolucao);
        Assert.Equal(30, (await GetEnergiaAsync(bia)).Detalhamento.Convocacoes - energiaBia.Convocacoes);
        Assert.Equal(150, (await GetEnergiaAsync(ana)).Detalhamento.Convocacoes - energiaAna.Convocacoes);
        Assert.Equal(150, (await GetConvocacaoAsync(ana, grupoId, convocacao.Id)).Devolucao);

        // Ler de novo não duplica nada.
        Assert.Equal(30, (await GetEnergiaAsync(bia)).Detalhamento.Convocacoes);
    }

    [Fact]
    public async Task Treino_ForaDaJanelaOuDeOutroGrupo_NaoConta()
    {
        var (ana, bia, grupoId) = await CriarGrupoAsync();
        var outroGrupoId = await _host.CreateGrupoAsync(bia, "Outro");
        var convocacao = await ConvocarAsync(ana, grupoId, (await JogoTestes.ComprarAsync(ana, CodigoCarta.TodoMundo)).Id);
        Assert.Equal(HttpStatusCode.OK, (await PutParticipacaoAsync(bia, grupoId, convocacao.Id, "Aceita")).StatusCode);

        // Antes da criação, exatamente no prazo e em outro grupo.
        await InserirTreinoAsync(bia, grupoId, "(SELECT created_at - interval '1 minute' FROM convocacoes)");
        await InserirTreinoAsync(bia, grupoId, "(SELECT expira_em FROM convocacoes)");
        await InserirTreinoAsync(bia, outroGrupoId, "now()");

        Assert.Equal(SituacaoConvocacao.Participando, (await GetConvocacaoAsync(bia, grupoId, convocacao.Id)).MinhaSituacao);

        // Com o prazo vencido e sem treino que conte, falhou.
        await _host.ExecuteAsync($"""
            UPDATE convocacoes SET created_at = created_at - interval '8 days', expira_em = expira_em - interval '8 days'
            WHERE id = '{convocacao.Id}'
            """);
        var encerrada = await GetConvocacaoAsync(bia, grupoId, convocacao.Id);
        Assert.Equal(SituacaoConvocacao.Encerrada, encerrada.Situacao);
        Assert.Equal(SituacaoConvocacao.Falhou, encerrada.MinhaSituacao);

        var lista = await GetConvocacoesAsync(ana, grupoId);
        Assert.Equal(convocacao.Id, lista.Destaque!.Id);
        Assert.Single(lista.Recentes);
    }

    [Fact]
    public async Task Listar_SoMembroDoGrupo()
    {
        var (ana, _, grupoId) = await CriarGrupoAsync();
        var estranha = await _host.CreateUsuarioAsync("Estranha");

        Assert.Null((await GetConvocacoesAsync(ana, grupoId)).Destaque);
        await ApiTestHost.AssertProblemAsync(
            await estranha.Client.GetAsync($"/api/grupos/{grupoId}/convocacoes", Token),
            HttpStatusCode.NotFound,
            "GRUPO_NAO_ENCONTRADO");
    }

    /// <summary>Ana (com Energia para uma carta TODO MUNDO por 16 atividades) e Bia, as duas no modo Jogo.</summary>
    private async Task<(TestUsuario Ana, TestUsuario Bia, Guid GrupoId)> CriarGrupoAsync(int energia = 16)
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await JogoTestes.AtivarJogoAsync(_host, ana);
        await JogoTestes.AtivarJogoAsync(_host, bia);
        await JogoTestes.DarEnergiaAsync(_host, ana, grupoId, energia);
        return (ana, bia, grupoId);
    }

    private Task InserirTreinoAsync(TestUsuario usuario, Guid grupoId, string inicioSql) =>
        _host.ExecuteAsync($"""
            INSERT INTO atividades (
                grupo_id, usuario_id, tipo, duracao_segundos, started_at, tipo_comprovacao,
                situacao_verificacao, verified_at)
            VALUES ('{grupoId}', '{usuario.Id}', 'Ciclismo', 1800, {inicioSql}, 'RegistroManual', 'Verificada', now())
            """);

    private static Task<HttpResponseMessage> PostConvocarAsync(
        TestUsuario usuario, Guid grupoId, Guid cartaId, string tipo = TipoAtividade.Ciclismo) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/convocacoes",
            new ConvocarRequest(cartaId, tipo),
            AppJsonContext.Default.ConvocarRequest,
            Token);

    private static async Task<ConvocacaoResponse> ConvocarAsync(TestUsuario usuario, Guid grupoId, Guid cartaId)
    {
        var response = await PostConvocarAsync(usuario, grupoId, cartaId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ConvocacaoResponse, Token))!;
    }

    private static async Task<PreviaConvocacaoResponse> GetPreviaAsync(TestUsuario usuario, Guid grupoId, Guid cartaId) =>
        (await usuario.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/convocacoes/previa?cartaId={cartaId}",
            AppJsonContext.Default.PreviaConvocacaoResponse,
            Token))!;

    private static async Task<ConvocacaoResponse> GetConvocacaoAsync(TestUsuario usuario, Guid grupoId, Guid id) =>
        (await usuario.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/convocacoes/{id}", AppJsonContext.Default.ConvocacaoResponse, Token))!;

    private static async Task<ConvocacoesResponse> GetConvocacoesAsync(TestUsuario usuario, Guid grupoId) =>
        (await usuario.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/convocacoes", AppJsonContext.Default.ConvocacoesResponse, Token))!;

    private static Task<HttpResponseMessage> PutParticipacaoAsync(
        TestUsuario usuario, Guid grupoId, Guid id, string decisao) =>
        usuario.Client.PutAsJsonAsync(
            $"/api/grupos/{grupoId}/convocacoes/{id}/participacao",
            new ResponderConvocacaoRequest(decisao),
            AppJsonContext.Default.ResponderConvocacaoRequest,
            Token);

    private static async Task<EnergiaResponse> GetEnergiaAsync(TestUsuario usuario) =>
        (await usuario.Client.GetFromJsonAsync("/api/jogo/energia", AppJsonContext.Default.EnergiaResponse, Token))!;

    private static Task<HttpResponseMessage> PostAtividadeAsync(TestUsuario usuario, Guid grupoId, string tipo) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(tipo, 3600, DateTimeOffset.UtcNow, "Treino", TipoComprovacao.RegistroManual),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
}
