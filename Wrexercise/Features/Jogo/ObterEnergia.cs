using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

internal static class ObterEnergia
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        // Só lê os dados de quem consulta, em todos os grupos: não depende de um grupo da rota.
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var atividades = await LoadAtividadesAsync(connection, usuarioId, cancellationToken);
        var desafios = await LoadDesafiosAsync(connection, usuarioId, cancellationToken);
        var ganha = CalculoEnergia.Calcular(atividades, desafios);

        // G3: subtrair a soma de cartas.preco das cartas compradas.
        var saldo = ganha.Total;

        return Results.Json(
            new EnergiaResponse(
                saldo,
                new EnergiaDetalhamentoResponse(
                    ganha.Atividades, ganha.Bonus, ganha.Metas, ganha.Sequencias, ganha.Desafios)),
            AppJsonContext.Default.EnergiaResponse);
    }

    /// <summary>
    /// As atividades não rejeitadas, inclusive de grupos que a pessoa deixou, em ordem de registro
    /// (decisão 0034): uma atividade retroativa não toma o bônus de outra já verificada.
    /// </summary>
    private static async Task<List<AtividadeEnergia>> LoadAtividadesAsync(
        NpgsqlConnection connection,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT a.started_at AT TIME ZONE g.fuso_horario, a.tipo, a.situacao_verificacao = 'Verificada'
            FROM atividades a
            JOIN grupos g ON g.id = a.grupo_id
            WHERE a.usuario_id = $1 AND a.situacao_verificacao <> 'Rejeitada'
            ORDER BY a.created_at, a.id
            """, connection);
        command.Parameters.AddWithValue(usuarioId);

        var atividades = new List<AtividadeEnergia>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            atividades.Add(new AtividadeEnergia(reader.GetDateTime(0), reader.GetString(1), reader.GetBoolean(2)));
        }

        return atividades;
    }

    /// <summary>
    /// Os desafios já iniciados dos grupos de que a pessoa é ou foi membro, com as semanas em que
    /// cumpriu a meta, pelas mesmas regras de elegibilidade da classificação.
    /// </summary>
    private static async Task<List<DesafioEnergia>> LoadDesafiosAsync(
        NpgsqlConnection connection,
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
            """, connection))
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
        await using (var command = new NpgsqlCommand($"""
            SELECT d.id, {ContagensAtividadeDesafio.IndiceSemanaSql}
            FROM atividades a
            JOIN desafios d ON d.grupo_id = a.grupo_id AND d.cancelled_at IS NULL
            JOIN grupos g ON g.id = d.grupo_id
            JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.usuario_id = a.usuario_id
            WHERE a.usuario_id = $1
              AND a.situacao_verificacao = 'Verificada'
              AND a.duracao_segundos >= d.duracao_minima_segundos
              AND (a.started_at AT TIME ZONE g.fuso_horario)::date >= d.data_inicio
              AND (a.started_at AT TIME ZONE g.fuso_horario)::date <= d.data_fim
              AND d.data_inicio + {ContagensAtividadeDesafio.IndiceSemanaSql} * 7 >= (m.joined_at AT TIME ZONE g.fuso_horario)::date
            GROUP BY d.id, d.sessoes_por_semana, {ContagensAtividadeDesafio.IndiceSemanaSql}
            HAVING count(*) >= d.sessoes_por_semana
            """, connection))
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
            .Select(d => new DesafioEnergia(d.Value.Inicio, d.Value.Fim, d.Value.Entrada, d.Value.Encerrado, cumpridas[d.Key]))
            .ToList();
    }
}
