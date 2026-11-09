using Npgsql;
using Wrexercise.Features.Desafios.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Desafios;

internal static class ObterProgresso
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var progresso = await GetProgressoAsync(
            dataSource,
            id,
            context.GetSessaoAtual().UsuarioId,
            cancellationToken);

        return progresso is null ? OperacaoDesafio.DesafioNaoEncontrado() : Results.Ok(progresso);
    }

    /// <returns>O progresso, ou <c>null</c> se o desafio não existir ou o usuário não for membro do grupo.</returns>
    private static async Task<ProgressoResponse?> GetProgressoAsync(
        NpgsqlDataSource dataSource,
        Guid desafioId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        DateOnly inicio;
        DateOnly fim;
        DateOnly hoje;
        int sessoesPorSemana;
        bool cancelado;
        await using (var desafio = new NpgsqlCommand("""
            SELECT d.data_inicio, d.data_fim, (now() AT TIME ZONE g.fuso_horario)::date,
                d.sessoes_por_semana, d.cancelled_at IS NOT NULL
            FROM desafios d
            JOIN grupos g ON g.id = d.grupo_id
            JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            WHERE d.id = $1
            """, connection))
        {
            desafio.Parameters.AddWithValue(desafioId);
            desafio.Parameters.AddWithValue(usuarioId);

            await using var reader = await desafio.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            inicio = reader.GetFieldValue<DateOnly>(0);
            fim = reader.GetFieldValue<DateOnly>(1);
            hoje = reader.GetFieldValue<DateOnly>(2);
            sessoesPorSemana = reader.GetInt32(3);
            cancelado = reader.GetBoolean(4);
        }

        await using var participantes = new NpgsqlCommand("""
            SELECT m.usuario_id, u.nome_exibicao, (m.joined_at AT TIME ZONE g.fuso_horario)::date
            FROM desafios d
            JOIN grupos g ON g.id = d.grupo_id
            JOIN membros_grupo m ON m.grupo_id = d.grupo_id AND m.removed_at IS NULL
            JOIN usuarios u ON u.id = m.usuario_id
            WHERE d.id = $1
              AND (m.joined_at AT TIME ZONE g.fuso_horario)::date <= d.data_fim - 6
            ORDER BY u.nome_exibicao, m.usuario_id
            """, connection);
        participantes.Parameters.AddWithValue(desafioId);

        var semana = cancelado ? null : ProgressoDesafio.GetSemana(inicio, fim, hoje);
        var contagens = semana is null
            ? new Dictionary<Guid, (int Verificadas, int Pendentes)>()
            : await LoadContagensSemanaAsync(connection, desafioId, semana.Inicio, semana.Fim, cancellationToken);

        var progresso = new List<ProgressoParticipanteResponse>();
        await using (var reader = await participantes.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var participanteId = reader.GetGuid(0);
                contagens.TryGetValue(participanteId, out var contagem);
                var calculado = ProgressoDesafio.CalculateProgresso(
                    contagem.Verificadas,
                    contagem.Pendentes,
                    sessoesPorSemana);
                progresso.Add(new ProgressoParticipanteResponse(
                    participanteId,
                    reader.GetString(1),
                    calculado.Verificadas,
                    calculado.Provisorias,
                    calculado.MetaCumprida,
                    semana is null
                        || CalculoSemanaDesafio.ParticipaDaSemana(semana.Inicio, reader.GetFieldValue<DateOnly>(2))));
            }
        }

        var seusDias = semana is null
            ? []
            : await LoadDiasDaSemanaAsync(connection, desafioId, semana.Inicio, semana.Fim, usuarioId, cancellationToken);

        return new ProgressoResponse(
            sessoesPorSemana,
            ProgressoDesafio.CountSemanas(inicio, fim),
            semana,
            progresso,
            seusDias.Where(d => d.Conta).Select(d => d.Data).ToList(),
            seusDias.Select(d => new SegundosDoDiaResponse(d.Data, d.Segundos)).ToList());
    }

    private static async Task<List<(DateOnly Data, int Segundos, bool Conta)>> LoadDiasDaSemanaAsync(
        NpgsqlConnection connection,
        Guid desafioId,
        DateOnly inicioSemana,
        DateOnly fimSemana,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            ContagensAtividadeDesafio.BuildDiasDaSemana(),
            connection);
        command.Parameters.AddWithValue(desafioId);
        command.Parameters.AddWithValue(inicioSemana);
        command.Parameters.AddWithValue(fimSemana);
        command.Parameters.AddWithValue(usuarioId);

        var dias = new List<(DateOnly Data, int Segundos, bool Conta)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            dias.Add((reader.GetFieldValue<DateOnly>(0), reader.GetInt32(1), reader.GetBoolean(2)));
        }

        return dias;
    }

    private static async Task<Dictionary<Guid, (int Verificadas, int Pendentes)>> LoadContagensSemanaAsync(
        NpgsqlConnection connection,
        Guid desafioId,
        DateOnly inicioSemana,
        DateOnly fimSemana,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            ContagensAtividadeDesafio.BuildAgregadoPorUsuarioSemanaAtual(),
            connection);
        command.Parameters.AddWithValue(desafioId);
        command.Parameters.AddWithValue(inicioSemana);
        command.Parameters.AddWithValue(fimSemana);

        var contagens = new Dictionary<Guid, (int Verificadas, int Pendentes)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            contagens[reader.GetGuid(0)] = (reader.GetInt32(1), reader.GetInt32(2));
        }

        return contagens;
    }
}
