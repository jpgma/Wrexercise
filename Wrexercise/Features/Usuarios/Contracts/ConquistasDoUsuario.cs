using Npgsql;
using Wrexercise.Features.Progresso.Contracts;

namespace Wrexercise.Features.Usuarios.Contracts;

/// <summary>
/// As conquistas de uma pessoa, calculadas na leitura sobre as atividades <c>Verificada</c> de todos os
/// grupos (inclusive os que ela deixou), com horários no fuso do grupo de cada atividade.
/// O Perfil e o Jogo (Enfeites) usam a mesma regra.
/// </summary>
internal static class ConquistasDoUsuario
{
    public static async Task<IReadOnlyList<string>> LoadCodigosAsync(
        NpgsqlConnection connection,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var estatisticas = await LoadEstatisticasAsync(connection, usuarioId, cancellationToken);
        return ConquistasUsuario.Calcular(estatisticas).Select(c => c.Codigo).ToList();
    }

    internal static async Task<EstatisticasConquistas> LoadEstatisticasAsync(
        NpgsqlConnection connection,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var porTipo = new Dictionary<string, int>();
        var antesDas7 = 0;
        var noturnos = 0;
        var fimDeSemana = 0;

        await using (var command = new NpgsqlCommand($"""
            SELECT a.tipo,
                count(*)::int,
                count(*) FILTER (WHERE (a.started_at AT TIME ZONE g.fuso_horario)::time < time '07:00')::int,
                count(*) FILTER (WHERE (a.started_at AT TIME ZONE g.fuso_horario)::time >= time '{ConquistasUsuario.HoraNoturno}:00')::int,
                count(*) FILTER (WHERE extract(isodow FROM a.started_at AT TIME ZONE g.fuso_horario) >= 6)::int
            FROM atividades a
            JOIN grupos g ON g.id = a.grupo_id
            WHERE a.usuario_id = $1 AND a.situacao_verificacao = 'Verificada'
            GROUP BY a.tipo
            """, connection))
        {
            command.Parameters.AddWithValue(usuarioId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                porTipo[reader.GetString(0)] = reader.GetInt32(1);
                antesDas7 += reader.GetInt32(2);
                noturnos += reader.GetInt32(3);
                fimDeSemana += reader.GetInt32(4);
            }
        }

        int diasComTreino;
        await using (var command = new NpgsqlCommand("""
            SELECT (count(*) FILTER (WHERE d.dia < $2)
                + count(DISTINCT (d.grupo_id, d.dia)) FILTER (WHERE d.dia >= $2))::int
            FROM (
                SELECT a.grupo_id, (a.started_at AT TIME ZONE g.fuso_horario)::date AS dia
                FROM atividades a
                JOIN grupos g ON g.id = a.grupo_id
                WHERE a.usuario_id = $1 AND a.situacao_verificacao = 'Verificada'
            ) d
            """, connection))
        {
            command.Parameters.AddWithValue(usuarioId);
            command.Parameters.AddWithValue(ConquistasUsuario.InicioTreinosPorDia);
            diasComTreino = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        var desafios = await DesafiosDaPessoa.LoadAsync(connection, null, usuarioId, cancellationToken);

        return new EstatisticasConquistas(
            porTipo.Values.Sum(),
            DesafiosDaPessoa.GetMelhorSequencia(desafios),
            antesDas7,
            noturnos,
            fimDeSemana,
            desafios.Count(d => d.IsConcluido()),
            porTipo,
            diasComTreino);
    }
}
