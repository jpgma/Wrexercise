using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Jogo;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Jogo;

public sealed class LojaRoutesTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Loja_Retorna401ParaAnonimo()
    {
        var response = await _host.CreateAnonimo().GetAsync("/api/jogo/loja", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Loja_MostraProvocacoesERespostas()
    {
        var ana = await _host.CreateUsuarioAsync();

        var loja = await JogoTestes.GetLojaAsync(ana);

        Assert.Equal(9, loja.Catalogo.Count);
        Assert.Equal(5, loja.Catalogo.Count(c => c.Tipo == TipoCarta.Provocacao));
        Assert.Equal(4, loja.Catalogo.Count(c => c.Tipo == TipoCarta.Resposta));
        Assert.Equal(LimitesJogada.CartasNaMao, loja.LimiteMao);
        Assert.Empty(loja.Mao);
    }

    [Fact]
    public async Task Comprar_DesceOSaldoEPoeACartaNaMao()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        await JogoTestes.DarEnergiaAsync(_host, ana, grupoId, 10);
        var antes = (await JogoTestes.GetLojaAsync(ana)).Saldo;

        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Variedade);

        var loja = await JogoTestes.GetLojaAsync(ana);
        Assert.Equal(antes - 35, loja.Saldo);
        Assert.Equal(35, carta.Preco);
        Assert.False(Assert.Single(loja.Mao).EmJogo);
        var energia = await ana.Client.GetFromJsonAsync("/api/jogo/energia", AppJsonContext.Default.EnergiaResponse, Token);
        Assert.Equal(antes - 35, energia!.Saldo);
    }

    [Fact]
    public async Task Comprar_SemSaldo_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync();

        await ApiTestHost.AssertProblemAsync(
            await JogoTestes.PostCompraAsync(ana, CodigoCarta.Mais10Min),
            HttpStatusCode.Conflict,
            "SALDO_INSUFICIENTE");
    }

    [Fact]
    public async Task Comprar_ComAMaoCheia_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        await JogoTestes.DarEnergiaAsync(_host, ana, grupoId, 20);
        for (var i = 0; i < LimitesJogada.CartasNaMao; i++)
        {
            await JogoTestes.ComprarAsync(ana, CodigoCarta.Mais10Min);
        }

        await ApiTestHost.AssertProblemAsync(
            await JogoTestes.PostCompraAsync(ana, CodigoCarta.Mais10Min),
            HttpStatusCode.Conflict,
            "MAO_CHEIA");
    }

    [Fact]
    public async Task Comprar_Resposta_VaiParaAMao()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        await JogoTestes.DarEnergiaAsync(_host, ana, grupoId, 3);

        var carta = await JogoTestes.ComprarAsync(ana, CodigoCarta.Aposta);

        Assert.Equal(TipoCarta.Resposta, carta.Tipo);
        Assert.Equal(25, carta.Preco);
        Assert.Single((await JogoTestes.GetLojaAsync(ana)).Mao);
    }

    [Fact]
    public async Task Comprar_CartaInexistente_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync();

        await ApiTestHost.AssertProblemAsync(
            await JogoTestes.PostCompraAsync(ana, "Inexistente"),
            HttpStatusCode.BadRequest,
            "CARTA_INVALIDA");
    }
}

/// <summary>Ajudantes compartilhados pelos testes da Loja e das Jogadas.</summary>
internal static class JogoTestes
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Atividades verificadas de dois meses atrás: rendem Energia, mas não cumprem nenhuma Jogada.
    /// Cada uma rende pelo menos 10 EN.
    /// </summary>
    public static Task DarEnergiaAsync(ApiTestHost host, TestUsuario usuario, Guid grupoId, int atividades) =>
        host.ExecuteAsync($"""
            INSERT INTO atividades (
                grupo_id, usuario_id, tipo, duracao_segundos, started_at, tipo_comprovacao,
                situacao_verificacao, verified_at, created_at)
            SELECT '{grupoId}', '{usuario.Id}', 'Outro', 1800, now() - interval '60 days' - s * interval '1 hour',
                'RegistroManual', 'Verificada', now() - interval '60 days', now() - interval '60 days'
            FROM generate_series(1, {atividades}) s
            """);

    public static Task AtivarJogoAsync(ApiTestHost host, TestUsuario usuario) =>
        host.ExecuteAsync($"UPDATE usuarios SET modo_inicio = 'Jogo' WHERE id = '{usuario.Id}'");

    public static async Task<LojaResponse> GetLojaAsync(TestUsuario usuario) =>
        (await usuario.Client.GetFromJsonAsync("/api/jogo/loja", AppJsonContext.Default.LojaResponse, Token))!;

    public static Task<HttpResponseMessage> PostCompraAsync(TestUsuario usuario, string codigo) =>
        usuario.Client.PostAsJsonAsync(
            "/api/jogo/cartas",
            new ComprarCartaRequest(codigo),
            AppJsonContext.Default.ComprarCartaRequest,
            Token);

    public static async Task<CartaMaoResponse> ComprarAsync(TestUsuario usuario, string codigo)
    {
        var response = await PostCompraAsync(usuario, codigo);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.CartaMaoResponse, Token))!;
    }
}
