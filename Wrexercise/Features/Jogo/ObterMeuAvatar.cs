using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Features.Jogo;

/// <summary>O avatar de quem consulta: folha, vitrine, Enfeites liberados e os que ainda não viu.</summary>
internal static class ObterMeuAvatar
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var estado = await Vitrines.LoadEstadoAsync(connection, usuarioId, cancellationToken);

        string? arquivo;
        await using (var command = new NpgsqlCommand("SELECT arquivo FROM avatares WHERE usuario_id = $1", connection))
        {
            command.Parameters.AddWithValue(usuarioId);
            arquivo = await command.ExecuteScalarAsync(cancellationToken) as string;
        }

        return Results.Json(
            new MeuAvatarResponse(
                arquivo is null ? null : Avatares.GetUrl(arquivo),
                Vitrines.ToResponses(estado.Liberados, estado.Vitrine),
                estado.Liberados.Select(Vitrines.ToResponse).ToList(),
                estado.Novos),
            AppJsonContext.Default.MeuAvatarResponse);
    }
}
