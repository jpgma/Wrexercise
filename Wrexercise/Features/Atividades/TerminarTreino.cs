using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Atividades;

internal sealed record TerminarTreinoRequest(int? DuracaoSegundos, string? Descricao, string? TipoComprovacao);

/// <summary>
/// "Terminar treino" (ADR 0078): o treino ao vivo vira um registro comum. O envio passa a ser agora, então a
/// janela de avaliação, a exclusão em 15 minutos e o RegistroTardio contam a partir daqui, como num registro.
/// </summary>
internal static class TerminarTreino
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        TerminarTreinoRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!ValidacaoAtividade.IsValidDuracao(request.DuracaoSegundos)
            || !ValidacaoAtividade.IsValidDescricao(request.Descricao)
            || !TipoComprovacao.IsValid(request.TipoComprovacao))
        {
            return OperacaoAtividade.InvalidData();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var status = await ExecutarAsync(dataSource, id, usuarioId, request, cancellationToken);
        if (status != AtividadeOperacaoStatus.Concluida)
        {
            return OperacaoAtividade.ToResult(status);
        }

        var atividade = await ConsultasAtividade.GetDetalheAsync(dataSource, id, usuarioId, cancellationToken);
        return atividade is null
            ? OperacaoAtividade.AtividadeNaoEncontrada()
            : Results.Json(atividade, AppJsonContext.Default.AtividadeResponse);
    }

    private static async Task<AtividadeOperacaoStatus> ExecutarAsync(
        NpgsqlDataSource dataSource,
        Guid atividadeId,
        Guid usuarioId,
        TerminarTreinoRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid autorId;
        Guid grupoId;
        string situacao;
        DateTimeOffset startedAt;
        int totalEvidencias;

        // FOR UPDATE: uma foto que chega ao mesmo tempo espera, e o cancelamento não cruza com o término.
        await using (var load = new NpgsqlCommand("""
            SELECT a.usuario_id, a.grupo_id, a.situacao_verificacao, a.started_at,
                (SELECT count(*)::int FROM evidencias_atividade e WHERE e.atividade_id = a.id)
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.usuario_id = $2 AND m.removed_at IS NULL
            WHERE a.id = $1
            FOR UPDATE OF a
            """, connection, transaction))
        {
            load.Parameters.AddWithValue(atividadeId);
            load.Parameters.AddWithValue(usuarioId);
            await using var reader = await load.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return AtividadeOperacaoStatus.AtividadeNaoEncontrada;
            }

            autorId = reader.GetGuid(0);
            grupoId = reader.GetGuid(1);
            situacao = reader.GetString(2);
            startedAt = reader.GetFieldValue<DateTimeOffset>(3);
            totalEvidencias = reader.GetInt32(4);
        }

        if (autorId != usuarioId)
        {
            return AtividadeOperacaoStatus.AcessoNegado;
        }

        if (situacao != SituacaoVerificacao.EmAndamento)
        {
            return AtividadeOperacaoStatus.TreinoNaoEmAndamento;
        }

        var agora = DateTimeOffset.UtcNow;
        if (!TreinoAoVivo.IsValidDuracaoFinal(request.DuracaoSegundos, startedAt, agora)
            || !TreinoAoVivo.IsValidComprovacaoFinal(request.TipoComprovacao, totalEvidencias))
        {
            return AtividadeOperacaoStatus.DadosInvalidos;
        }

        var duracao = request.DuracaoSegundos!.Value;
        DateTimeOffset submittedAt;
        await using (var update = new NpgsqlCommand("""
            UPDATE atividades
            SET duracao_segundos = $2, descricao = $3, tipo_comprovacao = $4,
                situacao_verificacao = 'AguardandoFeedback', submitted_at = now()
            WHERE id = $1
            RETURNING submitted_at
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(atividadeId);
            update.Parameters.AddWithValue(duracao);
            update.Parameters.Add(new NpgsqlParameter
            {
                Value = (object?)ValidacaoAtividade.NormalizeDescricao(request.Descricao) ?? DBNull.Value,
            });
            update.Parameters.AddWithValue(request.TipoComprovacao!);
            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            submittedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await RecalcularAvisosDataAsync(connection, transaction, atividadeId, startedAt, duracao, cancellationToken);

        // Diminuir a duração corta também o Percurso: o que veio depois do fim informado não conta (ADR 0084).
        await ConsultasPercurso.RecalcularAsync(connection, transaction, atividadeId, cancellationToken);

        await RegistroAtividade.FinalizarAsync(
            connection,
            transaction,
            atividadeId,
            grupoId,
            usuarioId,
            startedAt,
            duracao,
            submittedAt,
            totalEvidencias > 0,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return AtividadeOperacaoStatus.Concluida;
    }

    /// <summary>
    /// Durante o treino, a data das fotos foi comparada com o tempo decorrido. Com a duração final, o período
    /// pode ter encolhido: os avisos de data são refeitos com ele.
    /// </summary>
    private static async Task RecalcularAvisosDataAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        DateTimeOffset startedAt,
        int duracao,
        CancellationToken cancellationToken)
    {
        await using (var limpar = new NpgsqlCommand($"""
            DELETE FROM avisos_atividade
            WHERE atividade_id = $1 AND tipo IN ('{TipoAviso.FotoForaDoPeriodo}', '{TipoAviso.FotoSemData}')
            """, connection, transaction))
        {
            limpar.Parameters.AddWithValue(atividadeId);
            await limpar.ExecuteNonQueryAsync(cancellationToken);
        }

        var evidencias = new List<(Guid Id, DateTimeOffset? CapturadaEm, string? Origem)>();
        await using (var lista = new NpgsqlCommand("""
            SELECT id, capturada_em, origem_data_captura FROM evidencias_atividade WHERE atividade_id = $1
            ORDER BY ordem
            """, connection, transaction))
        {
            lista.Parameters.AddWithValue(atividadeId);
            await using var reader = await lista.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                evidencias.Add((
                    reader.GetGuid(0),
                    reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
        }

        var agora = DateTimeOffset.UtcNow;
        foreach (var (evidenciaId, capturadaEm, origem) in evidencias)
        {
            if (AvisosEvidencia.CalcularAvisoData(startedAt, duracao, capturadaEm, origem, agora) is { } aviso)
            {
                await ConsultasAtividade.InserirAvisoAsync(
                    connection, transaction, atividadeId, aviso, evidenciaId, null, cancellationToken);
            }
        }
    }
}
