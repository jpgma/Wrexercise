using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Jogo;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Jogo;

public sealed class EnfeitesRoutesTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    private static async Task<MeuAvatarResponse> GetMeuAsync(TestUsuario usuario) =>
        (await usuario.Client.GetFromJsonAsync("/api/jogo/avatar", AppJsonContext.Default.MeuAvatarResponse, Token))!;

    private static Task<HttpResponseMessage> PutVitrineAsync(TestUsuario usuario, params string[] enfeites) =>
        usuario.Client.PutAsJsonAsync(
            "/api/jogo/avatar/vitrine",
            new SalvarVitrineRequest(enfeites),
            AppJsonContext.Default.SalvarVitrineRequest,
            Token);

    private async Task<(TestUsuario Ana, Guid GrupoId)> CriarAnaComKettlebellAsync()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await JogoTestes.AtivarJogoAsync(_host, ana);
        await JogoTestes.DarEnergiaAsync(_host, ana, grupoId, 10);
        return (ana, grupoId);
    }

    [Fact]
    public async Task Anonimo_Recebe401()
    {
        var anonimo = _host.CreateAnonimo();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/jogo/avatar", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsync("/api/jogo/enfeites/vistos", null, Token)).StatusCode);
    }

    [Fact]
    public async Task MeuAvatar_EnfeiteNovoEntraNaVitrine_EContaComoNovo()
    {
        var (ana, _) = await CriarAnaComKettlebellAsync();

        var meu = await GetMeuAsync(ana);

        Assert.Equal(["kettlebell"], meu.Vitrine.Select(e => e.Codigo));
        Assert.Equal("Treinos10", meu.Vitrine[0].Conquista);
        Assert.Equal(["kettlebell"], meu.Liberados.Select(e => e.Codigo));
        Assert.Equal(["kettlebell"], meu.Novos);
        Assert.DoesNotContain(meu.Bloqueados, e => e.Codigo == "kettlebell");
        var planta = Assert.Single(meu.Bloqueados, e => e.Codigo == "planta");
        Assert.Equal(("Sequencia4", 0, 4), (planta.Conquista, planta.Atual, planta.Alvo));
    }

    [Fact]
    public async Task Vitrine_ComEnfeiteBloqueado_EInvalida()
    {
        var (ana, _) = await CriarAnaComKettlebellAsync();
        Assert.Contains((await GetMeuAsync(ana)).Bloqueados, e => e.Codigo == "trofeu");

        await ApiTestHost.AssertProblemAsync(
            await PutVitrineAsync(ana, "kettlebell", "trofeu"),
            HttpStatusCode.BadRequest,
            "VITRINE_INVALIDA");
    }

    [Fact]
    public async Task Vistos_GuardaAVitrineEOEnfeiteDeixaDeSerNovo()
    {
        var (ana, _) = await CriarAnaComKettlebellAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await ana.Client.PostAsync("/api/jogo/enfeites/vistos", null, Token)).StatusCode);

        var meu = await GetMeuAsync(ana);
        Assert.Equal(["kettlebell"], meu.Vitrine.Select(e => e.Codigo));
        Assert.Empty(meu.Novos);
    }

    [Fact]
    public async Task Vitrine_EsvaziarEPermitido_EOEnfeiteNaoVolta()
    {
        var (ana, _) = await CriarAnaComKettlebellAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await PutVitrineAsync(ana)).StatusCode);

        var meu = await GetMeuAsync(ana);
        Assert.Empty(meu.Vitrine);
        Assert.Empty(meu.Novos);
        Assert.Single(meu.Liberados);
    }

    [Theory]
    [InlineData("golzinho")]
    [InlineData("kettlebell", "kettlebell")]
    [InlineData("inexistente")]
    public async Task Vitrine_Invalida_Recebe400(params string[] enfeites)
    {
        var (ana, _) = await CriarAnaComKettlebellAsync();

        var response = await PutVitrineAsync(ana, enfeites);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "VITRINE_INVALIDA");
    }

    [Fact]
    public async Task Mapa_MostraAVitrineSoDeQuemEstaNoModoJogo()
    {
        var (ana, grupoId) = await CriarAnaComKettlebellAsync();
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await JogoTestes.AtivarJogoAsync(_host, bia);
        await JogoTestes.DarEnergiaAsync(_host, bia, grupoId, 10);

        var mapa = (await bia.Client.GetFromJsonAsync(
            "/api/jogo/avatares", AppJsonContext.Default.AvataresResponse, Token))!.Avatares
            .ToDictionary(a => a.UsuarioId);
        Assert.Equal(["kettlebell"], mapa[ana.Id].Vitrine.Select(e => e.Codigo));

        await _host.ExecuteAsync($"UPDATE usuarios SET modo_inicio = 'App' WHERE id = '{ana.Id}'");

        var depois = (await bia.Client.GetFromJsonAsync(
            "/api/jogo/avatares", AppJsonContext.Default.AvataresResponse, Token))!.Avatares;
        Assert.DoesNotContain(depois, a => a.UsuarioId == ana.Id);
        Assert.Contains(depois, a => a.UsuarioId == bia.Id);
    }

    [Fact]
    public async Task ConquistasNovas_ComemoramOEnfeiteNovoEDepoisAEvolucao()
    {
        var (ana, grupoId) = await CriarAnaComKettlebellAsync();

        var nova = Assert.Single((await GetMeuAsync(ana)).ConquistasNovas);
        Assert.Equal(
            new ConquistaNovaResponse("Treinos10", "10 treinos", "kettlebell", "Kettlebell de bronze"),
            nova);

        (await ana.Client.PostAsync("/api/jogo/enfeites/vistos", null, Token)).EnsureSuccessStatusCode();
        Assert.Empty((await GetMeuAsync(ana)).ConquistasNovas);

        // Mais 40 treinos: o kettlebell vira de prata. O Enfeite não é novo, mas a conquista é.
        // (Tantos treinos seguidos liberam também outros Enfeites, que não interessam aqui.)
        await JogoTestes.DarEnergiaAsync(_host, ana, grupoId, 40);

        var meu = await GetMeuAsync(ana);
        Assert.DoesNotContain("kettlebell", meu.Novos);
        Assert.Equal("Treinos50", meu.ConquistasNovas.Single(c => c.Enfeite == "kettlebell").Conquista);

        (await ana.Client.PostAsync("/api/jogo/enfeites/vistos", null, Token)).EnsureSuccessStatusCode();
        Assert.Empty((await GetMeuAsync(ana)).ConquistasNovas);
    }

    [Fact]
    public async Task ConquistasNovas_EnfeiteVistoAntesDosNiveis_NaoComemora()
    {
        var (ana, _) = await CriarAnaComKettlebellAsync();
        await _host.ExecuteAsync(
            $"INSERT INTO avatares (usuario_id, vitrine, enfeites_vistos) VALUES ('{ana.Id}', '{{kettlebell}}', '{{kettlebell}}')");

        Assert.Empty((await GetMeuAsync(ana)).ConquistasNovas);
    }

    [Fact]
    public async Task Bloqueados_DizemOQueFalta()
    {
        var (ana, _) = await CriarAnaComKettlebellAsync();

        var galo = (await GetMeuAsync(ana)).Bloqueados.Single(e => e.Codigo == "galo");

        Assert.Equal("Faltam 10 treinos antes das 7h", galo.Falta);
    }
}
