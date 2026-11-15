using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

internal sealed record AutorizarAppRequest(string? App, string? CodeChallenge, string? RedirectUri);

internal sealed record AutorizarAppResponse(string Codigo, string RedirectUri);

/// <summary>
/// A pessoa, com a sessão do navegador, permite que o app prepare rascunhos. O código volta ao app pelo
/// endereço de retorno e só serve, uma vez e por 5 minutos, para quem tem o verificador do PKCE.
/// </summary>
internal static class AutorizarApp
{
    public static async Task<IResult> HandleAsync(
        AutorizarAppRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!AppIntegracao.IsValid(request.App)
            || !AutorizacaoIntegracao.IsValidCodeChallenge(request.CodeChallenge)
            || !AutorizacaoIntegracao.IsRedirectUriPermitida(request.App, request.RedirectUri))
        {
            return ErrosIntegracao.InvalidData();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        var codigo = OpaqueToken.Generate();

        await using var insert = dataSource.CreateCommand("""
            INSERT INTO codigos_integracao (usuario_id, app, code_hash, code_challenge, redirect_uri, expires_at)
            VALUES ($1, $2, $3, $4, $5, $6)
            """);
        insert.Parameters.AddWithValue(usuarioId);
        insert.Parameters.AddWithValue(request.App!);
        insert.Parameters.AddWithValue(OpaqueToken.Hash(codigo));
        insert.Parameters.AddWithValue(request.CodeChallenge!);
        insert.Parameters.AddWithValue(request.RedirectUri!);
        insert.Parameters.AddWithValue(DateTimeOffset.UtcNow + AutorizacaoIntegracao.ValidadeCodigo);
        await insert.ExecuteNonQueryAsync(cancellationToken);

        return Results.Json(
            new AutorizarAppResponse(codigo, request.RedirectUri!),
            AppJsonContext.Default.AutorizarAppResponse);
    }
}
