using Npgsql;
using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Autenticacao;

internal static class Sessoes
{
    /// <returns>O token da nova sessão.</returns>
    public static async Task<string> CreateAsync(
        NpgsqlDataSource dataSource,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // A limpeza acontece aqui para dispensar uma tarefa agendada.
        await using (var cleanup = new NpgsqlCommand(
            "DELETE FROM sessoes_autenticacao WHERE usuario_id = $1 AND expires_at <= now()",
            connection,
            transaction))
        {
            cleanup.Parameters.AddWithValue(usuarioId);
            await cleanup.ExecuteNonQueryAsync(cancellationToken);
        }

        var token = await InsertAsync(connection, transaction, usuarioId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return token;
    }

    public static async Task RevokeAsync(
        NpgsqlDataSource dataSource,
        string token,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE sessoes_autenticacao
            SET revoked_at = now()
            WHERE token_hash = $1 AND revoked_at IS NULL
            """);
        command.Parameters.AddWithValue(OpaqueToken.Hash(token));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task SetSenhaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "UPDATE usuarios SET password_hash = $2 WHERE id = $1",
            connection,
            transaction);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(passwordHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Encerra as sessões do usuário, exceto a de <paramref name="manterTokenHash"/>, quando informada.</summary>
    public static async Task RevokeDoUsuarioAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioId,
        byte[]? manterTokenHash,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE sessoes_autenticacao
            SET revoked_at = now()
            WHERE usuario_id = $1 AND revoked_at IS NULL AND ($2::bytea IS NULL OR token_hash <> $2)
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bytea, (object?)manterTokenHash ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<string> InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var token = OpaqueToken.Generate();

        await using var command = new NpgsqlCommand("""
            INSERT INTO sessoes_autenticacao (usuario_id, token_hash, expires_at)
            VALUES ($1, $2, $3)
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(OpaqueToken.Hash(token));
        command.Parameters.AddWithValue(DateTimeOffset.UtcNow + SessaoPolicy.Lifetime);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return token;
    }

    public static async Task<UsuarioResponse?> GetUsuarioAsync(
        NpgsqlDataSource dataSource,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT id, nome_exibicao, email, modo_inicio FROM usuarios WHERE id = $1");
        command.Parameters.AddWithValue(usuarioId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new UsuarioResponse(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3))
            : null;
    }
}
