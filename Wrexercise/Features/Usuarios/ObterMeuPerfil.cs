using Npgsql;
using Wrexercise.Features.Usuarios.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Usuarios;

internal static class ObterMeuPerfil
{
    private const int SemanasNoGrafico = 8;

    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        DateTimeOffset membroDesde;
        await using (var usuario = new NpgsqlCommand("SELECT created_at FROM usuarios WHERE id = $1", connection))
        {
            usuario.Parameters.AddWithValue(usuarioId);
            membroDesde = TimestampLeitura.FromScalar((await usuario.ExecuteScalarAsync(cancellationToken))!);
        }

        long totalSegundos;
        await using (var totais = new NpgsqlCommand("""
            SELECT COALESCE(sum(duracao_segundos), 0)::bigint
            FROM atividades
            WHERE usuario_id = $1 AND situacao_verificacao = 'Verificada'
            """, connection))
        {
            totais.Parameters.AddWithValue(usuarioId);
            totalSegundos = (long)(await totais.ExecuteScalarAsync(cancellationToken))!;
        }

        var estatisticas = await ConquistasDoUsuario.LoadEstatisticasAsync(connection, usuarioId, cancellationToken);
        var semanas = await LoadSemanasAsync(connection, usuarioId, cancellationToken);

        return Results.Json(
            new MeuPerfilResponse(
                membroDesde,
                estatisticas.TotalTreinos,
                totalSegundos,
                estatisticas.MelhorSequencia,
                semanas,
                ConquistasUsuario.Visiveis(ConquistasUsuario.Listar(estatisticas))),
            AppJsonContext.Default.MeuPerfilResponse);
    }

    private static async Task<List<SemanaConsistenciaResponse>> LoadSemanasAsync(
        NpgsqlConnection connection,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        // date_trunc('week') segue a semana ISO, de segunda a domingo. A semana atual vem do fuso
        // dos grupos da pessoa, como as atividades; com fusos diferentes, vale o primeiro a virar
        // a semana. Sem grupo, vale o UTC.
        await using var command = new NpgsqlCommand($"""
            WITH hoje AS (
                SELECT COALESCE(
                    (SELECT max((now() AT TIME ZONE g.fuso_horario)::date)
                     FROM membros_grupo m
                     JOIN grupos g ON g.id = m.grupo_id
                     WHERE m.usuario_id = $1 AND m.removed_at IS NULL),
                    (now() AT TIME ZONE 'UTC')::date) AS dia
            ),
            semanas AS (
                SELECT generate_series(
                    date_trunc('week', hoje.dia) - interval '{SemanasNoGrafico - 1} weeks',
                    date_trunc('week', hoje.dia),
                    interval '1 week')::date AS inicio
                FROM hoje
            )
            SELECT s.inicio,
                count(a.id) FILTER (WHERE a.situacao_verificacao = 'Verificada')::int,
                count(a.id) FILTER (WHERE a.situacao_verificacao <> 'Rejeitada')::int
            FROM semanas s
            LEFT JOIN (atividades a JOIN grupos g ON g.id = a.grupo_id)
                ON a.usuario_id = $1
                AND date_trunc('week', a.started_at AT TIME ZONE g.fuso_horario)::date = s.inicio
            GROUP BY s.inicio
            ORDER BY s.inicio
            """, connection);
        command.Parameters.AddWithValue(usuarioId);

        var semanas = new List<SemanaConsistenciaResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            semanas.Add(new SemanaConsistenciaResponse(
                reader.GetFieldValue<DateOnly>(0),
                reader.GetInt32(1),
                reader.GetInt32(2)));
        }

        return semanas;
    }
}
