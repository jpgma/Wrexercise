using Npgsql;

namespace Wrexercise.Features.Notificacoes.Contracts;

internal static class TipoNotificacaoJogada
{
    public const string CartaRecebida = "CartaRecebida";
    public const string CartaAceita = "CartaAceita";
    public const string CartaRecusada = "CartaRecusada";
    public const string JogadaConcluida = "JogadaConcluida";

    /// <summary>A carta enviada falhou ou expirou: metade do preço voltou (decisão 0087). Só na lista, sem push.</summary>
    public const string CartaNaoCumprida = "CartaNaoCumprida";
}

/// <summary>Avisos de Jogadas, gravados na transação de quem chamou.</summary>
internal static class NotificacoesJogada
{
    /// <param name="energia">A Energia que voltou para quem enviou (decisão 0087), se houver.</param>
    public static async Task NotificarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid jogadaId,
        Guid usuarioId,
        Guid? atorId,
        string tipo,
        CancellationToken cancellationToken,
        int? energia = null)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO notificacoes (usuario_id, grupo_id, jogada_id, tipo, ator_id, energia)
            SELECT $2, j.grupo_id, j.id, $4, $3, $5
            FROM jogadas j
            WHERE j.id = $1
            """, connection, transaction);
        command.Parameters.AddWithValue(jogadaId);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Uuid, (object?)atorId ?? DBNull.Value);
        command.Parameters.AddWithValue(tipo);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Integer, ToParametro(energia));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Avisa quem enviou que a carta falhou ou expirou (decisão 0087). Um aviso por Jogada: repetir não grava outro.
    /// </summary>
    /// <param name="situacao"><c>Falhou</c> ou <c>Expirada</c>.</param>
    /// <returns>Se gravou o aviso.</returns>
    public static async Task<bool> NotificarNaoCumpridaAsync(
        NpgsqlConnection connection,
        Guid jogadaId,
        Guid remetenteId,
        Guid destinatarioId,
        string situacao,
        int energia,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO notificacoes (usuario_id, grupo_id, jogada_id, tipo, ator_id, situacao, energia)
            SELECT $2, j.grupo_id, j.id, 'CartaNaoCumprida', $3, $4, $5
            FROM jogadas j
            WHERE j.id = $1
            ON CONFLICT (jogada_id) WHERE tipo = 'CartaNaoCumprida' DO NOTHING
            """, connection);
        command.Parameters.AddWithValue(jogadaId);
        command.Parameters.AddWithValue(remetenteId);
        command.Parameters.AddWithValue(destinatarioId);
        command.Parameters.AddWithValue(situacao);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Integer, ToParametro(energia));
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    /// <summary>Marca como lidos os avisos de um tipo desta Jogada para a pessoa: a carta já foi respondida.</summary>
    public static async Task MarcarLidasAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid jogadaId,
        Guid usuarioId,
        string tipo,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE notificacoes
            SET read_at = now()
            WHERE jogada_id = $1 AND usuario_id = $2 AND tipo = $3 AND read_at IS NULL
            """, connection, transaction);
        command.Parameters.AddWithValue(jogadaId);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(tipo);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static object ToParametro(int? energia) => energia is > 0 ? energia.Value : DBNull.Value;
}
