using Npgsql;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Integracoes;

/// <summary>
/// Os grupos de quem conectou o app, só nome e identificador, para escolher onde o treino aparece ao vivo
/// (ADR 0081). Na ordem em que a pessoa entrou.
/// </summary>
internal static class ListarGruposApp
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var app = context.GetAppAtual();

        // SQL explícito sobre as tabelas da fatia Grupos: só leitura dos grupos ativos da pessoa.
        await using var command = dataSource.CreateCommand("""
            SELECT g.id, g.nome
            FROM membros_grupo m
            JOIN grupos g ON g.id = m.grupo_id
            WHERE m.usuario_id = $1 AND m.removed_at IS NULL
            ORDER BY m.joined_at, g.id
            """);
        command.Parameters.AddWithValue(app.UsuarioId);

        var grupos = new List<GrupoAppResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            grupos.Add(new GrupoAppResponse(reader.GetGuid(0), reader.GetString(1)));
        }

        return Results.Json(new GruposAppResponse(grupos), AppJsonContext.Default.GruposAppResponse);
    }
}
