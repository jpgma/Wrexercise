using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Atividades.Contracts;
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
            Assert.Equal(HttpStatusCode.Created, (await PostEvidenciaAsync(ana, id, JpegVariante(i))).StatusCode);
        }
    }

    [Fact]
    public async Task Criar_InicioMaisDe48HorasAtras_Recusado()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);

        var response = await PostAtividadeAsync(
            ana,
            grupoId,
            Valid() with { StartedAt = DateTimeOffset.UtcNow.AddHours(-49) });

        await ApiTestHost.AssertProblemAsync(response, HttpStatusCode.BadRequest, "ATIVIDADE_MUITO_ANTIGA");
    }

    [Fact]
    public async Task Criar_RegistradaMaisDe12HorasDepois_AvisoRegistroTardio()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);

        var response = await PostAtividadeAsync(
            ana,
            grupoId,
            Valid() with { StartedAt = DateTimeOffset.UtcNow.AddHours(-20), TipoComprovacao = TipoComprovacao.RegistroManual });
        var atividade = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token);

        Assert.Equal(TipoAviso.RegistroTardio, Assert.Single(atividade!.Avisos).Tipo);
        Assert.Equal([TipoAviso.RegistroTardio], Assert.Single(await ListarAsync(ana, grupoId)).Avisos);
    }

    [Fact]
    public async Task AnexarEvidencia_MesmaFotoEmOutroGrupo_Recusada()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoA = await _host.CreateGrupoAsync(ana, "A");
        var grupoB = await _host.CreateGrupoAsync(ana, "B");
        var primeira = await CriarComFotoAsync(ana, grupoA);
        var segunda = await CriarComFotoAsync(ana, grupoB);
        Assert.Equal(HttpStatusCode.Created, (await PostEvidenciaAsync(ana, primeira)).StatusCode);

        await ApiTestHost.AssertProblemAsync(
            await PostEvidenciaAsync(ana, segunda),
            HttpStatusCode.Conflict,
            "EVIDENCIA_DUPLICADA");
        Assert.Equal(1L, await _host.CountAsync("SELECT count(*) FROM evidencias_atividade"));
    }

    [Fact]
    public async Task AnexarEvidencia_MesmoOriginalRecomprimido_Recusada()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var original = new string('a', 64);
        var primeira = await CriarComFotoAsync(ana, grupoId);
        await PostEvidenciaAsync(ana, primeira, JpegVariante(1), new() { ["sha256Original"] = original });

        // A Bia não pode reaproveitar a foto da Ana, mesmo com outros bytes depois da compressão.
        await ApiTestHost.AvaliarFilaAsync(bia, grupoId);
        var daBia = await CriarComFotoAsync(bia, grupoId);
        await ApiTestHost.AssertProblemAsync(
            await PostEvidenciaAsync(bia, daBia, JpegVariante(2), new() { ["sha256Original"] = original.ToUpperInvariant() }),
            HttpStatusCode.Conflict,
            "EVIDENCIA_DUPLICADA");
    }

    [Fact]
    public async Task AnexarEvidencia_FotoParecida_AvisoComAtividadeRelacionada()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var primeira = await CriarComFotoAsync(ana, grupoId);
        await PostEvidenciaAsync(ana, primeira, JpegVariante(1), new() { ["dhash"] = "00000000000000ff" });
        var segunda = await CriarComFotoAsync(ana, grupoId);

        await PostEvidenciaAsync(ana, segunda, JpegVariante(2), new() { ["dhash"] = "000000000000003f" });

        var avisos = (await ObterAsync(ana, segunda)).Avisos;
        var parecida = Assert.Single(avisos, a => a.Tipo == TipoAviso.FotoParecida);
        Assert.Equal(primeira, parecida.AtividadeRelacionadaId);
    }

    [Fact]
    public async Task AnexarEvidencia_FotoParecidaDeOutroGrupo_NaoRevelaAAtividade()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoA = await _host.CreateGrupoAsync(ana, "A");
        var grupoB = await _host.CreateGrupoAsync(ana, "B");
        var bia = await _host.AddMembroAsync(grupoB, nome: "Bia");
        var primeira = await CriarComFotoAsync(ana, grupoA);
        await PostEvidenciaAsync(ana, primeira, JpegVariante(1), new() { ["dhash"] = "00000000000000ff" });
        var segunda = await CriarComFotoAsync(ana, grupoB);

        await PostEvidenciaAsync(ana, segunda, JpegVariante(2), new() { ["dhash"] = "00000000000000ff" });

        var vistoPelaBia = Assert.Single((await ObterAsync(bia, segunda)).Avisos, a => a.Tipo == TipoAviso.FotoParecida);
        Assert.Null(vistoPelaBia.AtividadeRelacionadaId);
        var vistoPelaAna = Assert.Single((await ObterAsync(ana, segunda)).Avisos, a => a.Tipo == TipoAviso.FotoParecida);
        Assert.Equal(primeira, vistoPelaAna.AtividadeRelacionadaId);
    }

    [Fact]
    public async Task AnexarEvidencia_GuardaSinaisSemDevolverLocalizacao()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var id = await CriarComFotoAsync(ana, grupoId);
        var capturadaEm = DateTimeOffset.UtcNow.AddMinutes(-20);

        var response = await PostEvidenciaAsync(ana, id, JpegVariante(1), new()
        {
            ["capturadaEm"] = capturadaEm.ToString("O"),
            ["origemDataCaptura"] = "Exif",
            ["latitude"] = "-23.5505",
            ["longitude"] = "-46.6333",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty((await ObterAsync(ana, id)).Avisos);
        Assert.Equal(1L, await _host.CountAsync("""
            SELECT count(*) FROM evidencias_atividade
            WHERE origem_data_captura = 'Exif' AND latitude = -23.5505 AND longitude = -46.6333
            """));
        var json = await (await ana.Client.GetAsync($"/api/atividades/{id}", Token)).Content.ReadAsStringAsync(Token);
        Assert.DoesNotContain("latitude", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-3 * 24 * 60, "Exif", TipoAviso.FotoForaDoPeriodo)]
    [InlineData(-3 * 24 * 60, "Arquivo", TipoAviso.FotoForaDoPeriodo)]
    [InlineData(null, null, TipoAviso.FotoSemData)]
    public async Task AnexarEvidencia_DataDaFoto_GeraAviso(int? minutosAtras, string? origem, string esperado)
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var id = await CriarComFotoAsync(ana, grupoId);
        var campos = new Dictionary<string, string>();
        if (minutosAtras is { } minutos)
        {
            campos["capturadaEm"] = DateTimeOffset.UtcNow.AddMinutes(minutos).ToString("O");
            campos["origemDataCaptura"] = origem!;
        }

        await PostEvidenciaAsync(ana, id, JpegVariante(1), campos);

        Assert.Equal(esperado, Assert.Single((await ObterAsync(ana, id)).Avisos).Tipo);
    }

    [Theory]
    [InlineData(-20, "EmRevisao")]
    [InlineData(-1, "Verificada")]
    public async Task FimDaJanela_ComAvisoQueBloqueia_VaiParaRevisao(int horasAtras, string esperada)
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await _host.AddMembroAsync(grupoId, nome: "Bia");
        var id = await CriarComFotoAsync(ana, grupoId, DateTimeOffset.UtcNow.AddHours(horasAtras));
        // Sem data só informa: não impede a verificação ao fim da janela.
        await PostEvidenciaAsync(ana, id);
        await _host.ExecuteAsync($"UPDATE atividades SET submitted_at = now() - interval '49 hours' WHERE id = '{id}'");

        await Wrexercise.Features.Reacoes.VerificacaoAtividade.EncerrarJanelasExpiradasAsync(_host.DataSource, Token);

        Assert.Equal(esperada, (await ObterAsync(ana, id)).SituacaoVerificacao);
    }

    [Fact]
    public async Task Criar_MesmaChave_DevolveAMesmaAtividade()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var chave = Guid.NewGuid();

        var primeira = await PostAtividadeComChaveAsync(ana, grupoId, chave);
        var repetida = await PostAtividadeComChaveAsync(ana, grupoId, chave);

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal(
            (await primeira.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token))!.Id,
            (await repetida.Content.ReadFromJsonAsync(AppJsonContext.Default.AtividadeResponse, Token))!.Id);
        Assert.Equal(1L, await _host.CountAsync("SELECT count(*) FROM atividades"));
    }

    [Fact]
    public async Task Criar_ChavesDiferentes_CriamDuasAtividades()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);

        await PostAtividadeComChaveAsync(ana, grupoId, Guid.NewGuid());
        await PostAtividadeComChaveAsync(ana, grupoId, Guid.NewGuid());

        Assert.Equal(2L, await _host.CountAsync("SELECT count(*) FROM atividades"));
    }

    [Fact]
    public async Task Criar_MesmaChaveDepoisDeChegarOutraAtividade_NaoCaiNaFila()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var chave = Guid.NewGuid();
        await PostAtividadeComChaveAsync(ana, grupoId, chave);
        await ApiTestHost.AvaliarFilaAsync(bia, grupoId);
        await CriarManualAsync(bia, grupoId);

        // A resposta da Ana se perdeu; a da Bia já está na fila dela, mas a repetição não é um registro novo.
        var repetida = await PostAtividadeComChaveAsync(ana, grupoId, chave);

        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
    }

    [Fact]
    public async Task Criar_MesmaChaveEmOutroGrupo_Recusada()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoA = await _host.CreateGrupoAsync(ana, "A");
        var grupoB = await _host.CreateGrupoAsync(ana, "B");
        var chave = Guid.NewGuid();
        await PostAtividadeComChaveAsync(ana, grupoA, chave);

        await ApiTestHost.AssertProblemAsync(
            await PostAtividadeComChaveAsync(ana, grupoB, chave),
            HttpStatusCode.UnprocessableEntity,
            "CHAVE_IDEMPOTENCIA_REUTILIZADA");
    }

    [Fact]
    public async Task Criar_ChaveDeOutraPessoa_NaoDevolveAAtividadeDela()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var chave = Guid.NewGuid();
        await PostAtividadeComChaveAsync(ana, grupoId, chave);
        await ApiTestHost.AvaliarFilaAsync(bia, grupoId);

        var daBia = await PostAtividadeComChaveAsync(bia, grupoId, chave);

        Assert.Equal(HttpStatusCode.Created, daBia.StatusCode);
        Assert.Equal(2L, await _host.CountAsync("SELECT count(*) FROM atividades"));
    }

    [Fact]
    public async Task Criar_ChaveInvalida_Recusada()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/grupos/{grupoId}/atividades")
        {
            Content = JsonContent.Create(Valid(), AppJsonContext.Default.CriarAtividadeRequest),
        };
        request.Headers.Add("Idempotency-Key", "nao-e-uuid");

        await ApiTestHost.AssertProblemAsync(
            await ana.Client.SendAsync(request, Token),
            HttpStatusCode.BadRequest,
            "DADOS_INVALIDOS");
    }

    [Fact]
    public async Task Criar_MesmaChaveAoMesmoTempo_CriaUmaSo()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var chave = Guid.NewGuid();

        var respostas = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => PostAtividadeComChaveAsync(ana, grupoId, chave)));

        Assert.All(respostas, r => Assert.True(r.IsSuccessStatusCode, r.StatusCode.ToString()));
        Assert.Equal(1L, await _host.CountAsync("SELECT count(*) FROM atividades"));
    }

    [Fact]
    public async Task AnexarEvidencia_RepeticaoNaMesmaAtividade_JaAnexadaMesmoNoLimite()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var id = await CriarComFotoAsync(ana, grupoId);
        for (var i = 0; i < 3; i++)
        {
            await PostEvidenciaAsync(ana, id, JpegVariante(i));
        }

        await ApiTestHost.AssertProblemAsync(
            await PostEvidenciaAsync(ana, id, JpegVariante(0)),
            HttpStatusCode.Conflict,
            "EVIDENCIA_JA_ANEXADA");
    }

    [Fact]
    public async Task Excluir_AutorNosPrimeirosMinutos_ApagaAtividadeEFotos()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await _host.AddMembroAsync(grupoId, nome: "Caio");
        var id = await CriarComFotoAsync(ana, grupoId);
        (await PostEvidenciaAsync(ana, id, JpegVariante(1))).EnsureSuccessStatusCode();
        // Um Kudos não impede: a exclusão só fecha quando alguém contesta.
        await ReagirAsync(bia, id, "Kudos");

        Assert.NotNull((await ObterAsync(ana, id)).ExclusaoPermitidaAte);
        Assert.Null((await ObterAsync(bia, id)).ExclusaoPermitidaAte);
        await ApiTestHost.AssertProblemAsync(
            await bia.Client.DeleteAsync($"/api/atividades/{id}", Token),
            HttpStatusCode.Forbidden,
            "ACESSO_NEGADO");

        var response = await ana.Client.DeleteAsync($"/api/atividades/{id}", Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.Client.GetAsync($"/api/atividades/{id}", Token)).StatusCode);
        Assert.Equal(0L, await _host.ScalarAsync<long>("SELECT count(*) FROM evidencias_atividade"));
        // A mesma foto volta a valer: o autor registra de novo, agora com os dados certos.
        var nova = await CriarComFotoAsync(ana, grupoId);
        Assert.Equal(HttpStatusCode.Created, (await PostEvidenciaAsync(ana, nova, JpegVariante(1))).StatusCode);
    }

    [Fact]
    public async Task Excluir_DepoisDe15MinutosOuContestada_Recusada()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        var antiga = await CriarManualAsync(ana, grupoId);
        await _host.ExecuteAsync(
            $"UPDATE atividades SET submitted_at = now() - interval '16 minutes' WHERE id = '{antiga}'");
        var contestada = await CriarManualAsync(ana, grupoId);
        await ReagirAsync(bia, contestada, "Suspeito");

        Assert.Null((await ObterAsync(ana, antiga)).ExclusaoPermitidaAte);
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.DeleteAsync($"/api/atividades/{antiga}", Token),
            HttpStatusCode.Conflict,
            "EXCLUSAO_NAO_PERMITIDA");
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.DeleteAsync($"/api/atividades/{contestada}", Token),
            HttpStatusCode.Conflict,
            "EXCLUSAO_NAO_PERMITIDA");
    }

    [Fact]
    public async Task Excluir_FotoParecidaDeOutra_AvisoFicaSemLink()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await _host.AddMembroAsync(grupoId, nome: "Bia");
        var primeira = await CriarComFotoAsync(ana, grupoId);
        await PostEvidenciaAsync(ana, primeira, JpegVariante(1), new() { ["dhash"] = "00000000000000ff" });
        var segunda = await CriarComFotoAsync(ana, grupoId);
        await PostEvidenciaAsync(ana, segunda, JpegVariante(2), new() { ["dhash"] = "000000000000003f" });

        (await ana.Client.DeleteAsync($"/api/atividades/{primeira}", Token)).EnsureSuccessStatusCode();

        var parecida = Assert.Single((await ObterAsync(ana, segunda)).Avisos, a => a.Tipo == TipoAviso.FotoParecida);
        Assert.Null(parecida.AtividadeRelacionadaId);
    }

    private static Task<HttpResponseMessage> PostAtividadeComChaveAsync(TestUsuario usuario, Guid grupoId, Guid chave)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/grupos/{grupoId}/atividades")
        {
            Content = JsonContent.Create(Valid(), AppJsonContext.Default.CriarAtividadeRequest),
        };
        request.Headers.Add("Idempotency-Key", chave.ToString());
        return usuario.Client.SendAsync(request, Token);
    }

    private static byte[] JpegVariante(int variante)
    {
        // Bytes depois do fim do JPEG mudam o SHA-256 sem mudar o formato detectado.
        var bytes = new byte[JpegMinimo.Length + 1];
        JpegMinimo.CopyTo(bytes, 0);
        bytes[^1] = (byte)(variante + 1);
        return bytes;
    }

    private static async Task<AtividadeResponse> ObterAsync(TestUsuario usuario, Guid atividadeId) =>
        (await usuario.Client.GetFromJsonAsync(
            $"/api/atividades/{atividadeId}",
            AppJsonContext.Default.AtividadeResponse,
            Token))!;

    private static async Task<Guid> CriarComFotoAsync(TestUsuario usuario, Guid grupoId, DateTimeOffset? startedAt = null)
    {
        var response = await PostAtividadeAsync(
            usuario,
            grupoId,
            Valid() with { StartedAt = startedAt ?? DateTimeOffset.UtcNow.AddMinutes(-30) });
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

    private static Task<HttpResponseMessage> PostEvidenciaAsync(
        TestUsuario usuario,
        Guid atividadeId,
        byte[]? arquivo = null,
        Dictionary<string, string>? campos = null)
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(arquivo ?? JpegMinimo);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(bytes, "arquivo", "foto.jpg");
        foreach (var (nome, valor) in campos ?? [])
        {
            content.Add(new StringContent(valor), nome);
        }

        return usuario.Client.PostAsync($"/api/atividades/{atividadeId}/evidencias", content, Token);
    }
}
