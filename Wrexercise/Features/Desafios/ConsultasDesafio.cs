using Npgsql;

namespace Wrexercise.Features.Desafios;

internal sealed record EstadoDesafio(Guid GrupoId, string Situacao);

/// <summary>
/// Toda consulta parte do vínculo de quem pergunta: sem ser membro ativo do grupo,
/// o desafio simplesmente não aparece.
/// </summary>
internal static class ConsultasDesafio
{
    // Participa quem é membro ativo e entrou antes da última semana começar.
    public const string SelectDesafio = """
        SELECT d.id, d.grupo_id, d.nome, d.descricao, d.data_inicio, d.data_fim,
            d.duracao_minima_segundos, d.sessoes_por_semana, d.cancelled_at IS NOT NULL,
            (now() AT TIME ZONE g.fuso_horario)::date,
            g.fuso_horario, m.papel,
            (m.joined_at AT TIME ZONE g.fuso_horario)::date <= d.data_fim - 6,
            (SELECT count(*)::int
             FROM membros_grupo t
             WHERE t.grupo_id = d.grupo_id
               AND t.removed_at IS NULL
               AND (t.joined_at AT TIME ZONE g.fuso_horario)::date <= d.data_fim - 6)
        FROM desafios d
        JOIN grupos g ON g.id = d.grupo_id
        JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.usuario_id = $1 AND m.removed_at IS NULL
        """;

    /// <returns>O desafio, ou <c>null</c> se ele não existir ou o usuário não for membro do grupo.</returns>
    public static async Task<DesafioResponse?> GetAsync(
        NpgsqlDataSource dataSource,
        Guid desafioId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(SelectDesafio + """

            WHERE d.id = $2
            """);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(desafioId);

        DesafioResponse desafio;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            desafio = ReadDesafio(reader);
        }

        var metas = await LoadMetasPorTipoAsync(dataSource, [desafio.Id], cancellationToken);
        return WithMetasPorTipo(desafio, metas);
    }

    /// <summary>As metas por tipo dos desafios, na ordem de criação.</summary>
    public static async Task<Dictionary<Guid, List<MetaPorTipoResponse>>> LoadMetasPorTipoAsync(
        NpgsqlDataSource dataSource,
        Guid[] desafioIds,
        CancellationToken cancellationToken)
    {
        var metas = new Dictionary<Guid, List<MetaPorTipoResponse>>();
        if (desafioIds.Length == 0)
        {
            return metas;
        }

        await using var command = dataSource.CreateCommand("""
            SELECT desafio_id, id, nome, tipos, duracao_minima_segundos
            FROM metas_tipo_desafio
            WHERE desafio_id = ANY($1)
            ORDER BY desafio_id, ordem
            """);
        command.Parameters.AddWithValue(desafioIds);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var desafioId = reader.GetGuid(0);
            if (!metas.TryGetValue(desafioId, out var lista))
            {
                lista = [];
                metas[desafioId] = lista;
            }

            lista.Add(new MetaPorTipoResponse(
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetFieldValue<string[]>(3),
                reader.GetInt32(4)));
        }

        return metas;
    }

    public static DesafioResponse WithMetasPorTipo(
        DesafioResponse desafio,
        Dictionary<Guid, List<MetaPorTipoResponse>> metas) =>
        metas.TryGetValue(desafio.Id, out var lista) ? desafio with { MetasPorTipo = lista } : desafio;

    public static DesafioOperacaoStatus? GetBloqueio(string situacao) => situacao switch
    {
        SituacaoDesafio.Encerrado => DesafioOperacaoStatus.DesafioEncerrado,
        SituacaoDesafio.Cancelado => DesafioOperacaoStatus.DesafioCancelado,
        _ => null,
    };

    public static async Task<EstadoDesafio?> GetEstadoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid desafioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT d.grupo_id, d.data_inicio, d.data_fim, d.cancelled_at IS NOT NULL,
                (now() AT TIME ZONE g.fuso_horario)::date
            FROM desafios d
            JOIN grupos g ON g.id = d.grupo_id
            WHERE d.id = $1
            """, connection, transaction);
        command.Parameters.AddWithValue(desafioId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new EstadoDesafio(
                reader.GetGuid(0),
                ProgressoDesafio.GetSituacao(
                    reader.GetFieldValue<DateOnly>(1),
                    reader.GetFieldValue<DateOnly>(2),
                    reader.GetBoolean(3),
                    reader.GetFieldValue<DateOnly>(4)))
            : null;
    }

    /// <summary>Lê a linha de <see cref="SelectDesafio"/>; as metas por tipo vêm de <see cref="LoadMetasPorTipoAsync"/>.</summary>
    public static DesafioResponse ReadDesafio(NpgsqlDataReader reader)
    {
        var inicio = reader.GetFieldValue<DateOnly>(4);
        var fim = reader.GetFieldValue<DateOnly>(5);

        return new DesafioResponse(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            inicio,
            fim,
            reader.GetInt32(6),
            reader.GetInt32(7),
            ProgressoDesafio.GetSituacao(inicio, fim, reader.GetBoolean(8), reader.GetFieldValue<DateOnly>(9)),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetBoolean(12),
            reader.GetInt32(13),
            []);
    }
}
