using Npgsql;
using Wrexercise.Features.Grupos.Contracts;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// O autor apaga a própria atividade nos primeiros 15 minutos, enquanto ninguém a contestou (ADR 0054).
/// A exclusão é definitiva: reações, comentários, notificações, avisos e fotos saem junto.
/// </summary>
internal static class ExcluirAtividade
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoEvidencias armazenamento,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var (status, arquivos) = await ExecutarAsync(dataSource, id, usuarioId, cancellationToken);
        if (status != AtividadeOperacaoStatus.Concluida)
        {
            return OperacaoAtividade.ToResult(status);
        }

        // Depois do commit: se a transação falhasse, as fotos continuariam valendo.
        foreach (var arquivo in arquivos)
        {
            armazenamento.ExcluirSeExistir(arquivo);
        }

        return Results.NoContent();
    }

    private static async Task<(AtividadeOperacaoStatus Status, List<string> Arquivos)> ExecutarAsync(
        NpgsqlDataSource dataSource,
        Guid atividadeId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid autorId;
        string situacao;
        DateTimeOffset submittedAt;

        // FOR UPDATE: uma reação ou foto que chega ao mesmo tempo espera a exclusão e não acha mais a atividade.
        await using (var load = new NpgsqlCommand("""
            SELECT a.usuario_id, a.situacao_verificacao, a.submitted_at
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
                return (AtividadeOperacaoStatus.AtividadeNaoEncontrada, []);
            }

            autorId = reader.GetGuid(0);
            situacao = reader.GetString(1);
            submittedAt = reader.GetFieldValue<DateTimeOffset>(2);
        }

        if (autorId != usuarioId)
        {
            return (AtividadeOperacaoStatus.AcessoNegado, []);
        }

        if (ExclusaoAtividade.PermitidaAte(true, situacao, submittedAt, DateTimeOffset.UtcNow) is null)
        {
            return (AtividadeOperacaoStatus.ExclusaoNaoPermitida, []);
        }

        // O aviso FotoParecida de outra atividade continua, só sem o link para esta.
        await using (var desvincular = new NpgsqlCommand(
            "UPDATE avisos_atividade SET atividade_relacionada_id = NULL WHERE atividade_relacionada_id = $1",
            connection,
            transaction))
        {
            desvincular.Parameters.AddWithValue(atividadeId);
            await desvincular.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var avisos = new NpgsqlCommand(
            "DELETE FROM avisos_atividade WHERE atividade_id = $1", connection, transaction))
        {
            avisos.Parameters.AddWithValue(atividadeId);
            await avisos.ExecuteNonQueryAsync(cancellationToken);
        }

        var arquivos = new List<string>();
        await using (var evidencias = new NpgsqlCommand("""
            DELETE FROM evidencias_atividade WHERE atividade_id = $1
            RETURNING caminho_relativo, caminho_miniatura
            """, connection, transaction))
        {
            evidencias.Parameters.AddWithValue(atividadeId);
            await using var reader = await evidencias.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                arquivos.Add(reader.GetString(0));
                if (!reader.IsDBNull(1))
                {
                    arquivos.Add(reader.GetString(1));
                }
            }
        }

        // Revisores, reações, comentários, notificações e histórico saem por ON DELETE CASCADE.
        await using (var excluir = new NpgsqlCommand("DELETE FROM atividades WHERE id = $1", connection, transaction))
        {
            excluir.Parameters.AddWithValue(atividadeId);
            await excluir.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return (AtividadeOperacaoStatus.Concluida, arquivos);
    }
}
