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
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var pessoas = new List<(Guid Id, string? Arquivo, bool Jogo)>();
        await using (var command = new NpgsqlCommand("""
            WITH visiveis AS (
                SELECT $1::uuid AS usuario_id
                UNION
                SELECT outro.usuario_id
                FROM membros_grupo eu
                JOIN membros_grupo outro ON outro.grupo_id = eu.grupo_id AND outro.removed_at IS NULL
                WHERE eu.usuario_id = $1 AND eu.removed_at IS NULL
            )
            SELECT v.usuario_id, a.arquivo, u.modo_inicio = 'Jogo'
            FROM visiveis v
            JOIN usuarios u ON u.id = v.usuario_id
            LEFT JOIN avatares a ON a.usuario_id = v.usuario_id
            """, connection))
        {
            command.Parameters.AddWithValue(usuarioId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                pessoas.Add((
                    reader.GetGuid(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.GetBoolean(2)));
            }
        }

        // A vitrine é calculada por pessoa, como as conquistas; só quem está no modo Jogo a expõe.
        var avatares = new List<AvatarMembroResponse>();
        foreach (var (id, arquivo, jogo) in pessoas)
        {
            IReadOnlyList<EnfeiteResponse> vitrine = [];
            if (jogo)
            {
                var estado = await Vitrines.LoadEstadoAsync(connection, id, cancellationToken);
                vitrine = Vitrines.ToResponses(estado.Liberados, estado.Vitrine);
            }

            if (arquivo is not null || vitrine.Count > 0)
            {
                avatares.Add(new AvatarMembroResponse(id, arquivo is null ? null : Avatares.GetUrl(arquivo), vitrine));
            }
        }

        return Results.Json(new AvataresResponse(avatares), AppJsonContext.Default.AvataresResponse);
    }
}
