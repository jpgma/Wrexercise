using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Integracoes;

internal sealed record AppAtual(Guid Id, Guid UsuarioId, string App);

/// <summary>
/// Autenticação de um app conectado (ADR 0065): <c>Authorization: Bearer</c> com o token opaco
/// entregue na conexão. Só vale nas rotas de <c>/api/integracoes</c> e nunca junto do cookie de sessão.
/// </summary>
internal static class TokenApp
{
    private const string ItemKey = "Wrexercise.AppAtual";
    private const string Prefixo = "Bearer ";

    public static TBuilder RequireTokenApp<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (invocation, next) =>
        {
            var context = invocation.HttpContext;
            var app = await ResolveAsync(context);
            if (app is null)
            {
                return NaoAutorizado();
            }

            context.Items[ItemKey] = app;
            return await next(invocation);
        });

    /// <summary>Só pode ser chamado em endpoints protegidos por <see cref="RequireTokenApp{TBuilder}"/>.</summary>
    public static AppAtual GetAppAtual(this HttpContext context) =>
        context.Items[ItemKey] as AppAtual
        ?? throw new InvalidOperationException("O endpoint não exige token de app.");

    public static string? LerToken(HttpContext context)
    {
        var valor = context.Request.Headers.Authorization.ToString();
        return valor.StartsWith(Prefixo, StringComparison.OrdinalIgnoreCase) && valor.Length > Prefixo.Length
            ? valor[Prefixo.Length..].Trim()
            : null;
    }

    private static IResult NaoAutorizado() => ApiProblem.Create(
        StatusCodes.Status401Unauthorized,
        "APP_NAO_AUTORIZADO",
        "Conecte o app de novo.");

    private static async Task<AppAtual?> ResolveAsync(HttpContext context)
    {
        // Com o cookie, a requisição vem do navegador: o token não substitui a sessão nem a proteção de origem.
        if (SessaoCookie.Read(context) is not null || LerToken(context) is not { } token)
        {
            return null;
        }

        var dataSource = context.RequestServices.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand("""
            UPDATE apps_conectados
            SET last_used_at = now()
            WHERE token_hash = $1 AND revoked_at IS NULL
            RETURNING id, usuario_id, app
            """);
        command.Parameters.AddWithValue(OpaqueToken.Hash(token));

        await using var reader = await command.ExecuteReaderAsync(context.RequestAborted);
        return await reader.ReadAsync(context.RequestAborted)
            ? new AppAtual(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2))
            : null;
    }
}
