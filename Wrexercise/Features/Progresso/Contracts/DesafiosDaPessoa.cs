using Npgsql;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Progresso.Contracts;

/// <summary>Um desafio já iniciado de um grupo de que a pessoa é ou foi membro.</summary>
/// <param name="SemanasCumpridas">Índices (a partir de 0) das semanas em que a pessoa cumpriu a meta.</param>
internal sealed record DesafioDaPessoa(
    DateOnly DataInicio,
    DateOnly DataFim,
    DateOnly EntradaLocal,
    bool Encerrado,
    IReadOnlyList<int> SemanasCumpridas)
{
    /// <summary>Encerrado, com todas as semanas que contavam para a pessoa cumpridas.</summary>
    public bool IsConcluido()
    {
        if (!Encerrado)
        {
            return false;
        }

        var cumpridas = SemanasCumpridas.ToHashSet();
        var contavam = 0;
        for (var indice = 0; indice < CalculoSemanaDesafio.CountSemanas(DataInicio, DataFim); indice++)
        {
            if (!CalculoSemanaDesafio.ParticipaDaSemana(DataInicio.AddDays(indice * 7), EntradaLocal))
            {
                continue;
            }

            if (!cumpridas.Contains(indice))
            {
                return false;
            }

            contavam++;
        }

        return contavam > 0;
    }
}

/// <summary>
/// Regra única dos desafios da pessoa: a Energia (Jogo) e as conquistas (Usuarios) a usam, para que
/// "desafio concluído" e "melhor sequência" signifiquem o mesmo em todo lugar.
/// </summary>
internal static class DesafiosDaPessoa
{
    /// <summary>
    /// Os desafios já iniciados dos grupos de que a pessoa é ou foi membro, com as semanas em que
    /// cumpriu a meta, pelas mesmas regras de elegibilidade da classificação.
    /// </summary>
    public static async Task<List<DesafioDaPessoa>> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var desafios = new Dictionary<Guid, (DateOnly Inicio, DateOnly Fim, DateOnly Entrada, bool Encerrado)>();
        await using (var command = new NpgsqlCommand("""
            SELECT d.id, d.data_inicio, d.data_fim,
                (m.joined_at AT TIME ZONE g.fuso_horario)::date,
                (now() AT TIME ZONE g.fuso_horario)::date > d.data_fim
            FROM desafios d
            JOIN grupos g ON g.id = d.grupo_id
            JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.usuario_id = $1
            WHERE d.cancelled_at IS NULL
              AND d.data_inicio <= (now() AT TIME ZONE g.fuso_horario)::date
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(usuarioId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                desafios[reader.GetGuid(0)] = (
                    reader.GetFieldValue<DateOnly>(1),
                    reader.GetFieldValue<DateOnly>(2),
                    reader.GetFieldValue<DateOnly>(3),
                    reader.GetBoolean(4));
            }
        }

        var cumpridas = desafios.Keys.ToDictionary(id => id, _ => new List<int>());
        // Um dia conta quando as atividades verificadas cumprem a duração mínima ou uma meta por tipo
        // (decisões 0040 e 0043).
        await using (var command = new NpgsqlCommand($"""
            SELECT dias.desafio_id, dias.indice_semana
            FROM ({ContagensAtividadeDesafio.BuildDiasPorCondicao(DiasPorTipoSql)}) dias
            JOIN desafios d ON d.id = dias.desafio_id
            WHERE dias.conta_verificada
            GROUP BY dias.desafio_id, d.sessoes_por_semana, dias.indice_semana
            HAVING count(*) >= d.sessoes_por_semana
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(usuarioId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (cumpridas.TryGetValue(reader.GetGuid(0), out var indices))
                {
                    indices.Add(reader.GetInt32(1));
                }
            }
        }

        return desafios
            .Select(d => new DesafioDaPessoa(d.Value.Inicio, d.Value.Fim, d.Value.Entrada, d.Value.Encerrado, cumpridas[d.Key]))
            .ToList();
    }

    /// <summary>As atividades verificadas da pessoa ($1), por desafio, dia e tipo.</summary>
    private const string DiasPorTipoSql = $"""
                SELECT d.id AS desafio_id, a.usuario_id,
                    {ContagensAtividadeDesafio.DiaSql} AS dia,
                    {ContagensAtividadeDesafio.IndiceSemanaSql} AS indice_semana,
                    d.duracao_minima_segundos AS minimo,
                    a.tipo,
                    sum(a.duracao_segundos) AS segundos_verificados,
                    sum(a.duracao_segundos) AS segundos_provisorios
                FROM atividades a
                JOIN desafios d ON d.grupo_id = a.grupo_id AND d.cancelled_at IS NULL
                JOIN grupos g ON g.id = d.grupo_id
                JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.usuario_id = a.usuario_id
                WHERE a.usuario_id = $1
                  AND a.situacao_verificacao = 'Verificada'
                  AND {ContagensAtividadeDesafio.DiaSql} >= d.data_inicio
                  AND {ContagensAtividadeDesafio.DiaSql} <= d.data_fim
                  AND d.data_inicio + {ContagensAtividadeDesafio.IndiceSemanaSql} * 7 >= (m.joined_at AT TIME ZONE g.fuso_horario)::date
                GROUP BY d.id, a.usuario_id, d.duracao_minima_segundos, a.tipo,
                    {ContagensAtividadeDesafio.DiaSql}, {ContagensAtividadeDesafio.IndiceSemanaSql}
                """;

    /// <summary>A melhor sequência de semanas cumpridas em um único desafio.</summary>
    public static int GetMelhorSequencia(IEnumerable<DesafioDaPessoa> desafios) =>
        desafios.Select(d => CalculoSemanaDesafio.GetMelhorSequencia(d.SemanasCumpridas)).DefaultIfEmpty(0).Max();
}
