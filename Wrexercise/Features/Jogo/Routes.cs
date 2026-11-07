using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Jogo;

internal static class JogoRoutes
{
    public static void MapJogoRoutes(this IEndpointRouteBuilder routes)
    {
        var grupos = routes.MapGroup("/api/grupos").RequireSessao();
        grupos.MapGet("/{grupoId:guid}/cena", ObterCena.HandleAsync);
        grupos.MapGet("/{grupoId:guid}/jogadas", ListarJogadas.HandleAsync);
        grupos.MapPost("/{grupoId:guid}/jogadas", JogarCarta.HandleAsync);
        grupos.MapPost("/{grupoId:guid}/jogadas/vistas", MarcarJogadasVistas.HandleAsync);

        var jogo = routes.MapGroup("/api/jogo").RequireSessao();
        jogo.MapGet("/energia", ObterEnergia.HandleAsync);
        jogo.MapGet("/loja", ObterLoja.HandleAsync);
        jogo.MapPost("/cartas", ComprarCarta.HandleAsync);

        routes.MapGroup("/api/jogadas")
            .RequireSessao()
            .MapPut("/{id:guid}/resposta", ResponderJogada.HandleAsync);
    }
}
