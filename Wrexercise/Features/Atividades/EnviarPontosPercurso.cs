using Npgsql;
using NpgsqlTypes;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// Um lote de pontos do Percurso durante o treino ao vivo (ADR 0084). Só o autor, só com o treino aberto e só se
/// ele escolheu gravar o percurso. Repetir o lote não duplica nada; pontos inválidos são descartados em silêncio.
/// As coordenadas nunca vão para o log. O app conectado manda pelo mesmo caminho (<see cref="GravarAsync"/>,
/// ADR 0085).
/// </summary>
internal static class EnviarPontosPercurso
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        EnviarPontosPercursoRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!IsLoteValido(request))
        {
            return OperacaoAtividade.InvalidData();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid autorId;
        string situacao;
        DateTimeOffset startedAt;
        int? totalPontos;

        // FOR UPDATE: o mesmo bloqueio do término; um lote que chega ao terminar espera ou é recusado.
        await using (var load = new NpgsqlCommand("""
            SELECT a.usuario_id, a.situacao_verificacao, a.started_at, p.total_pontos
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            LEFT JOIN percursos p ON p.atividade_id = a.id
            WHERE a.id = $1
            FOR UPDATE OF a
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(id);
            load.Parameters.AddWithValue(usuarioId);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return OperacaoAtividade.AtividadeNaoEncontrada();
            }

            autorId = reader.GetGuid(0);
            situacao = reader.GetString(1);
            startedAt = reader.GetFieldValue<DateTimeOffset>(2);
            totalPontos = reader.IsDBNull(3) ? null : reader.GetInt32(3);
        }

        if (autorId != usuarioId)
        {
            return OperacaoAtividade.ToResult(AtividadeOperacaoStatus.AcessoNegado);
        }

        var (status, resposta) = await GravarAsync(
            connection, transaction, id, situacao, startedAt, totalPontos, request.Pontos!, cancellationToken);
        if (status != AtividadeOperacaoStatus.Concluida)
        {
            return OperacaoAtividade.ToResult(status);
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.Json(resposta, AppJsonContext.Default.PontosPercursoResponse);
    }

    /// <summary>Um lote com 1 a 500 pontos.</summary>
    public static bool IsLoteValido(EnviarPontosPercursoRequest request) =>
        request.Pontos is { Count: > 0 and <= PercursoAtividade.MaxPontosPorLote };

    /// <summary>
    /// Guarda os pontos válidos de um treino já carregado (e bloqueado) por quem chama e refaz o resumo. Quem chama
    /// faz o commit quando o status é <see cref="AtividadeOperacaoStatus.Concluida"/>.
    /// </summary>
    /// <param name="totalPontos">Os pontos já guardados, ou <c>null</c> quando o treino não grava percurso.</param>
    public static async Task<(AtividadeOperacaoStatus Status, PontosPercursoResponse? Resposta)> GravarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string situacao,
        DateTimeOffset startedAt,
        int? totalPontos,
        IReadOnlyList<PontoPercursoRequest> lote,
        CancellationToken cancellationToken)
    {
        if (situacao != SituacaoVerificacao.EmAndamento)
        {
            return (AtividadeOperacaoStatus.TreinoNaoEmAndamento, null);
        }

        if (totalPontos is not { } total)
        {
            return (AtividadeOperacaoStatus.PercursoNaoEncontrado, null);
        }

        var agora = DateTimeOffset.UtcNow;
        var validos = lote
            .Select(ToPonto)
            .OfType<PontoPercurso>()
            .Where(p => PercursoAtividade.IsPontoValido(p, startedAt, agora))
            .DistinctBy(p => p.Seq)
            .ToList();

        if (total + validos.Count > PercursoAtividade.MaxPontosPorAtividade)
        {
            return (AtividadeOperacaoStatus.PercursoCheio, null);
        }

        if (validos.Count > 0)
        {
            await using var batch = new NpgsqlBatch(connection, transaction);
            foreach (var ponto in validos)
            {
                var insert = new NpgsqlBatchCommand("""
                    INSERT INTO pontos_percurso (
                        atividade_id, seq, recorded_at, latitude, longitude, precisao_metros, altitude_metros, segmento)
                    VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                    ON CONFLICT (atividade_id, seq) DO NOTHING
                    """);
                insert.Parameters.AddWithValue(id);
                insert.Parameters.AddWithValue(ponto.Seq);
                insert.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, ponto.Em.ToUniversalTime());
                insert.Parameters.AddWithValue(ponto.Latitude);
                insert.Parameters.AddWithValue(ponto.Longitude);
                insert.Parameters.AddWithValue((float)ponto.Precisao);
                insert.Parameters.AddWithValue(
                    NpgsqlDbType.Real, ponto.Altitude is { } altitude ? (float)altitude : (object)DBNull.Value);
                insert.Parameters.AddWithValue((short)ponto.Segmento);
                batch.BatchCommands.Add(insert);
            }

            await batch.ExecuteNonQueryAsync(cancellationToken);
        }

        var calculo = await ConsultasPercurso.RecalcularAsync(connection, transaction, id, cancellationToken);

        await using var resumo = new NpgsqlCommand(
            "SELECT coalesce(max(seq), -1), count(*)::int FROM pontos_percurso WHERE atividade_id = $1",
            connection,
            transaction);
        resumo.Parameters.AddWithValue(id);
        await using var reader = await resumo.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (
            AtividadeOperacaoStatus.Concluida,
            new PontosPercursoResponse(reader.GetInt32(0), calculo?.DistanciaMetros ?? 0, reader.GetInt32(1)));
    }

    private static PontoPercurso? ToPonto(PontoPercursoRequest p) =>
        p is { Seq: { } seq, Em: { } em, Latitude: { } lat, Longitude: { } lon, Precisao: { } precisao, Segmento: { } segmento }
            ? new PontoPercurso(seq, em, lat, lon, precisao, p.Altitude, segmento)
            : null;
}
