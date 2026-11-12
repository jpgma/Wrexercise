using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Json;
using Wrexercise.Infrastructure.Push;

namespace Wrexercise.Features.Notificacoes;

internal sealed record PushConfiguracaoResponse(string? ChavePublica);

internal sealed record PushSubscriptionRequest(string? Endpoint, string? P256dh, string? Auth);

/// <summary>Cada aparelho com o push ligado é uma inscrição. Ver docs/decisoes/0049-notificacoes-push.md.</summary>
internal static class InscricoesPush
{
    private const int MaximoPorUsuario = 10;
    private const int TamanhoMaximoEndpoint = 2048;

    // O servidor faz POST no endpoint informado: só serviços de push conhecidos, para não virar um proxy.
    private static readonly string[] ServicosPush =
    [
        "fcm.googleapis.com",
        ".push.apple.com",
        ".push.services.mozilla.com",
        ".notify.windows.com",
    ];

    public static IResult ObterConfiguracao(WebPushClient push) =>
        Results.Json(new PushConfiguracaoResponse(push.ChavePublica), AppJsonContext.Default.PushConfiguracaoResponse);

    public static async Task<IResult> SalvarAsync(
        PushSubscriptionRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        WebPushClient push,
        CancellationToken cancellationToken)
    {
        if (push.ChavePublica is null)
        {
            return ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "PUSH_DESLIGADO",
                "As notificações no aparelho não estão disponíveis neste servidor.");
        }

        if (request is not { Endpoint: { } endpoint, P256dh: { } p256dh, Auth: { } auth }
            || !IsEndpointPermitido(endpoint)
            || !WebPushCriptografia.IsInscricaoValida(p256dh, auth))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "PUSH_INVALIDO",
                "Não foi possível ativar as notificações neste aparelho.");
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // O mesmo aparelho pode trocar de conta: a inscrição passa para quem entrou por último.
        await using (var upsert = new NpgsqlCommand("""
            INSERT INTO push_subscriptions (usuario_id, endpoint, p256dh, auth)
            VALUES ($1, $2, $3, $4)
            ON CONFLICT (endpoint) DO UPDATE
            SET usuario_id = EXCLUDED.usuario_id, p256dh = EXCLUDED.p256dh, auth = EXCLUDED.auth, updated_at = now()
            """, connection, transaction))
        {
            upsert.Parameters.AddWithValue(usuarioId);
            upsert.Parameters.AddWithValue(endpoint);
            upsert.Parameters.AddWithValue(p256dh);
            upsert.Parameters.AddWithValue(auth);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var limite = new NpgsqlCommand($"""
            DELETE FROM push_subscriptions
            WHERE usuario_id = $1
              AND id NOT IN (
                  SELECT id FROM push_subscriptions
                  WHERE usuario_id = $1
                  ORDER BY updated_at DESC
                  LIMIT {MaximoPorUsuario})
            """, connection, transaction))
        {
            limite.Parameters.AddWithValue(usuarioId);
            await limite.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    public static async Task<IResult> RemoverAsync(
        string? endpoint,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(endpoint))
        {
            await using var command = dataSource.CreateCommand(
                "DELETE FROM push_subscriptions WHERE endpoint = $1 AND usuario_id = $2");
            command.Parameters.AddWithValue(endpoint);
            command.Parameters.AddWithValue(context.GetSessaoAtual().UsuarioId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    public static bool IsEndpointPermitido(string endpoint) =>
        endpoint.Length <= TamanhoMaximoEndpoint
        && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && ServicosPush.Any(servico => servico.StartsWith('.')
            ? uri.Host.EndsWith(servico, StringComparison.OrdinalIgnoreCase)
            : uri.Host.Equals(servico, StringComparison.OrdinalIgnoreCase));
}
