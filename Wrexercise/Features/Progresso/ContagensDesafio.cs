using Npgsql;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Progresso;

internal sealed record DesafioCompeticaoContext(
    Guid DesafioId,
    Guid GrupoId,
    DateOnly DataInicio,
    DateOnly DataFim,
    DateOnly Hoje,
    int SessoesPorSemana,
    bool Cancelado,
    DateOnly EntradaUsuario);

internal static class ContagensDesafio
{
    /// <returns><c>null</c> se o desafio não existir ou o usuário não for membro ativo.</returns>
    public static async Task<DesafioCompeticaoContext?> LoadDesafioAsync(
        NpgsqlConnection connection,
        Guid desafioId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT d.grupo_id, d.data_inicio, d.data_fim,
                (now() AT TIME ZONE g.fuso_horario)::date,
                d.sessoes_por_semana, d.cancelled_at IS NOT NULL,
                (m.joined_at AT TIME ZONE g.fuso_horario)::date
            FROM desafios d
            JOIN grupos g ON g.id = d.grupo_id
            JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            WHERE d.id = $1
            """, connection);
        command.Parameters.AddWithValue(desafioId);
        command.Parameters.AddWithValue(usuarioId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new DesafioCompeticaoContext(
            desafioId,
            reader.GetGuid(0),
            reader.GetFieldValue<DateOnly>(1),
            reader.GetFieldValue<DateOnly>(2),
            reader.GetFieldValue<DateOnly>(3),
            reader.GetInt32(4),
            reader.GetBoolean(5),
            reader.GetFieldValue<DateOnly>(6));
    }

    public static async Task<IReadOnlyList<ParticipanteDesafio>> LoadParticipantesAsync(
        NpgsqlConnection connection,
        Guid desafioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT m.usuario_id, u.nome_exibicao, (m.joined_at AT TIME ZONE g.fuso_horario)::date
            FROM desafios d
            JOIN grupos g ON g.id = d.grupo_id
            JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.removed_at IS NULL
            JOIN usuarios u ON u.id = m.usuario_id
            WHERE d.id = $1
              AND (m.joined_at AT TIME ZONE g.fuso_horario)::date <= d.data_fim - 6
            ORDER BY u.nome_exibicao, m.usuario_id
            """, connection);
        command.Parameters.AddWithValue(desafioId);

        var participantes = new List<ParticipanteDesafio>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            participantes.Add(new ParticipanteDesafio(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetFieldValue<DateOnly>(2)));
        }

        return participantes;
    }

    /// <summary>
    /// O tempo e a quantidade das atividades verificadas de cada pessoa que valem para o desafio, os
    /// desempates depois da sequência (ADR 0090). Mesma elegibilidade das contagens de dias.
    /// </summary>
    public static async Task<Dictionary<Guid, TotaisVerificados>> LoadTotaisVerificadosAsync(
        NpgsqlConnection connection,
        Guid desafioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT a.usuario_id, coalesce(sum(a.duracao_segundos), 0)::int, count(*)::int
            {ContagensAtividadeDesafio.FromElegiveisSql}
              AND a.situacao_verificacao = 'Verificada'
            GROUP BY a.usuario_id
            """, connection);
        command.Parameters.AddWithValue(desafioId);

        var totais = new Dictionary<Guid, TotaisVerificados>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            totais[reader.GetGuid(0)] = new TotaisVerificados(reader.GetInt32(1), reader.GetInt32(2));
        }

        return totais;
    }

    public static async Task<Dictionary<(Guid UsuarioId, int IndiceSemana), (int Verificadas, int Pendentes)>> LoadMatrizAsync(
        NpgsqlConnection connection,
        Guid desafioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            ContagensAtividadeDesafio.BuildAgregadoPorUsuarioESemana(),
            connection);
        command.Parameters.AddWithValue(desafioId);

        var matriz = new Dictionary<(Guid, int), (int, int)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            matriz[(reader.GetGuid(0), reader.GetInt32(1))] = (reader.GetInt32(2), reader.GetInt32(3));
        }

        return matriz;
    }
}
