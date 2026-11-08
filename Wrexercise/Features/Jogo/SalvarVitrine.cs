using Npgsql;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Jogo;

internal sealed record SalvarVitrineRequest(IReadOnlyList<string>? Enfeites);

/// <summary>A pessoa escolhe e ordena até 3 Enfeites liberados; esvaziar a vitrine é permitido.</summary>
internal static class SalvarVitrine
{
    public static async Task<IResult> HandleAsync(
        SalvarVitrineRequest request,
        HttpContext context,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (request.Enfeites is not { } escolhidos)
        {
            return ErrosJogo.DadosInvalidos();
        }

        var usuarioId = context.GetSessaoAtual().UsuarioId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var estado = await Vitrines.LoadEstadoAsync(connection, usuarioId, cancellationToken);
        if (!Enfeites.IsVitrineValida(estado.Liberados, escolhidos))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "VITRINE_INVALIDA",
                "A vitrine aceita até 3 Enfeites que você já conquistou, sem repetir.");
        }

        await Vitrines.SalvarAsync(connection, usuarioId, escolhidos, estado.Liberados, cancellationToken);
        return Results.NoContent();
    }
}
