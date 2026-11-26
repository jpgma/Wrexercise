using Npgsql;

namespace Wrexercise.Features.Notificacoes.Contracts;

internal static class TipoNotificacaoConvocacao
{
    public const string Convocacao = "Convocacao";
}

/// <summary>Aviso da Convocação (decisão 0091), gravado na transação de quem chamou.</summary>
internal static class NotificacoesConvocacao
{
    /// <summary>Avisa cada convidado, menos o anfitrião, que é o ator. Um aviso só: não há lembretes.</summary>
    public static async Task NotificarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid convocacaoId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"""
            INSERT INTO notificacoes (usuario_id, grupo_id, convocacao_id, tipo, ator_id, situacao)
            SELECT p.usuario_id, c.grupo_id, c.id, '{TipoNotificacaoConvocacao.Convocacao}', c.anfitriao_id,
                c.tipo_atividade
            FROM convocacoes c
            JOIN participantes_convocacao p ON p.convocacao_id = c.id
            WHERE c.id = $1 AND p.usuario_id <> c.anfitriao_id
            """, connection, transaction);
        command.Parameters.AddWithValue(convocacaoId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Marca o aviso como lido: a pessoa já respondeu.</summary>
    public static async Task MarcarLidaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid convocacaoId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE notificacoes
            SET read_at = now()
            WHERE convocacao_id = $1 AND usuario_id = $2 AND read_at IS NULL
            """, connection, transaction);
        command.Parameters.AddWithValue(convocacaoId);
        command.Parameters.AddWithValue(usuarioId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
