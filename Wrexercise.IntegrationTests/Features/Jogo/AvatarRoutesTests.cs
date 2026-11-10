using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Jogo;
using Wrexercise.Infrastructure.Json;
using Wrexercise.UnitTests.Infrastructure.Files;

namespace Wrexercise.IntegrationTests.Features.Jogo;

public sealed class AvatarRoutesTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    private static Task<HttpResponseMessage> EnviarAsync(TestUsuario usuario, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(bytes);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(arquivo, "arquivo", "folha.png");
        return usuario.Client.PutAsync("/api/jogo/avatar/folha", content, Token);
    }

    private static async Task<string> EnviarComSucessoAsync(TestUsuario usuario)
    {
        var response = await EnviarAsync(usuario, PngFolha.Criar());
        response.EnsureSuccessStatusCode();
        var enviada = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.FolhaEnviadaResponse, Token);
        return enviada!.Folha;
    }

    private static async Task<AvataresResponse> GetAvataresAsync(TestUsuario usuario) =>
        (await usuario.Client.GetFromJsonAsync("/api/jogo/avatares", AppJsonContext.Default.AvataresResponse, Token))!;

    private int ArquivosNoDisco() => Directory.GetFiles(_host.AvataresDiretorio).Length;

    [Fact]
    public async Task Anonimo_Recebe401()
    {
        var anonimo = _host.CreateAnonimo();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/jogo/avatares", Token)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonimo.GetAsync("/api/avatares/0123456789abcdef0123456789abcdef.png", Token)).StatusCode);
    }

    [Fact]
    public async Task EnviarFolha_GuardaOArquivoEEntregaAoDono()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var folha = await EnviarComSucessoAsync(ana);

        Assert.StartsWith("/api/avatares/", folha);
        Assert.Equal(1, ArquivosNoDisco());
        var download = await ana.Client.GetAsync(folha, Token);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Contains("nosniff", download.Headers.GetValues("X-Content-Type-Options"));
        Assert.Contains("immutable", download.Headers.CacheControl?.ToString());
        Assert.Equal(PngFolha.Criar().Length, (await download.Content.ReadAsByteArrayAsync(Token)).Length);
    }

    [Theory]
    [InlineData(256, 384, 6)]
    [InlineData(431, 2352, 6)]
    [InlineData(432, 2352, 2)]
    public async Task EnviarFolha_FormatoErrado_Recusa400SemGuardarNada(int largura, int altura, int tipoCor)
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var response = await EnviarAsync(ana, PngFolha.Criar(largura, altura, (byte)tipoCor));

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "FOLHA_INVALIDA");
        Assert.Equal(0, ArquivosNoDisco());
        Assert.Equal(0, await _host.CountAsync("SELECT count(*) FROM avatares"));
    }

    [Fact]
    public async Task EnviarFolha_Apng_Recusa400()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var response = await EnviarAsync(ana, PngFolha.Criar(blocoExtra: "acTL"));

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "FOLHA_INVALIDA");
    }

    [Fact]
    public async Task EnviarFolha_PaletaIndexada_GuardaSemMudar()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var png = PngFolha.Criar(tipoCor: 3);

        var response = await EnviarAsync(ana, png);

        response.EnsureSuccessStatusCode();
        var enviada = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.FolhaEnviadaResponse, Token);
        Assert.Equal(png, await ana.Client.GetByteArrayAsync(enviada!.Folha, Token));
    }

    [Fact]
    public async Task EnviarFolha_Maior1000Kb_Recusa400()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var response = await EnviarAsync(ana, PngFolha.Criar(bytesExtras: 1001 * 1024));

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "FOLHA_INVALIDA");
        Assert.Equal(0, ArquivosNoDisco());
    }

    [Fact]
    public async Task EnviarFolha_SemMultipart_Recusa400()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var response = await ana.Client.PutAsync("/api/jogo/avatar/folha", new StringContent("{}"), Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task EnviarOutraFolha_ApagaAAntiga()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var primeira = await EnviarComSucessoAsync(ana);

        var segunda = await EnviarComSucessoAsync(ana);

        Assert.NotEqual(primeira, segunda);
        Assert.Equal(1, ArquivosNoDisco());
        Assert.Equal(HttpStatusCode.NotFound, (await ana.Client.GetAsync(primeira, Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ana.Client.GetAsync(segunda, Token)).StatusCode);
    }

    [Fact]
    public async Task VoltarAoPadrao_ApagaOArquivoELimpaOMapa()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        await EnviarComSucessoAsync(ana);

        var response = await ana.Client.DeleteAsync("/api/jogo/avatar/folha", Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, ArquivosNoDisco());
        Assert.Empty((await GetAvataresAsync(ana)).Avatares);
    }

    [Fact]
    public async Task VoltarAoPadrao_SemFolha_NaoFalha()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var response = await ana.Client.DeleteAsync("/api/jogo/avatar/folha", Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Download_SoParaQuemDivideUmGrupo()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var forasteiro = await _host.CreateUsuarioAsync("Caio");
        var folha = await EnviarComSucessoAsync(ana);

        Assert.Equal(HttpStatusCode.OK, (await bia.Client.GetAsync(folha, Token)).StatusCode);
        await ApiTestHost.AssertProblemAsync(
            await forasteiro.Client.GetAsync(folha, Token),
            HttpStatusCode.NotFound,
            "AVATAR_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Download_QuemSaiuDoGrupo_PerdeOAcesso()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var folha = await EnviarComSucessoAsync(ana);
        await _host.ExecuteAsync(
            $"UPDATE membros_grupo SET removed_at = now() WHERE usuario_id = '{bia.Id}' AND grupo_id = '{grupoId}'");

        Assert.Equal(HttpStatusCode.NotFound, (await bia.Client.GetAsync(folha, Token)).StatusCode);
    }

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("0123456789abcdef0123456789abcdef.png")]
    public async Task Download_NomeInvalidoOuInexistente_Retorna404(string nome)
    {
        var ana = await _host.CreateUsuarioAsync("Ana");

        var response = await ana.Client.GetAsync($"/api/avatares/{nome}", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Mapa_TemOsColegasDeGrupoEMais_NaoOsForasteiros()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.CreateUsuarioAsync("Caio");
        var daAna = await EnviarComSucessoAsync(ana);
        var daBia = await EnviarComSucessoAsync(bia);
        await EnviarComSucessoAsync(caio);

        var mapa = (await GetAvataresAsync(ana)).Avatares.ToDictionary(a => a.UsuarioId, a => a.Folha);

        Assert.Equal(2, mapa.Count);
        Assert.Equal(daAna, mapa[ana.Id]);
        Assert.Equal(daBia, mapa[bia.Id]);
    }

    [Fact]
    public async Task Redefinir_AdministradorVoltaOAvatarDeUmMembroAoPadrao()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var admin = await _host.AddMembroAsync(grupoId, PapelMembro.Administrador, "Admin");
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await EnviarComSucessoAsync(bia);

        var response = await admin.Client.DeleteAsync($"/api/grupos/{grupoId}/membros/{bia.Id}/avatar", Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, ArquivosNoDisco());
        Assert.Empty((await GetAvataresAsync(ana)).Avatares);
    }

    [Fact]
    public async Task Redefinir_ProprietarioPode()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await EnviarComSucessoAsync(bia);

        var response = await ana.Client.DeleteAsync($"/api/grupos/{grupoId}/membros/{bia.Id}/avatar", Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, ArquivosNoDisco());
    }

    [Fact]
    public async Task Redefinir_MembroComum_Recebe403EAFolhaFica()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");
        await EnviarComSucessoAsync(caio);

        var response = await bia.Client.DeleteAsync($"/api/grupos/{grupoId}/membros/{caio.Id}/avatar", Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Forbidden, "ACESSO_NEGADO");
        Assert.Equal(1, ArquivosNoDisco());
    }

    [Fact]
    public async Task Redefinir_QuemNaoEMembroDoGrupo_Recebe404()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var forasteiro = await _host.CreateUsuarioAsync("Caio");

        var response = await forasteiro.Client.DeleteAsync($"/api/grupos/{grupoId}/membros/{ana.Id}/avatar", Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.NotFound, "GRUPO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Redefinir_AlvoDeOutroGrupo_Recebe404EAFolhaFica()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var caio = await _host.CreateUsuarioAsync("Caio");
        await EnviarComSucessoAsync(caio);

        var response = await ana.Client.DeleteAsync($"/api/grupos/{grupoId}/membros/{caio.Id}/avatar", Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.NotFound, "MEMBRO_NAO_ENCONTRADO");
        Assert.Equal(1, ArquivosNoDisco());
    }
}
