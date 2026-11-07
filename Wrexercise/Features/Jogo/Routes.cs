using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Jogo;

internal static class JogoRoutes
{
    public static void MapJogoRoutes(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/grupos")
            .RequireSessao()
            .MapGet("/{grupoId:guid}/cena", ObterCena.HandleAsync);
    }
}
