using Npgsql;

namespace Wrexercise.Features.Notificacoes.Contracts;

internal static class TipoNotificacaoJogada
{
    public const string CartaRecebida = "CartaRecebida";
    public const string CartaAceita = "CartaAceita";
    public const string CartaRecusada = "CartaRecusada";
    public const string JogadaConcluida = "JogadaConcluida";
}

/// <summary>Avisos de Jogadas, gravados na transação de quem chamou.</summary>
internal static class NotificacoesJogada
{
    public static async Task NotificarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid jogadaId,
        Guid usuarioId,
        Guid? atorId,
        string tipo,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO notificacoes (usuario_id, grupo_id, jogada_id, tipo, ator_id)
            SELECT $2, j.grupo_id, j.id, $4, $3
            FROM jogadas j
            WHERE j.id = $1
            """, connection, transaction);
        command.Parameters.AddWithValue(jogadaId);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Uuid, (object?)atorId ?? DBNull.Value);
        command.Parameters.AddWithValue(tipo);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
