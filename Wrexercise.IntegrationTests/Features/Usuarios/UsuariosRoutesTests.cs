using System.Net;
using System.Net.Http.Json;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Usuarios;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.IntegrationTests.Features.Usuarios;

public sealed class UsuariosRoutesTests : IAsyncLifetime
{
    private const string HojeNoGrupo = "(now() AT TIME ZONE 'America/Sao_Paulo')::date";
    private const string SegundaDaSemana = $"date_trunc('week', {HojeNoGrupo})::date";

    private ApiTestHost _host = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await ApiTestHost.CreateAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task PerfilMembro_SoComDadosDoGrupo_SemEmailNemConquistas()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var outroGrupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, PapelMembro.Administrador, "Bia");
        await CriarAtividadeSoloAsync(ana, outroGrupoId);
        await CriarAtividadeAsync(ana, grupoId);
        await _host.ExecuteAsync("UPDATE atividades SET situacao_verificacao = 'Verificada', verified_at = now()");

        var response = await bia.Client.GetAsync($"/api/grupos/{grupoId}/membros/{ana.Id}/perfil", Token);

        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync(Token);
        Assert.DoesNotContain("@", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("conquistas", raw, StringComparison.OrdinalIgnoreCase);
        var perfil = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.PerfilMembroResponse, Token);
        Assert.Equal("Ana", perfil!.NomeExibicao);
        Assert.Equal(PapelMembro.Proprietario, perfil.Papel);
        Assert.Equal(1, perfil.TotalTreinos);
        Assert.Equal(3600, perfil.TotalSegundos);
        Assert.Null(perfil.Desafio);
    }

    [Fact]
    public async Task PerfilMembro_ForaDoGrupo_NaoEncontrado()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var estranha = await _host.CreateUsuarioAsync("Estranha");
        var removida = await _host.AddMembroAsync(grupoId, nome: "Removida");
        await ana.Client.DeleteAsync($"/api/grupos/{grupoId}/membros/{removida.Id}", Token);

        await ApiTestHost.AssertProblemAsync(
            await estranha.Client.GetAsync($"/api/grupos/{grupoId}/membros/{ana.Id}/perfil", Token),
            HttpStatusCode.NotFound,
            "GRUPO_NAO_ENCONTRADO");
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.GetAsync($"/api/grupos/{grupoId}/membros/{estranha.Id}/perfil", Token),
            HttpStatusCode.NotFound,
            "MEMBRO_NAO_ENCONTRADO");
        await ApiTestHost.AssertProblemAsync(
            await ana.Client.GetAsync($"/api/grupos/{grupoId}/membros/{removida.Id}/perfil", Token),
            HttpStatusCode.NotFound,
            "MEMBRO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task PerfilMembro_MostraODesafioAtual_NaoOAgendado_ComSemanasDoMembro()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await _host.ExecuteAsync($"""
            INSERT INTO desafios (
                grupo_id, criador_id, nome, data_inicio, data_fim, duracao_minima_segundos, sessoes_por_semana)
            VALUES
                ('{grupoId}', '{ana.Id}', 'Atual', {SegundaDaSemana} - 7, {SegundaDaSemana} + 13, 3600, 1),
                ('{grupoId}', '{ana.Id}', 'Próximo', {SegundaDaSemana} + 14, {SegundaDaSemana} + 20, 3600, 1)
            """);
        await _host.ExecuteAsync(
            $"UPDATE membros_grupo SET joined_at = now() - interval '60 days' WHERE usuario_id = '{ana.Id}'");
        await _host.ExecuteAsync($"""
            UPDATE membros_grupo
            SET joined_at = (({SegundaDaSemana} - 6)::timestamp AT TIME ZONE 'America/Sao_Paulo')
            WHERE usuario_id = '{bia.Id}'
            """);
        await CriarAtividadeAsync(ana, grupoId);
        await _host.ExecuteAsync("UPDATE atividades SET situacao_verificacao = 'Verificada', verified_at = now()");

        var doAna = await GetPerfilAsync(bia, grupoId, ana.Id);
        Assert.Equal("Atual", doAna.Desafio!.Nome);
        Assert.True(doAna.Desafio.EmAndamento);
        Assert.Equal(1, doAna.Desafio.SessoesVerificadas);
        Assert.Equal(1, doAna.Desafio.SemanasCumpridas);
        Assert.Equal(2, doAna.Desafio.SemanasDisputadas);
        Assert.Equal(1, doAna.Desafio.SequenciaSemanas);

        var daBia = await GetPerfilAsync(ana, grupoId, bia.Id);
        Assert.Equal([false, true, true], daBia.Desafio!.Semanas.Select(s => s.Participou));
        Assert.Equal(1, daBia.Desafio.SemanasDisputadas);
    }

    [Fact]
    public async Task MeuPerfil_SomaOsGrupos_ECalculaConquistas()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoUm = await _host.CreateGrupoAsync(ana);
        var grupoDois = await _host.CreateGrupoAsync(ana);
        for (var i = 0; i < 6; i++)
        {
            await CriarAtividadeSoloAsync(ana, grupoUm);
            await CriarAtividadeSoloAsync(ana, grupoDois);
        }

        var perfil = await ana.Client.GetFromJsonAsync(
            "/api/usuarios/eu/perfil",
            AppJsonContext.Default.MeuPerfilResponse,
            Token);

        Assert.Equal(12, perfil!.TotalTreinos);
        Assert.Equal(12 * 3600, perfil.TotalSegundos);
        Assert.Equal(8, perfil.Semanas.Count);
        Assert.Equal(12, perfil.Semanas.Sum(s => s.SessoesVerificadas));

        // As medalhas contam dias com treino em cada grupo: hoje, nos dois grupos, são 2.
        Assert.DoesNotContain(perfil.Conquistas, c => c.Codigo == "Treinos10");

        // Antes da meta diária, cada atividade contava um.
        await _host.ExecuteAsync($"""
            UPDATE atividades SET started_at = timestamptz '2026-11-02 12:00-03'
            WHERE usuario_id = '{ana.Id}'
            """);
        perfil = await ana.Client.GetFromJsonAsync(
            "/api/usuarios/eu/perfil",
            AppJsonContext.Default.MeuPerfilResponse,
            Token);
        Assert.Contains(perfil!.Conquistas, c => c.Codigo == "Treinos10");
    }

    [Theory]
    [InlineData("Pacific/Kiritimati")] // UTC+14
    [InlineData("Pacific/Pago_Pago")] // UTC−11
    public async Task MeuPerfil_SemanasComecamNaSegunda_NoFusoDoGrupo(string fusoHorario)
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await _host.ExecuteAsync($"UPDATE grupos SET fuso_horario = '{fusoHorario}' WHERE id = '{grupoId}'");
        var segundaNoFuso = await _host.ScalarAsync<DateTime>(
            $"SELECT date_trunc('week', (now() AT TIME ZONE '{fusoHorario}')::date)::date::timestamp");

        var perfil = await ana.Client.GetFromJsonAsync(
            "/api/usuarios/eu/perfil",
            AppJsonContext.Default.MeuPerfilResponse,
            Token);

        Assert.All(perfil!.Semanas, s => Assert.Equal(DayOfWeek.Monday, s.Inicio.DayOfWeek));
        Assert.Equal(DateOnly.FromDateTime(segundaNoFuso), perfil.Semanas[^1].Inicio);
    }

    [Fact]
    public async Task MeuPerfil_MelhorSequenciaUsaUmUnicoDesafio()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        await _host.ExecuteAsync($"""
            INSERT INTO desafios (
                grupo_id, criador_id, nome, data_inicio, data_fim, duracao_minima_segundos, sessoes_por_semana)
            VALUES ('{grupoId}', '{ana.Id}', 'Longo', {SegundaDaSemana} - 28, {SegundaDaSemana} + 6, 3600, 1)
            """);
        await _host.ExecuteAsync(
            $"UPDATE membros_grupo SET joined_at = now() - interval '90 days' WHERE usuario_id = '{ana.Id}'");
        foreach (var semanasAtras in new[] { 4, 3, 1 })
        {
            await CriarAtividadeSoloAsync(ana, grupoId);
            await _host.ExecuteAsync($"""
                UPDATE atividades SET started_at = now() - interval '{semanasAtras} weeks'
                WHERE id = (SELECT id FROM atividades WHERE started_at > now() - interval '1 day' LIMIT 1)
                """);
        }

        var perfil = await ana.Client.GetFromJsonAsync(
            "/api/usuarios/eu/perfil",
            AppJsonContext.Default.MeuPerfilResponse,
            Token);

        Assert.Equal(2, perfil!.MelhorSequencia);
    }

    [Fact]
    public async Task ListarAtividades_FiltraPorAutor()
    {
        var ana = await _host.CreateUsuarioAsync("Ana");
        var grupoId = await _host.CreateGrupoAsync(ana);
        var bia = await _host.AddMembroAsync(grupoId, nome: "Bia");
        await CriarAtividadeAsync(ana, grupoId);
        await CriarAtividadeAsync(bia, grupoId);

        var lista = await ana.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/atividades?autorId={bia.Id}",
            AppJsonContext.Default.AtividadesPaginadasResponse,
            Token);

        Assert.Equal("Bia", Assert.Single(lista!.Atividades).NomeExibicao);
    }

    private static async Task<PerfilMembroResponse> GetPerfilAsync(TestUsuario leitor, Guid grupoId, Guid usuarioId) =>
        (await leitor.Client.GetFromJsonAsync(
            $"/api/grupos/{grupoId}/membros/{usuarioId}/perfil",
            AppJsonContext.Default.PerfilMembroResponse,
            Token))!;

    private static async Task CriarAtividadeAsync(TestUsuario usuario, Guid grupoId)
    {
        var response = await usuario.Client.PostAsJsonAsync(
            $"/api/grupos/{grupoId}/atividades",
            new CriarAtividadeRequest(
                TipoAtividade.Corrida,
                3600,
                DateTimeOffset.UtcNow.AddMinutes(-30),
                null,
                TipoComprovacao.RegistroManual),
            AppJsonContext.Default.CriarAtividadeRequest,
            Token);
        response.EnsureSuccessStatusCode();
    }

    // Em um grupo sem outros membros, a atividade manual já nasce verificada.
    private static Task CriarAtividadeSoloAsync(TestUsuario usuario, Guid grupoId) =>
        CriarAtividadeAsync(usuario, grupoId);
}
