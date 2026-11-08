using Npgsql;
using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Jogo;

/// <summary>
/// Depois da encenação de Enfeite novo: guarda a vitrine em vigor e marca os Enfeites como vistos, para
/// que um Enfeite que a pessoa tirar depois não volte sozinho.
/// </summary>
internal static class MarcarEnfeitesVistos
{
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var estado = await Vitrines.LoadEstadoAsync(connection, usuarioId, cancellationToken);
        await Vitrines.SalvarAsync(connection, usuarioId, estado.Vitrine, estado.Liberados, cancellationToken);
        return Results.NoContent();
    }
}
