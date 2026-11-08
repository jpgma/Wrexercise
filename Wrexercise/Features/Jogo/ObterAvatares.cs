using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// As folhas importadas da pessoa e de quem divide um grupo com ela. Um mapa só para o aplicativo todo:
/// o componente <c>Avatar</c> aparece fora de qualquer grupo (Perfil, Convite).
/// </summary>
internal static class ObterAvatares
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var command = dataSource.CreateCommand("""
            WITH visiveis AS (
                SELECT $1::uuid AS usuario_id
                UNION
                SELECT outro.usuario_id
                FROM membros_grupo eu
                JOIN membros_grupo outro ON outro.grupo_id = eu.grupo_id AND outro.removed_at IS NULL
                WHERE eu.usuario_id = $1 AND eu.removed_at IS NULL
            )
            SELECT v.usuario_id, a.arquivo
            FROM visiveis v
            JOIN avatares a ON a.usuario_id = v.usuario_id
            WHERE a.arquivo IS NOT NULL
            """);
        command.Parameters.AddWithValue(usuarioId);

        var avatares = new List<AvatarMembroResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            avatares.Add(new AvatarMembroResponse(reader.GetGuid(0), Avatares.GetUrl(reader.GetString(1))));
        }

        return Results.Json(new AvataresResponse(avatares), AppJsonContext.Default.AvataresResponse);
    }
}
