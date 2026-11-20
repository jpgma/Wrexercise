using Npgsql;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Notificacoes.Contracts;

internal static class TipoNotificacaoTreino
{
    public const string ComecouTreinar = "ComecouTreinar";
    public const string Torcida = "Torcida";
    public const string TreinoLongo = "TreinoLongo";
}

/// <summary>Avisos do treino ao vivo (ADR 0078), gravados na transação de quem chamou.</summary>
internal static class NotificacoesTreino
{
    /// <summary>Um aviso "começou a treinar" por pessoa e grupo a cada 12 horas: mais que isso vira ruído.</summary>
    private static readonly TimeSpan IntervaloComecouTreinar = TimeSpan.FromHours(12);

    /// <summary>Avisa os colegas acordados de que alguém começou a treinar agora.</summary>
    public static async Task NotificarComecouTreinarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            INSERT INTO notificacoes (usuario_id, grupo_id, atividade_id, tipo, ator_id)
            SELECT m.usuario_id, a.grupo_id, a.id, '{TipoNotificacaoTreino.ComecouTreinar}', a.usuario_id
            FROM atividades a
            JOIN membros_grupo m ON m.grupo_id = a.grupo_id AND m.removed_at IS NULL AND m.usuario_id <> a.usuario_id
            WHERE a.id = $1
              AND {MembrosDormindo.AcordadoSql("m")}
              AND NOT EXISTS (
                  SELECT 1 FROM notificacoes n
                  WHERE n.tipo = '{TipoNotificacaoTreino.ComecouTreinar}'
                    AND n.ator_id = a.usuario_id AND n.grupo_id = a.grupo_id
                    AND n.created_at > now() - $2)
            """, connection, transaction);
        command.Parameters.AddWithValue(atividadeId);
        command.Parameters.AddWithValue(IntervaloComecouTreinar);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Avisa quem treina de que um colega torceu. O ator é quem torceu.</summary>
    public static async Task NotificarTorcidaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        Guid torcedorId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            INSERT INTO notificacoes (usuario_id, grupo_id, atividade_id, tipo, ator_id)
            SELECT a.usuario_id, a.grupo_id, a.id, '{TipoNotificacaoTreino.Torcida}', $2
            FROM atividades a
            WHERE a.id = $1
            """, connection, transaction);
        command.Parameters.AddWithValue(atividadeId);
        command.Parameters.AddWithValue(torcedorId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Lembra quem esqueceu o treino aberto ("Ainda treinando?"), uma vez por treino.
    /// </summary>
    /// <returns>Quantos lembretes foram gravados.</returns>
    public static async Task<int> NotificarTreinosLongosAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        TimeSpan aberto,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            INSERT INTO notificacoes (usuario_id, grupo_id, atividade_id, tipo, ator_id)
            SELECT a.usuario_id, a.grupo_id, a.id, '{TipoNotificacaoTreino.TreinoLongo}', a.usuario_id
            FROM atividades a
            WHERE a.situacao_verificacao = 'EmAndamento'
              AND a.started_at <= now() - $1
              AND NOT EXISTS (
                  SELECT 1 FROM notificacoes n
                  WHERE n.atividade_id = a.id AND n.tipo = '{TipoNotificacaoTreino.TreinoLongo}')
            """, connection, transaction);
        command.Parameters.AddWithValue(aberto);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
