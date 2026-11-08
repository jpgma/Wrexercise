using System.Threading.RateLimiting;
using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Jogo;

internal static class JogoRoutes
{
    public const string AvatarPolicy = "avatar";

    public static void AddJogoRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
            options.AddPolicy(AvatarPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                })));

    public static void MapJogoRoutes(this IEndpointRouteBuilder routes)
    {
        var grupos = routes.MapGroup("/api/grupos").RequireSessao();
        grupos.MapGet("/{grupoId:guid}/cena", ObterCena.HandleAsync);
        grupos.MapGet("/{grupoId:guid}/jogadas", ListarJogadas.HandleAsync);
        grupos.MapPost("/{grupoId:guid}/jogadas", JogarCarta.HandleAsync);
        grupos.MapPost("/{grupoId:guid}/jogadas/vistas", MarcarJogadasVistas.HandleAsync);
        grupos.MapDelete("/{grupoId:guid}/membros/{usuarioId:guid}/avatar", RedefinirAvatarMembro.HandleAsync);

        var jogo = routes.MapGroup("/api/jogo").RequireSessao();
        jogo.MapGet("/energia", ObterEnergia.HandleAsync);
        jogo.MapGet("/loja", ObterLoja.HandleAsync);
        jogo.MapPost("/cartas", ComprarCarta.HandleAsync);
        jogo.MapGet("/avatares", ObterAvatares.HandleAsync);
        jogo.MapPut("/avatar/folha", EnviarFolhaAvatar.HandleAsync).RequireRateLimiting(AvatarPolicy);
        jogo.MapDelete("/avatar/folha", RestaurarAvatar.HandleAsync);

        routes.MapGroup("/api/avatares")
            .RequireSessao()
            .MapGet("/{arquivo}", ObterFolhaAvatar.HandleAsync);

        routes.MapGroup("/api/jogadas")
            .RequireSessao()
            .MapPut("/{id:guid}/resposta", ResponderJogada.HandleAsync);
    }
}
