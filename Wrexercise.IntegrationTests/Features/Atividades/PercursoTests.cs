using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Atividades;

/// <summary>O Percurso do treino ao vivo (ADR 0084).</summary>
public sealed class PercursoTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const double MetrosPorGrau = 6_371_000 * Math.PI / 180;
    private const double LatBase = -23.55;
    private const double LonBase = -46.63;

    private static readonly byte[] JpegMinimo =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9,
    ];

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Comecar_ComPercurso_CriaOPercursoSemOPontoDeInicio()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);

        var atividade = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Corrida, true, -23.55, -46.63));
        var atual = await ana.Client.GetFromJsonAsync(
            "/api/atividades/em-andamento", AppJsonContext.Default.TreinoAtualResponse, Token);

        Assert.Null(atividade.Latitude);
        Assert.Equal(1L, await _host.CountAsync("SELECT count(*) FROM percursos"));
        Assert.Equal(new PercursoAoVivoResponse(-1, 0), atual!.Percurso);
    }

    [Theory]
    [InlineData(TipoAtividade.Musculacao, true)]
    [InlineData(TipoAtividade.Corrida, false)]
    public async Task Comecar_SemPercurso_QuandoOTipoNaoGravaOuAPessoaNaoQuer(string tipo, bool gravar)
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);

        await ComecarAsync(ana, grupoId, tipo, gravar);

        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM percursos"));
    }

    [Fact]
    public async Task Pontos_RepetirOLoteNaoDuplica_EADistanciaVemDoServidor()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Caminhada, true));
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(5));
        var lote = Reta(0, 41, 5, DateTimeOffset.UtcNow.AddSeconds(-210));

        var primeiro = await EnviarAsync(ana, treino.Id, lote);
        var repetido = await EnviarAsync(ana, treino.Id, lote);
        var resposta = await repetido.Content.ReadFromJsonAsync(AppJsonContext.Default.PontosPercursoResponse, Token);

        Assert.Equal(HttpStatusCode.OK, primeiro.StatusCode);
        Assert.Equal(40, resposta!.UltimoSeq);
        Assert.Equal(41, resposta.TotalPontos);
        Assert.InRange(resposta.DistanciaMetros, 195, 205);
    }

    [Fact]
    public async Task Pontos_SoOAutorEComOTreinoAberto()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var estranho = await _host.CreateUsuarioAsync("Estranho");
        var treino = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Corrida, true));
        var lote = Reta(0, 5, 5, DateTimeOffset.UtcNow.AddSeconds(-30));

        await ApiTestHost.AssertProblemAsync(
            await EnviarAsync(bia, treino.Id, lote), HttpStatusCode.Forbidden, "ACESSO_NEGADO");
        await ApiTestHost.AssertProblemAsync(
            await EnviarAsync(estranho, treino.Id, lote), HttpStatusCode.NotFound, "ATIVIDADE_NAO_ENCONTRADA");

        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(20));
        await TerminarAsync(ana, treino.Id, 600);
        await ApiTestHost.AssertProblemAsync(
            await EnviarAsync(ana, treino.Id, lote), HttpStatusCode.Conflict, "TREINO_NAO_EM_ANDAMENTO");
    }

    [Fact]
    public async Task Pontos_SemPercurso_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Corrida, false));

        var response = await EnviarAsync(ana, treino.Id, Reta(0, 5, 5, DateTimeOffset.UtcNow.AddSeconds(-30)));

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.NotFound, "PERCURSO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Terminar_ComDuracaoMenor_CortaOPercurso()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Caminhada, true));
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(30));
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-30);
        // 25 min andando a 1 m/s, um ponto a cada 5 s: 1,5 km.
        var pontos = Reta(0, 301, 5, inicio.AddSeconds(5));
        await EnviarAsync(ana, treino.Id, pontos[..250]);
        await EnviarAsync(ana, treino.Id, pontos[250..]);

        // Terminou dizendo 10 min: só os primeiros 600 m contam.
        var terminada = await LerAsync(await TerminarAsync(ana, treino.Id, 600));

        Assert.NotNull(terminada.Percurso);
        Assert.InRange(terminada.Percurso!.DistanciaMetros, 570, 600);
    }

    [Fact]
    public async Task Obter_OGrupoVeOPercursoAparado_OAutorVeTudo()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var treino = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Corrida, true));
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(10));
        // 1 km a 2,5 m/s, um ponto a cada 2 s.
        await EnviarAsync(ana, treino.Id, Reta(0, 201, 5, DateTimeOffset.UtcNow.AddMinutes(-9), segundos: 2));

        var doAutor = await ObterAsync(ana, treino.Id);
        var daBia = await ObterAsync(bia, treino.Id);
        var aparadoDoAutor = await ana.Client.GetFromJsonAsync(
            $"/api/atividades/{treino.Id}/percurso?aparado=true", AppJsonContext.Default.PercursoResponse, Token);

        Assert.True(doAutor.Voce);
        Assert.False(daBia.Voce);
        Assert.Equal(doAutor.Resumo.DistanciaMetros, daBia.Resumo.DistanciaMetros);
        Assert.True(daBia.Bbox!.LatMin > doAutor.Bbox!.LatMin);
        Assert.True(daBia.Bbox.LatMax < doAutor.Bbox.LatMax);
        Assert.Equal(daBia.Bbox, aparadoDoAutor!.Bbox);
    }

    [Fact]
    public async Task Mapa_ServeSoOsTilesEmVoltaDoPercurso()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var estranho = await _host.CreateUsuarioAsync("Estranho");
        var treino = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Corrida, true));
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(10));
        await EnviarAsync(ana, treino.Id, Reta(0, 201, 5, DateTimeOffset.UtcNow.AddMinutes(-9), segundos: 2));
        var tile = TileMapa.De(LatBase + 500 / MetrosPorGrau, LonBase, 16);

        var dentro = await ana.Client.GetAsync(Tile(treino.Id, tile), Token);
        var longe = await ana.Client.GetAsync(Tile(treino.Id, tile with { X = tile.X + 5 }), Token);
        var zoomAlto = await ana.Client.GetAsync(Tile(treino.Id, TileMapa.De(LatBase, LonBase, 18)), Token);
        var deFora = await estranho.Client.GetAsync(Tile(treino.Id, tile), Token);

        Assert.Equal(HttpStatusCode.OK, dentro.StatusCode);
        Assert.Equal("image/png", dentro.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, longe.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, zoomAlto.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deFora.StatusCode);
    }

    [Fact]
    public async Task Imagem_EntraComOSeloPercurso_SemMudarAComprovacao_EUmaSoVez()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await _host.AddMembroAsync(grupoId, nome: "Bia");
        var treino = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Corrida, true));
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(20));

        var aoVivo = await PostImagemAsync(ana, treino.Id);
        await TerminarAsync(ana, treino.Id, 1200);
        var primeira = await PostImagemAsync(ana, treino.Id);
        var segunda = await PostImagemAsync(ana, treino.Id);
        var atividade = await ana.Client.GetFromJsonAsync(
            $"/api/atividades/{treino.Id}", AppJsonContext.Default.AtividadeResponse, Token);

        await ApiTestHost.AssertProblemAsync(aoVivo, HttpStatusCode.Conflict, "ATIVIDADE_ENCERRADA");
        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        await ApiTestHost.AssertProblemAsync(segunda, HttpStatusCode.Conflict, "EVIDENCIA_JA_ANEXADA");
        var evidencia = Assert.Single(atividade!.Evidencias);
        Assert.Equal(OrigemDataCaptura.Percurso, evidencia.OrigemDataCaptura);
        Assert.False(evidencia.AdicionadaDepois);
        Assert.Equal(TipoComprovacao.RegistroManual, atividade.TipoComprovacao);
        Assert.Equal(SituacaoVerificacao.AguardandoFeedback, atividade.SituacaoVerificacao);
    }

    [Fact]
    public async Task Imagem_SemPercurso_Recusa()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Corrida, false));
        await RecuarInicioAsync(treino.Id, TimeSpan.FromMinutes(20));
        await TerminarAsync(ana, treino.Id, 1200);

        var response = await PostImagemAsync(ana, treino.Id);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.NotFound, "PERCURSO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Cancelar_ApagaOPercurso()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var treino = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Corrida, true));
        await EnviarAsync(ana, treino.Id, Reta(0, 5, 5, DateTimeOffset.UtcNow.AddSeconds(-30)));

        await ana.Client.DeleteAsync($"/api/atividades/{treino.Id}", Token);

        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM percursos"));
        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM pontos_percurso"));
    }

    [Fact]
    public async Task Cena_MostraADistanciaDeQuemTreinaComPercurso()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var treino = await LerAsync(await ComecarAsync(ana, grupoId, TipoAtividade.Caminhada, true));
        await EnviarAsync(ana, treino.Id, Reta(0, 11, 5, DateTimeOffset.UtcNow.AddSeconds(-60)));

        var cena = await bia.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/cena", AppJsonContext.Default.CenaResponse, Token);

        var membro = cena!.Membros.Single(m => m.UsuarioId == ana.Id);
        Assert.InRange(membro.TreinoAoVivo!.DistanciaMetros!.Value, 45, 55);
    }

    /// <summary>Pontos para o norte, a <paramref name="metros"/> por ponto, com precisão de 5 m.</summary>
    private static PontoPercursoRequest[] Reta(
        int seqInicial,
        int total,
        double metros,
        DateTimeOffset de,
        int segundos = 5) =>
        Enumerable.Range(0, total)
            .Select(i => new PontoPercursoRequest(
                seqInicial + i,
                de.AddSeconds(i * segundos),
                LatBase + i * metros / MetrosPorGrau,
                LonBase,
                5,
                null,
                0))
            .ToArray();

    private static string Tile(Guid atividadeId, TileMapa tile) =>
        $"/api/atividades/{atividadeId}/percurso/mapa/{tile.Zoom}/{tile.X}/{tile.Y}";

    private Task RecuarInicioAsync(Guid atividadeId, TimeSpan quanto) =>
        _host.ExecuteAsync($"""
            UPDATE atividades SET started_at = started_at - interval '{(int)quanto.TotalSeconds} seconds'
            WHERE id = '{atividadeId}'
            """);

    private static Task<HttpResponseMessage> ComecarAsync(
        TestUsuario usuario,
        Guid grupoId,
        string tipo,
        bool gravarPercurso,
        double? latitude = null,
        double? longitude = null) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades/em-andamento",
            new ComecarTreinoRequest(tipo, latitude, longitude, gravarPercurso),
            AppJsonContext.Default.ComecarTreinoRequest,
            Token);

    private static Task<HttpResponseMessage> EnviarAsync(
        TestUsuario usuario,
        Guid atividadeId,
        PontoPercursoRequest[] pontos) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/atividades/{atividadeId}/percurso/pontos",
            new EnviarPontosPercursoRequest(pontos),
            AppJsonContext.Default.EnviarPontosPercursoRequest,
            Token);

    private static async Task<PercursoResponse> ObterAsync(TestUsuario usuario, Guid atividadeId) =>
        (await usuario.Client.GetFromJsonAsync(
            $"/api/atividades/{atividadeId}/percurso", AppJsonContext.Default.PercursoResponse, Token))!;

    private static Task<HttpResponseMessage> TerminarAsync(TestUsuario usuario, Guid atividadeId, int duracao) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/atividades/{atividadeId}/terminar",
            new TerminarTreinoRequest(duracao, null, TipoComprovacao.RegistroManual),
            AppJsonContext.Default.TerminarTreinoRequest,
            Token);

    private static async Task<AtividadeResponse> LerAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token))!;

    private static Task<HttpResponseMessage> PostImagemAsync(TestUsuario usuario, Guid atividadeId)
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(JpegMinimo);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(bytes, "arquivo", "percurso.jpg");
        return usuario.Client.PostAsync($"/api/atividades/{atividadeId}/percurso/imagem", content, Token);
    }
}
