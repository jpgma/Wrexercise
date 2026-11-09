using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Atividades;

public sealed class AtividadesRoutesTests : IAsyncLifetime
{
    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly byte[] JpegMinimo =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9,
    ];

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Create_ReturnsAtividade_ForRegistroManual()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);

        var response = await PostAtividadeAsync(ana, grupoId, Valid() with { TipoComprovacao = TipoComprovacao.RegistroManual });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var atividade = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        Assert.Equal(TipoAtividade.Corrida, atividade!.Tipo);
        Assert.Equal(TipoComprovacao.RegistroManual, atividade.TipoComprovacao);
        Assert.True(atividade.Voce);
        Assert.Empty(atividade.Evidencias);
    }

    [Fact]
    public async Task AnexarEvidencia_RejectsRegistroManual()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var criada = await PostAtividadeAsync(ana, grupoId, Valid() with { TipoComprovacao = TipoComprovacao.RegistroManual });
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        var response = await PostEvidenciaAsync(ana, atividade!.Id);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.Conflict, "COMPROVACAO_NAO_APLICAVEL");
    }

    [Fact]
    public async Task AnexarEvidencia_StoresFile_ForFotografia()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var criada = await PostAtividadeAsync(ana, grupoId, Valid() with { TipoComprovacao = TipoComprovacao.Fotografia });
        var atividade = await criada.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        var response = await PostEvidenciaAsync(ana, atividade!.Id);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var evidencia = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.EvidenciaCriadaResponse, Token);
        var imagem = await ana.Client.GetAsync(evidencia!.Url, Token);
        Assert.Equal(HttpStatusCode.OK, imagem.StatusCode);
        Assert.Equal("image/jpeg", imagem.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task List_Returns404_ForOutroGrupo()
    {
        var ana = await _host.CreateUsuarioAsync();
        var bob = await _host.CreateUsuarioAsync("Bob");
        var grupoId = await _host.CreateGrupoAsync(ana);

        var response = await bob.Client.GetAsync($"/api/grupos/{grupoId}/atividades", Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.NotFound, "GRUPO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Recentes_AgregaVariosGrupos()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoA = await _host.CreateGrupoAsync(ana, "A");
        var grupoB = await _host.CreateGrupoAsync(ana, "B");
        await PostAtividadeAsync(ana, grupoA, Valid());
        await PostAtividadeAsync(ana, grupoB, Valid() with { Tipo = TipoAtividade.Yoga });

        var response = await ana.Client.GetAsync("/api/atividades/recentes", Token);
        response.EnsureSuccessStatusCode();
        var pagina = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadesPaginadasResponse, Token);
        Assert.Equal(2, pagina!.Atividades.Count);
    }

    [Fact]
    public async Task Progresso_ContaAtividadeElegivelNaSemana()
    {
        var ana = await _host.CreateUsuarioAsync();
        var grupoId = await _host.CreateGrupoAsync(ana);
        var desafioId = Guid.NewGuid();

        await _host.ExecuteAsync($"""
            INSERT INTO desafios (
                id, grupo_id, criador_id, nome, data_inicio, data_fim,
                duracao_minima_segundos, sessoes_por_semana)
            SELECT '{desafioId}', g.id, m.usuario_id, 'Ativo',
                date_trunc('week', (now() AT TIME ZONE g.fuso_horario))::date,
                date_trunc('week', (now() AT TIME ZONE g.fuso_horario))::date + 27,
                3600, 4
            FROM grupos g
            JOIN membros_grupo m ON m.grupo_id = g.id AND m.removed_at IS NULL AND m.papel = 'Proprietario'
            WHERE g.id = '{grupoId}'
            """);

        await _host.ExecuteAsync($"""
            UPDATE membros_grupo SET joined_at = now() - interval '60 days' WHERE grupo_id = '{grupoId}'
            """);

        await PostAtividadeAsync(ana, grupoId, Valid() with { DuracaoSegundos = 3600, TipoComprovacao = TipoComprovacao.RegistroManual });

        var progresso = await ana.Client.GetAsync($"/api/desafios/{desafioId}/progresso", Token);
        progresso.EnsureSuccessStatusCode();
        var body = await progresso.Content.ReadFromJsonAsync(AppJsonContext.Default.ProgressoResponse, Token);
        var anaProgresso = Assert.Single(body!.Participantes);
        Assert.Equal(1, anaProgresso.SessoesVerificadas);
        Assert.Equal(1, anaProgresso.SessoesProvisorias);
    }

    [Fact]
    public async Task Listar_MostraDescricaoEContagensDeReacoes()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");
        var id = await CriarManualAsync(ana, grupoId);
        await ReagirAsync(bia, id, "Kudos");
        await ReagirAsync(caio, id, "Suspeito");

        var atividade = Assert.Single(await ListarAsync(ana, grupoId));

        Assert.Equal("Treino leve", atividade.Descricao);
        Assert.Equal((1, 1, 0), (atividade.Kudos, atividade.Suspeito, atividade.Rejeitar));
    }

    [Fact]
    public async Task Listar_FiltraNoServidor_MantendoAPaginacao()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");

        // A da Bia vem primeiro: com a da Ana pendente, a Bia não poderia registrar.
        var verificada = await CriarManualAsync(bia, grupoId);
        await ReagirAsync(ana, verificada, "Kudos");
        await ReagirAsync(caio, verificada, "Kudos");
        var pendente = await CriarManualAsync(ana, grupoId);
        var emRevisao = await CriarManualAsync(ana, grupoId);
        await ReagirAsync(bia, emRevisao, "Suspeito");

        Assert.Equal(
            new HashSet<Guid> { pendente, emRevisao },
            (await ListarAsync(ana, grupoId, "?filtro=Minhas")).Select(a => a.Id).ToHashSet());
        Assert.Equal(
            new HashSet<Guid> { pendente, emRevisao },
            (await ListarAsync(ana, grupoId, "?filtro=Pendentes")).Select(a => a.Id).ToHashSet());
        Assert.Equal([verificada], (await ListarAsync(ana, grupoId, "?filtro=Verificadas")).Select(a => a.Id));
        Assert.Equal([emRevisao], (await ListarAsync(ana, grupoId, "?filtro=EmRevisao")).Select(a => a.Id));

        var primeira = await ana.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/atividades?filtro=Minhas&limite=1",
            AppJsonContext.Default.AtividadesPaginadasResponse,
            Token);
        Assert.NotNull(primeira!.ProximoCursor);
        var segunda = await ana.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/atividades?filtro=Minhas&limite=1&cursor={Uri.EscapeDataString(primeira.ProximoCursor)}",
            AppJsonContext.Default.AtividadesPaginadasResponse,
            Token);
        Assert.Null(segunda!.ProximoCursor);
        Assert.NotEqual(primeira.Atividades[0].Id, Assert.Single(segunda.Atividades).Id);
    }

    [Fact]
    public async Task Listar_FiltroDesconhecido_Recusado()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);

        var response = await ana.Client.GetAsync($"/api/grupos/{grupoId}/atividades?filtro=Outro", Token);

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task ParaAvaliar_SoOQueEsperaAReacaoDeQuemConsulta()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");

        var daBia = await CriarManualAsync(bia, grupoId);
        await ReagirAsync(ana, daBia, "Kudos");
        var aguardando = await CriarManualAsync(ana, grupoId);
        var jaReagida = await CriarManualAsync(ana, grupoId);
        await ReagirAsync(bia, jaReagida, "Kudos");
        var emRevisao = await CriarManualAsync(ana, grupoId);
        await ReagirAsync(caio, emRevisao, "Suspeito");
        var expirada = await CriarManualAsync(ana, grupoId);
        await _host.ExecuteAsync(
            $"UPDATE atividades SET submitted_at = now() - interval '49 hours' WHERE id = '{expirada}'");

        var fila = await bia.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/atividades/para-avaliar",
            AppJsonContext.Default.FilaAvaliacaoResponse,
            Token);

        Assert.Equal(
            new HashSet<Guid> { aguardando, emRevisao },
            fila!.Atividades.Select(a => a.Id).ToHashSet());
    }

    [Fact]
    public async Task Criar_ComFilaDeAvaliacao_RecusadoAteReagir()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");

        var primeira = await CriarManualAsync(ana, grupoId);
        // A própria atividade não entra na fila da autora.
        var segunda = await CriarManualAsync(ana, grupoId);

        await ApiTestHost.AssertProblemAsync(
            await PostAtividadeAsync(bia, grupoId, Valid()),
            HttpStatusCode.Conflict,
            "AVALIACOES_PENDENTES");

        // Bloqueia enquanto restar qualquer uma; e qualquer reação conta, não só dar moral.
        await ReagirAsync(bia, primeira, "Suspeito");
        await ApiTestHost.AssertProblemAsync(
            await PostAtividadeAsync(bia, grupoId, Valid()),
            HttpStatusCode.Conflict,
            "AVALIACOES_PENDENTES");
        await ReagirAsync(bia, segunda, "Rejeitar");

        Assert.Equal(HttpStatusCode.Created, (await PostAtividadeAsync(bia, grupoId, Valid())).StatusCode);
    }

    [Fact]
    public async Task Criar_JanelaEncerrada_NaoBloqueia()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");

        var expirada = await CriarManualAsync(ana, grupoId);
        await _host.ExecuteAsync(
            $"UPDATE atividades SET submitted_at = now() - interval '49 hours' WHERE id = '{expirada}'");

        Assert.Equal(HttpStatusCode.Created, (await PostAtividadeAsync(bia, grupoId, Valid())).StatusCode);
    }

    [Fact]
    public async Task Criar_QuemEntrouDepois_NaoEBloqueadoPeloQueVeioAntes()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await CriarManualAsync(ana, grupoId);
        var caio = await _host.AddMembroAsync(grupoId, nome: "Caio");

        Assert.Equal(HttpStatusCode.Created, (await PostAtividadeAsync(caio, grupoId, Valid())).StatusCode);
    }

    [Fact]
    public async Task ParaAvaliar_ForaDoGrupo_NaoEncontrado()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var estranha = await _host.CreateUsuarioAsync("Estranha");

        await ApiTestHost.AssertProblemAsync(
            await estranha.Client.GetAsync($"/api/grupos/{grupoId}/atividades/para-avaliar", Token),
            HttpStatusCode.NotFound,
            "GRUPO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Miniatura_ServidaSoParaMembros_ComCachePrivado()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var estranha = await _host.CreateUsuarioAsync("Estranha");
        var id = await CriarComFotoAsync(ana, grupoId);

        var response = await PostEvidenciaComMiniaturaAsync(ana, id, JpegMinimo);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var atividade = Assert.Single(await ListarAsync(ana, grupoId));
        Assert.NotNull(atividade.MiniaturaUrl);

        var miniatura = await ana.Client.GetAsync(atividade.MiniaturaUrl, Token);
        Assert.Equal(HttpStatusCode.OK, miniatura.StatusCode);
        Assert.Equal("image/jpeg", miniatura.Content.Headers.ContentType?.MediaType);
        Assert.Equal(JpegMinimo, await miniatura.Content.ReadAsByteArrayAsync(Token));
        var cache = miniatura.Headers.CacheControl!;
        Assert.True(cache.Private);
        Assert.Equal(TimeSpan.FromDays(7), cache.MaxAge);
        Assert.Contains(cache.Extensions, e => e.Name == "immutable");

        await ApiTestHost.AssertProblemAsync(
            await estranha.Client.GetAsync(atividade.MiniaturaUrl, Token),
            HttpStatusCode.NotFound,
            "EVIDENCIA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task Miniatura_Ausente_ListaSemUrl()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var id = await CriarComFotoAsync(ana, grupoId);

        var evidencia = await (await PostEvidenciaAsync(ana, id))
            .Content.ReadFromJsonAsync(AppJsonContext.Default.EvidenciaCriadaResponse, Token);

        var atividade = Assert.Single(await ListarAsync(ana, grupoId));
        Assert.Null(atividade.MiniaturaUrl);
        Assert.Equal(1, atividade.TotalEvidencias);
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.GetAsync($"{evidencia!.Url}/miniatura", Token),
            HttpStatusCode.NotFound,
            "EVIDENCIA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task Miniatura_GrandeDemais_RecusaOEnvioInteiro()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var id = await CriarComFotoAsync(ana, grupoId);
        var grande = new byte[(100 * 1024) + 1];
        JpegMinimo.CopyTo(grande, 0);

        var response = await PostEvidenciaComMiniaturaAsync(ana, id, grande);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM evidencias_atividade"));
    }

    [Fact]
    public async Task Miniatura_NaoJpeg_RecusaOEnvioInteiro()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var id = await CriarComFotoAsync(ana, grupoId);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        var response = await PostEvidenciaComMiniaturaAsync(ana, id, png);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await _host.CountAsync("SELECT count(*) FROM evidencias_atividade"));
    }

    [Fact]
    public async Task SemRevisores_AceitaAsTresEvidencias()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var id = await CriarComFotoAsync(ana, grupoId);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await PostEvidenciaAsync(ana, id)).StatusCode);
        }
    }

    private static async Task<Guid> CriarComFotoAsync(TestUsuario usuario, Guid grupoId)
    {
        var response = await PostAtividadeAsync(usuario, grupoId, Valid());
        response.EnsureSuccessStatusCode();
        var atividade = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        return atividade!.Id;
    }

    private static Task<HttpResponseMessage> PostEvidenciaComMiniaturaAsync(
        TestUsuario usuario,
        Guid atividadeId,
        byte[] miniatura)
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(JpegMinimo);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(bytes, "arquivo", "foto.jpg");
        var bytesMiniatura = new ByteArrayContent(miniatura);
        bytesMiniatura.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(bytesMiniatura, "miniatura", "miniatura.jpg");
        return usuario.Client.PostAsync($"/api/atividades/{atividadeId}/evidencias", content, Token);
    }

    private static async Task<Guid> CriarManualAsync(TestUsuario usuario, Guid grupoId)
    {
        var response = await PostAtividadeAsync(
            usuario,
            grupoId,
            Valid() with { TipoComprovacao = TipoComprovacao.RegistroManual });
        response.EnsureSuccessStatusCode();
        var atividade = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);
        return atividade!.Id;
    }

    private static async Task ReagirAsync(TestUsuario usuario, Guid atividadeId, string tipo)
    {
        var response = await usuario.Client.PutAsJsonAsync(
            $"/api/atividades/{atividadeId}/reacao",
            new Wrexercise.Features.Reacoes.ReacaoRequest(tipo),
            AppJsonContext.Default.ReacaoRequest,
            Token);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<IReadOnlyList<AtividadeResumoResponse>> ListarAsync(
        TestUsuario usuario,
        Guid grupoId,
        string query = "") =>
        (await usuario.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/atividades{query}",
            AppJsonContext.Default.AtividadesPaginadasResponse,
            Token))!.Atividades;

    private static CriarAtividadeRequest Valid() => new(
        TipoAtividade.Corrida,
        3600,
        DateTimeOffset.UtcNow.AddMinutes(-30),
        "Treino leve",
        TipoComprovacao.Fotografia);

    private static Task<HttpResponseMessage> PostAtividadeAsync(
        TestUsuario usuario,
        Guid grupoId,
        CriarAtividadeRequest body) =>
        usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            body,
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);

    private static Task<HttpResponseMessage> PostEvidenciaAsync(TestUsuario usuario, Guid atividadeId)
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(JpegMinimo);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(bytes, "arquivo", "foto.jpg");
        return usuario.Client.PostAsync($"/api/atividades/{atividadeId}/evidencias", content, Token);
    }
}
