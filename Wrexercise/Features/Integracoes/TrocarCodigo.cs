using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

internal sealed record TrocarCodigoRequest(string? Codigo, string? CodeVerifier, string? RedirectUri);

internal sealed record TrocarCodigoResponse(string Token, string NomeUsuario);

/// <summary>
/// O app troca o código pelo token, sem cookie e sem origem de navegador. Um código errado ou repetido
/// não diz o motivo; uma tentativa com o verificador errado queima o código.
/// </summary>
internal static class TrocarCodigo
{
    public static async Task<IResult> HandleAsync(
        TrocarCodigoRequest request,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Codigo)
            || !AutorizacaoIntegracao.IsValidCodeVerifier(request.CodeVerifier)
            || string.IsNullOrEmpty(request.RedirectUri))
        {
            return ErrosIntegracao.InvalidData();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid usuarioId;
        string app;
        string challenge;
        string redirectUri;
        await using (var usar = new NpgsqlCommand("""
            UPDATE codigos_integracao
            SET used_at = now()
            WHERE code_hash = $1 AND used_at IS NULL AND expires_at > now()
            RETURNING usuario_id, app, code_challenge, redirect_uri
            """, connection, transaction))
        {
            usar.Parameters.AddWithValue(OpaqueToken.Hash(request.Codigo));
            await using var reader = await usar.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ErrosIntegracao.CodigoInvalido();
            }

            usuarioId = reader.GetGuid(0);
            app = reader.GetString(1);
            challenge = reader.GetString(2);
            redirectUri = reader.GetString(3);
        }

        if (redirectUri != request.RedirectUri
            || !AutorizacaoIntegracao.VerificarPkce(request.CodeVerifier!, challenge))
        {
            // O código fica usado: quem não tem o verificador não ganha outra tentativa.
            await transaction.CommitAsync(cancellationToken);
            return ErrosIntegracao.CodigoInvalido();
        }

        // Uma conexão por app: conectar de novo substitui a anterior.
        await using (var revogar = new NpgsqlCommand("""
            UPDATE apps_conectados SET revoked_at = now()
            WHERE usuario_id = $1 AND app = $2 AND revoked_at IS NULL
            """, connection, transaction))
        {
            revogar.Parameters.AddWithValue(usuarioId);
            revogar.Parameters.AddWithValue(app);
            await revogar.ExecuteNonQueryAsync(cancellationToken);
        }

        var token = OpaqueToken.Generate();
        await using (var insert = new NpgsqlCommand(
            "INSERT INTO apps_conectados (usuario_id, app, token_hash) VALUES ($1, $2, $3)",
            connection,
            transaction))
        {
            insert.Parameters.AddWithValue(usuarioId);
            insert.Parameters.AddWithValue(app);
            insert.Parameters.AddWithValue(OpaqueToken.Hash(token));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        string nome;
        await using (var consulta = new NpgsqlCommand(
            "SELECT nome_exibicao FROM usuarios WHERE id = $1",
            connection,
            transaction))
        {
            consulta.Parameters.AddWithValue(usuarioId);
            nome = (string)(await consulta.ExecuteScalarAsync(cancellationToken))!;
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.Json(new TrocarCodigoResponse(token, nome), AppJsonContext.Default.TrocarCodigoResponse);
    }
}
