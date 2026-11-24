using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Jogo;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Jogo;

public sealed class FadigaRoutesTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Recusar_DaFadigaSoAQuemRecebeuEUmaVezSo()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var jogadaId = await JogarAsync(ana, grupoId, bia);

        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Recusada")).StatusCode);
        await ApiTestHost.AssertProblemAsync(
            await PutRespostaAsync(bia, jogadaId, "Recusada"),
            HttpStatusCode.Conflict,
            "JOGADA_ENCERRADA");

        Assert.Equal(Fadiga.Cargas, (await GetEnergiaAsync(bia)).FadigaRestante);
        Assert.Equal(0, (await GetEnergiaAsync(ana)).FadigaRestante);
        // Quem enviou perde a carta (decisão 0074).
        Assert.Empty((await JogoTestes.GetLojaAsync(ana)).Mao);
    }

    [Fact]
    public async Task ExpirarSemResposta_DaFadiga()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var jogadaId = await JogarAsync(ana, grupoId, bia);
        Assert.Equal(0, (await GetEnergiaAsync(bia)).FadigaRestante);

        await _host.ExecuteAsync($"""
            UPDATE jogadas SET enviada_em = now() - interval '2 hours', expira_em = now() - interval '1 minute'
            WHERE id = '{jogadaId}'
            """);

        Assert.Equal(Fadiga.Cargas, (await GetEnergiaAsync(bia)).FadigaRestante);
    }

    [Fact]
    public async Task AceitarEFalhar_DaFadigaMaisLeve()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var jogadaId = await JogarAsync(ana, grupoId, bia);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Aceita")).StatusCode);
        Assert.Equal(0, (await GetEnergiaAsync(bia)).FadigaRestante);

        await _host.ExecuteAsync($"""
            UPDATE jogadas
            SET enviada_em = now() - interval '2 hours', respondida_em = now() - interval '1 hour',
                expira_em = now() - interval '1 minute'
            WHERE id = '{jogadaId}'
            """);

        // Decisão 0089: quem tentou e não cumpriu fica com 2 cargas, não 3.
        Assert.Equal(Fadiga.CargasFalha, (await GetEnergiaAsync(bia)).FadigaRestante);
    }

    [Fact]
    public async Task TresDiasVerificados_RendemMetadeEOQuartoRendeNormal()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var jogadaId = await JogarAsync(ana, grupoId, bia);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Recusada")).StatusCode);

        // Dois treinos no mesmo dia gastam uma carga só.
        await InserirDiaVerificadoAsync(bia, grupoId, dias: 3, minutos: 1);
        await InserirDiaVerificadoAsync(bia, grupoId, dias: 3, minutos: 2);
        var depoisDoPrimeiro = await GetEnergiaAsync(bia);
        Assert.Equal(2, depoisDoPrimeiro.FadigaRestante);
        Assert.Equal(-Fadiga.Desconto, depoisDoPrimeiro.Detalhamento.Fadiga);

        await InserirDiaVerificadoAsync(bia, grupoId, dias: 2, minutos: 3);
        await InserirDiaVerificadoAsync(bia, grupoId, dias: 1, minutos: 4);
        await InserirDiaVerificadoAsync(bia, grupoId, dias: 0, minutos: 5);

        var energia = await GetEnergiaAsync(bia);
        Assert.Equal(0, energia.FadigaRestante);
        Assert.Equal(4 * CalculoEnergia.PorDia, energia.Detalhamento.Atividades);
        Assert.Equal(-3 * Fadiga.Desconto, energia.Detalhamento.Fadiga);
    }

    [Fact]
    public async Task SegundaRecusaComFadigaAtiva_SomaUmaCarga()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var cris = await _host.AddMembroAsync(grupoId, nome: "Cris");
        await JogoTestes.AtivarJogoAsync(_host, cris);
        await JogoTestes.DarEnergiaAsync(_host, cris, grupoId, 6);
        var primeira = await JogarAsync(ana, grupoId, bia);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, primeira, "Recusada")).StatusCode);
        await InserirDiaVerificadoAsync(bia, grupoId, dias: 1, minutos: 1);

        var segunda = await JogarAsync(cris, grupoId, bia);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, segunda, "Recusada")).StatusCode);

        // Decisão 0089: 3 − 1 treino + 1 da segunda recusa.
        Assert.Equal(Fadiga.Cargas, (await GetEnergiaAsync(bia)).FadigaRestante);
    }

    [Fact]
    public async Task ComFadiga_NaoJogaProvocacao_MasOGrupoVeABateria()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 6);
        var jogadaId = await JogarAsync(ana, grupoId, bia);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Recusada")).StatusCode);

        var carta = await JogoTestes.ComprarAsync(bia, CodigoCarta.Variedade);
        await ApiTestHost.AssertProblemAsync(
            await bia.Client.PostAsJsonAsync(
                $"/api/grupos/{grupoId}/jogadas",
                new JogarCartaRequest(carta.Id, ana.Id),
                AppJsonContext.Default.JogarCartaRequest,
                Token),
            HttpStatusCode.Conflict,
            "FADIGA_ATIVA");

        var cena = (await ana.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/cena", AppJsonContext.Default.CenaResponse, Token))!;
        var fadiga = Assert.Single(cena.Membros, m => m.UsuarioId == bia.Id).Fadiga;
        Assert.Equal(new FadigaMembroResponse(Fadiga.Cargas, "Recusa", "VARIEDADE", "Ana"), fadiga);
        Assert.Null(Assert.Single(cena.Membros, m => m.UsuarioId == ana.Id).Fadiga);
    }

    [Fact]
    public async Task Recusa_LiberaUmaRevancheSemEsperar24Horas()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var primeira = await JogarAsync(ana, grupoId, bia);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, primeira, "Recusada")).StatusCode);

        // A Revanche: Ana manda outra carta logo depois da recusa.
        var revanche = await JogarAsync(ana, grupoId, bia);

        // Uma vez só: a carta da Revanche passa a ser a última e, sem nova recusa, o limite de 24 horas volta.
        await _host.ExecuteAsync($"""
            UPDATE jogadas SET enviada_em = now() - interval '1 hour' WHERE id = '{primeira}';
            UPDATE jogadas SET enviada_em = now() - interval '30 minutes', expira_em = now() - interval '1 second'
            WHERE id = '{revanche}';
            """);
        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.PostAsJsonAsync(
                $"/api/grupos/{grupoId}/jogadas",
                new JogarCartaRequest(carta.Id, bia.Id),
                AppJsonContext.Default.JogarCartaRequest,
                Token),
            HttpStatusCode.Conflict,
            "JOGADA_RECENTE");
    }

    [Fact]
    public async Task CumprirUmaCarta_TiraUmaCarga()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var primeira = await JogarAsync(ana, grupoId, bia);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, primeira, "Recusada")).StatusCode);
        var segunda = await JogarAsync(ana, grupoId, bia);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, segunda, "Aceita")).StatusCode);

        // O próximo treino, verificado, cumpre a VARIEDADE (sem treino anterior, qualquer tipo vale): gasta uma
        // carga pelo dia e tira outra pela carta cumprida.
        await _host.ExecuteAsync($"""
            INSERT INTO atividades (
                grupo_id, usuario_id, tipo, duracao_segundos, started_at, tipo_comprovacao,
                situacao_verificacao, verified_at, created_at)
            VALUES ('{grupoId}', '{bia.Id}', 'Outro', 1800, now() + interval '1 minute', 'RegistroManual',
                'Verificada', now() + interval '2 minutes', now())
            """);

        var jogadas = (await bia.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/jogadas", AppJsonContext.Default.JogadasResponse, Token))!;
        Assert.Equal(SituacaoJogada.Concluida, jogadas.Jogadas.Single(j => j.Id == segunda).Situacao);
        Assert.Equal(Fadiga.Cargas - 2, (await GetEnergiaAsync(bia)).FadigaRestante);
    }

    [Fact]
    public async Task DiaAnulado_GastaACargaSemTirarEnergiaDeOutroDia()
    {
        var (ana, bia, grupoId) = await CriarParAsync();
        var jogadaId = await JogarAsync(ana, grupoId, bia);
        Assert.Equal(HttpStatusCode.NoContent, (await PutRespostaAsync(bia, jogadaId, "Recusada")).StatusCode);
        await InserirDiaVerificadoAsync(bia, grupoId, dias: 1, minutos: 1);
        await InserirDiaVerificadoAsync(bia, grupoId, dias: 0, minutos: 2);

        await _host.ExecuteAsync($"""
            UPDATE atividades SET situacao_verificacao = 'Anulada'
            WHERE usuario_id = '{bia.Id}' AND verified_at = (
                SELECT min(verified_at) FROM atividades WHERE usuario_id = '{bia.Id}' AND verified_at > now())
            """);

        var energia = await GetEnergiaAsync(bia);
        Assert.Equal(1, energia.FadigaRestante);
        Assert.Equal(-Fadiga.Desconto, energia.Detalhamento.Fadiga);
    }

    /// <summary>Ana e Bia no modo Jogo, num grupo só delas; Ana com Energia para algumas cartas.</summary>
    private async Task<(TestUsuario Ana, TestUsuario Bia, Guid GrupoId)> CriarParAsync()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await JogoTestes.AtivarJogoAsync(_host, ana);
        await JogoTestes.AtivarJogoAsync(_host, bia);
        await JogoTestes.DarEnergiaAsync(_host, ana, grupoId, 6);
        return (ana, bia, grupoId);
    }

    /// <summary>
    /// Uma atividade verificada <paramref name="minutos"/> depois de agora, num dia <paramref name="dias"/> antes de hoje:
    /// verificada depois de qualquer recusa feita antes, como um treino novo.
    /// </summary>
    private Task InserirDiaVerificadoAsync(TestUsuario usuario, Guid grupoId, int dias, int minutos) =>
        _host.ExecuteAsync($"""
            INSERT INTO atividades (
                grupo_id, usuario_id, tipo, duracao_segundos, started_at, tipo_comprovacao,
                situacao_verificacao, verified_at, created_at)
            VALUES ('{grupoId}', '{usuario.Id}', 'Outro', 1800,
                date_trunc('day', now() AT TIME ZONE 'America/Sao_Paulo') AT TIME ZONE 'America/Sao_Paulo'
                    - interval '{dias} days' + interval '12 hours' + interval '{minutos} minutes',
                'RegistroManual', 'Verificada', now() + interval '{minutos} minutes', now())
            """);

    private static async Task<Guid> JogarAsync(TestUsuario remetente, Guid grupoId, TestUsuario destinatario)
    {
        var carta = await JogoTestes.ComprarAsync(remetente, CodigoCarta.Variedade);
        var response = await remetente.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/jogadas",
            new JogarCartaRequest(carta.Id, destinatario.Id),
            AppJsonContext.Default.JogarCartaRequest,
            Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.JogadaCriadaResponse, Token))!.Id;
    }

    private static Task<HttpResponseMessage> PutRespostaAsync(TestUsuario usuario, Guid jogadaId, string resposta) =>
        usuario.Client.PutAsJsonAsync(
            $"/api/jogadas/{jogadaId}/resposta",
            new ResponderJogadaRequest(resposta, null, null),
            AppJsonContext.Default.ResponderJogadaRequest,
            Token);

    private static async Task<EnergiaResponse> GetEnergiaAsync(TestUsuario usuario) =>
        (await usuario.Client.GetFromJsonAsync("/api/jogo/energia", AppJsonContext.Default.EnergiaResponse, Token))!;
}
