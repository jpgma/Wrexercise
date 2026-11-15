using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

internal sealed record AppConectadoResponse(Guid Id, string App, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);

internal sealed record AppsConectadosResponse(IReadOnlyList<AppConectadoResponse> Apps);

/// <summary>Os apps com acesso ativo, na tela de configurações.</summary>
internal static class ListarAppsConectados
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var command = dataSource.CreateCommand("""
            SELECT id, app, created_at, last_used_at
            FROM apps_conectados
            WHERE usuario_id = $1 AND revoked_at IS NULL
            ORDER BY created_at DESC
            """);
        command.Parameters.AddWithValue(usuarioId);

        var apps = new List<AppConectadoResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            apps.Add(new AppConectadoResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return Results.Json(new AppsConectadosResponse(apps), AppJsonContext.Default.AppsConectadosResponse);
    }
}

/// <summary>A pessoa desconecta um app pelo Wrexercise. O token dele para de valer na hora.</summary>
internal static class DesconectarApp
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var command = dataSource.CreateCommand("""
            UPDATE apps_conectados SET revoked_at = now()
            WHERE id = $1 AND usuario_id = $2 AND revoked_at IS NULL
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(usuarioId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0
            ? Results.NoContent()
            : ErrosIntegracao.AppNaoEncontrado();
    }
}

/// <summary>O app se desconecta sozinho (ao sair da conta ou desvincular): revoga o próprio token.</summary>
internal static class RevogarToken
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var app = context.GetAppAtual();
        await using var command = dataSource.CreateCommand(
            "UPDATE apps_conectados SET revoked_at = now() WHERE id = $1");
        command.Parameters.AddWithValue(app.Id);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return Results.NoContent();
    }
}
