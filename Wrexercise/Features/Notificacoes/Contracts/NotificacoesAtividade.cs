using Npgsql;

namespace Wrexercise.Features.Notificacoes.Contracts;

/// <summary>
/// Grava notificações na transação de quem chamou: o evento e o aviso acontecem juntos ou não acontecem.
/// </summary>
internal static class NotificacoesAtividade
{
    private static readonly TimeSpan Retencao = TimeSpan.FromDays(90);

    /// <summary>Avisa os revisores de que há um treino para avaliar.</summary>
    public static async Task NotificarNovaAtividadeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO notificacoes (usuario_id, grupo_id, atividade_id, tipo, ator_id)
            SELECT r.usuario_id, a.grupo_id, a.id, 'NovaAtividade', a.usuario_id
            FROM revisores_atividade r
            JOIN atividades a ON a.id = r.atividade_id
            WHERE r.atividade_id = $1
            """, connection, transaction);
        command.Parameters.AddWithValue(atividadeId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Avisa o autor. Reações seguidas, enquanto não lidas, somam na mesma notificação.</summary>
    public static async Task NotificarReacaoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        Guid atorId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO notificacoes (usuario_id, grupo_id, atividade_id, tipo, ator_id)
            SELECT a.usuario_id, a.grupo_id, a.id, 'Reacao', $2
            FROM atividades a
            WHERE a.id = $1
            ON CONFLICT (usuario_id, atividade_id, tipo) WHERE read_at IS NULL AND tipo IN ('Reacao', 'Comentario')
            DO UPDATE SET total = notificacoes.total + 1, ator_id = EXCLUDED.ator_id, updated_at = now()
            """, connection, transaction);
        command.Parameters.AddWithValue(atividadeId);
        command.Parameters.AddWithValue(atorId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Avisa o autor e quem já comentou, menos quem acabou de escrever.</summary>
    public static async Task NotificarComentarioAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        Guid atorId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO notificacoes (usuario_id, grupo_id, atividade_id, tipo, ator_id)
            SELECT m.usuario_id, a.grupo_id, a.id, 'Comentario', $2
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.removed_at IS NULL
            WHERE a.id = $1
              AND m.usuario_id <> $2
              AND (m.usuario_id = a.usuario_id
                   OR EXISTS (
                       SELECT 1 FROM comentarios c
                       WHERE c.atividade_id = a.id AND c.usuario_id = m.usuario_id))
            ON CONFLICT (usuario_id, atividade_id, tipo) WHERE read_at IS NULL AND tipo IN ('Reacao', 'Comentario')
            DO UPDATE SET total = notificacoes.total + 1, ator_id = EXCLUDED.ator_id, updated_at = now()
            """, connection, transaction);
        command.Parameters.AddWithValue(atividadeId);
        command.Parameters.AddWithValue(atorId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Avisa o autor da nova situação da atividade.</summary>
    public static async Task NotificarSituacaoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        string situacao,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO notificacoes (usuario_id, grupo_id, atividade_id, tipo, situacao)
            SELECT a.usuario_id, a.grupo_id, a.id, 'SituacaoAlterada', $2
            FROM atividades a
            WHERE a.id = $1
            """, connection, transaction);
        command.Parameters.AddWithValue(atividadeId);
        command.Parameters.AddWithValue(situacao);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Avisa os administradores, menos o autor, de que há uma disputa para decidir.</summary>
    public static async Task NotificarDisputaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO notificacoes (usuario_id, grupo_id, atividade_id, tipo, ator_id)
            SELECT m.usuario_id, a.grupo_id, a.id, 'Disputa', a.usuario_id
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.removed_at IS NULL
            WHERE a.id = $1
              AND m.usuario_id <> a.usuario_id
              AND m.papel IN ('Proprietario', 'Administrador')
            """, connection, transaction);
        command.Parameters.AddWithValue(atividadeId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<int> ExcluirAntigasAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM notificacoes WHERE updated_at < $1");
        command.Parameters.AddWithValue(DateTimeOffset.UtcNow - Retencao);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
