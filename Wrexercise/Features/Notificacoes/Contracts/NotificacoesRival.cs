using Npgsql;

namespace Wrexercise.Features.Notificacoes.Contracts;

internal static class TipoNotificacaoRival
{
    public const string RivalEscolhido = "RivalEscolhido";
}

/// <summary>Avisos do Rival da semana, gravados na transação de quem chamou.</summary>
internal static class NotificacoesRival
{
    /// <summary>Avisa quem foi escolhido como Rival. O ator é quem escolheu.</summary>
    public static async Task NotificarEscolhidoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid rivalRegistroId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            INSERT INTO notificacoes (usuario_id, grupo_id, rival_id, tipo, ator_id)
            SELECT r.rival_id, r.grupo_id, r.id, '{TipoNotificacaoRival.RivalEscolhido}', r.escolhedor_id
            FROM rivais r
            WHERE r.id = $1
            """, connection, transaction);
        command.Parameters.AddWithValue(rivalRegistroId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
