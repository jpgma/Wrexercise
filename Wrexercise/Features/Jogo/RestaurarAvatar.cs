using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Jogo;

/// <summary>Volta ao avatar padrão.</summary>
internal static class RestaurarAvatar
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        ArmazenamentoAvatares armazenamento,
        CancellationToken cancellationToken)
    {
        await Avatares.RestaurarAsync(
            dataSource,
            armazenamento,
            context.GetSessaoAtual().UsuarioId,
            cancellationToken);
        return Results.NoContent();
    }
}
