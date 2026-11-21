using Npgsql;
using NpgsqlTypes;

namespace Wrexercise.Features.Atividades;

/// <summary>O resumo do Percurso na atividade e no Feed (ADR 0084). <c>VelocidadeMedia</c> em m/s.</summary>
internal sealed record PercursoResumoResponse(int DistanciaMetros, double? VelocidadeMedia, string? Recorde);

/// <summary>SQL do Percurso usado por mais de uma operação (ADR 0084).</summary>
internal static class ConsultasPercurso
{
    /// <summary>
    /// Junta o percurso (<c>pc</c>) e o melhor de antes da mesma pessoa no mesmo tipo (<c>ant</c>) à atividade
    /// <c>a</c>. Antes: treinos com percurso iniciados antes deste, que não foram rejeitados nem anulados.
    /// </summary>
    public const string JoinResumo = """
        LEFT JOIN percursos pc ON pc.atividade_id = a.id
        LEFT JOIN LATERAL (
            SELECT count(*)::int AS total,
                max(ap.distancia_metros) AS distancia_maxima,
                max(ap.distancia_metros::float8 / greatest(aa.duracao_segundos, 1))
                    FILTER (WHERE ap.distancia_metros >= CASE WHEN aa.tipo = 'Ciclismo' THEN 5000 ELSE 1000 END)
                    AS velocidade_maxima
            FROM atividades aa
            JOIN percursos ap ON ap.atividade_id = aa.id
            WHERE pc.atividade_id IS NOT NULL
              AND aa.usuario_id = a.usuario_id AND aa.tipo = a.tipo AND aa.started_at < a.started_at
              AND aa.situacao_verificacao NOT IN ('Rejeitada', 'Anulada', 'EmAndamento')
        ) ant ON true
        """;

    /// <summary>As colunas que <see cref="ReadResumo"/> lê, nesta ordem.</summary>
    public const string ColunasResumo =
        "pc.distancia_metros, ant.total, ant.distancia_maxima, ant.velocidade_maxima";

    /// <summary>
    /// Lê o resumo a partir de <paramref name="coluna"/> (as 4 de <see cref="ColunasResumo"/>). Rejeitada,
    /// anulada ou em andamento não tem recorde.
    /// </summary>
    public static PercursoResumoResponse? ReadResumo(
        NpgsqlDataReader reader,
        int coluna,
        string tipo,
        int duracaoSegundos,
        string situacao)
    {
        if (reader.IsDBNull(coluna))
        {
            return null;
        }

        var distancia = reader.GetInt32(coluna);
        var anteriores = new MelhoresAnteriores(
            reader.IsDBNull(coluna + 1) ? 0 : reader.GetInt32(coluna + 1),
            reader.IsDBNull(coluna + 2) ? null : reader.GetInt32(coluna + 2),
            reader.IsDBNull(coluna + 3) ? null : reader.GetDouble(coluna + 3));
        var recorde = situacao is SituacaoVerificacao.Rejeitada or SituacaoVerificacao.Anulada
            or SituacaoVerificacao.EmAndamento
            ? null
            : RecordesPercurso.Calcular(tipo, distancia, duracaoSegundos, anteriores);
        return new PercursoResumoResponse(
            distancia,
            duracaoSegundos > 0 && distancia > 0 ? (double)distancia / duracaoSegundos : null,
            recorde);
    }

    public static async Task<List<PontoPercurso>> CarregarPontosAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid atividadeId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT seq, recorded_at, latitude, longitude, precisao_metros, altitude_metros, segmento
            FROM pontos_percurso WHERE atividade_id = $1 ORDER BY seq
            """, connection, transaction);
        command.Parameters.AddWithValue(atividadeId);
        var pontos = new List<PontoPercurso>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            pontos.Add(new PontoPercurso(
                reader.GetInt32(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetDouble(2),
                reader.GetDouble(3),
                reader.GetFloat(4),
                reader.IsDBNull(5) ? null : reader.GetFloat(5),
                reader.GetInt16(6)));
        }

        return pontos;
    }

    /// <summary>
    /// O período em que os pontos contam: do início ao fim informado ao terminar, ou até agora (com folga) no treino
    /// ao vivo. Diminuir a duração ao terminar, ou numa correção, também corta o percurso.
    /// </summary>
    public static (DateTimeOffset Inicio, DateTimeOffset Fim) Periodo(
        string situacao,
        DateTimeOffset startedAt,
        int duracaoSegundos,
        DateTimeOffset agora) =>
        situacao == SituacaoVerificacao.EmAndamento
            ? (startedAt.AddSeconds(-60), agora.AddSeconds(60))
            : (startedAt.AddSeconds(-60), startedAt.AddSeconds(duracaoSegundos));

    /// <summary>
    /// Refaz o resumo guardado a partir dos pontos crus. Sem percurso, não faz nada e devolve <c>null</c>.
    /// </summary>
    public static async Task<CalculoPercurso?> RecalcularAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        CancellationToken cancellationToken)
    {
        string tipo;
        string situacao;
        DateTimeOffset startedAt;
        int duracao;
        await using (var load = new NpgsqlCommand("""
            SELECT a.tipo, a.situacao_verificacao, a.started_at, a.duracao_segundos
            FROM atividades a JOIN percursos p ON p.atividade_id = a.id
            WHERE a.id = $1
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(atividadeId);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            tipo = reader.GetString(0);
            situacao = reader.GetString(1);
            startedAt = reader.GetFieldValue<DateTimeOffset>(2);
            duracao = reader.GetInt32(3);
        }

        var pontos = await CarregarPontosAsync(connection, transaction, atividadeId, cancellationToken);
        var (inicio, fim) = Periodo(situacao, startedAt, duracao, DateTimeOffset.UtcNow);
        var calculo = PercursoAtividade.Calcular(pontos, tipo, inicio, fim);

        await using var update = new NpgsqlCommand("""
            UPDATE percursos
            SET distancia_metros = $2, velocidade_maxima = $3, ganho_elevacao_metros = $4, total_pontos = $5,
                updated_at = now()
            WHERE atividade_id = $1
            """, connection, transaction);
        update.Parameters.AddWithValue(atividadeId);
        update.Parameters.AddWithValue(calculo.DistanciaMetros);
        update.Parameters.AddWithValue(NpgsqlDbType.Double, (object?)calculo.VelocidadeMaxima ?? DBNull.Value);
        update.Parameters.AddWithValue(NpgsqlDbType.Integer, (object?)calculo.GanhoElevacaoMetros ?? DBNull.Value);
        update.Parameters.AddWithValue(pontos.Count);
        await update.ExecuteNonQueryAsync(cancellationToken);

        return calculo;
    }
}
