using Npgsql;

namespace Wrexercise.Features.Notificacoes.Contracts;

internal static class TipoNotificacaoCutucada
{
    public const string Cutucada = "Cutucada";
}

/// <summary>Aviso da Cutucada (decisão 0063), gravado na transação de quem chamou.</summary>
internal static class NotificacoesCutucada
{
    /// <summary>Avisa quem foi cutucado. O ator é quem cutucou.</summary>
    public static async Task NotificarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid cutucadaId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            INSERT INTO notificacoes (usuario_id, grupo_id, cutucada_id, tipo, ator_id)
            SELECT c.destinatario_id, c.grupo_id, c.id, '{TipoNotificacaoCutucada.Cutucada}', c.autor_id
            FROM cutucadas c
            WHERE c.id = $1
            """, connection, transaction);
        command.Parameters.AddWithValue(cutucadaId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
