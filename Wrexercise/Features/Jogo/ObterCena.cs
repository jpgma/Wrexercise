using Npgsql;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Features.Progresso.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

internal static class ObterCena
{
    private sealed record MembroCena(Guid UsuarioId, string NomeExibicao, DateOnly EntradaLocal, bool ModoJogo);

    public static async Task<IResult> HandleAsync(
        Guid grupoId,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        if (await GrupoAcesso.GetPapelAsync(dataSource, grupoId, usuarioId, cancellationToken) is null)
        {
            return GrupoAcesso.GrupoNaoEncontrado();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var membros = await LoadMembrosAsync(connection, grupoId, cancellationToken);
        var comPendentes = await LoadComAtividadePendenteAsync(connection, grupoId, cancellationToken);
        var comMeta = await LoadComMetaCumpridaAsync(connection, grupoId, membros, cancellationToken);

        var resposta = membros
            .Select(m => new MembroCenaResponse(
                m.UsuarioId,
                m.NomeExibicao,
                PoseAvatar.Calcular(comPendentes.Contains(m.UsuarioId), comMeta.Contains(m.UsuarioId)),
                m.ModoJogo))
            .ToList();

        return Results.Json(new CenaResponse(grupoId, resposta), AppJsonContext.Default.CenaResponse);
    }

    private static async Task<IReadOnlyList<MembroCena>> LoadMembrosAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT m.usuario_id, u.nome_exibicao, (m.joined_at AT TIME ZONE g.fuso_horario)::date,
                u.modo_inicio = 'Jogo'
            FROM membros_grupo m
            JOIN grupos g ON g.id = m.grupo_id
            JOIN usuarios u ON u.id = m.usuario_id
            WHERE m.grupo_id = $1 AND m.removed_at IS NULL
            ORDER BY m.joined_at, m.usuario_id
            """, connection);
        command.Parameters.AddWithValue(grupoId);

        var membros = new List<MembroCena>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            membros.Add(new MembroCena(
                reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2), reader.GetBoolean(3)));
        }

        return membros;
    }

    /// <summary>Quem tem atividade ainda sem veredito neste grupo, na semana corrente (segunda a domingo, fuso do grupo).</summary>
    private static async Task<HashSet<Guid>> LoadComAtividadePendenteAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT DISTINCT a.usuario_id
            FROM atividades a
            JOIN grupos g ON g.id = a.grupo_id
            WHERE a.grupo_id = $1
              AND a.situacao_verificacao IN ('AguardandoFeedback', 'EmRevisao')
              AND (a.started_at AT TIME ZONE g.fuso_horario)::date
                  >= date_trunc('week', now() AT TIME ZONE g.fuso_horario)::date
              AND (a.started_at AT TIME ZONE g.fuso_horario)::date
                  < date_trunc('week', now() AT TIME ZONE g.fuso_horario)::date + 7
            """, connection);
        command.Parameters.AddWithValue(grupoId);

        var ids = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    /// <summary>Quem já cumpriu a meta da semana do desafio em destaque. Sem desafio em curso, ninguém.</summary>
    private static async Task<HashSet<Guid>> LoadComMetaCumpridaAsync(
        NpgsqlConnection connection,
        Guid grupoId,
        IReadOnlyList<MembroCena> membros,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        var destacado = await DesafioDestaqueGrupo.LoadAsync(connection, grupoId, cancellationToken);
        if (destacado is null)
        {
            return ids;
        }

        var semana = CalculoSemanaDesafio.GetSemana(destacado.DataInicio, destacado.DataFim, destacado.Hoje);
        if (semana is null)
        {
            return ids;
        }

        await using var command = new NpgsqlCommand(
            ContagensAtividadeDesafio.BuildAgregadoPorUsuarioSemanaAtual(),
            connection);
        command.Parameters.AddWithValue(destacado.Id);
        command.Parameters.AddWithValue(semana.Inicio);
        command.Parameters.AddWithValue(semana.Fim);

        var contagens = new Dictionary<Guid, (int Verificadas, int Pendentes)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                contagens[reader.GetGuid(0)] = (reader.GetInt32(1), reader.GetInt32(2));
            }
        }

        foreach (var membro in membros)
        {
            if (!CalculoSemanaDesafio.ParticipaDaSemana(semana.Inicio, membro.EntradaLocal)
                || !contagens.TryGetValue(membro.UsuarioId, out var contagem))
            {
                continue;
            }

            if (CalculoSemanaDesafio.CalculateProgresso(
                    contagem.Verificadas, contagem.Pendentes, destacado.SessoesPorSemana).MetaCumprida)
            {
                ids.Add(membro.UsuarioId);
            }
        }

        return ids;
    }
}
