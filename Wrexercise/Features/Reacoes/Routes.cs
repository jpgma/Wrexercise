using System.Threading.RateLimiting;
using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Reacoes;

internal static class ReacoesRoutes
{
    public const string ComentariosPolicy = "comentarios";

    public static void AddReacoesRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(ComentariosPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });

    public static void MapReacoesRoutes(this IEndpointRouteBuilder routes)
    {
        var atividade = routes.MapGroup("/api/atividades/{id:guid}").RequireSessao();
        atividade.MapPut("/reacao", RegistrarReacao.HandleAsync);
        atividade.MapGet("/reacoes", ListarReacoes.HandleAsync);
        atividade.MapPost("/revisao", RevisarAtividade.HandleAsync);
        atividade.MapGet("/comentarios", Comentarios.ListarAsync);
        atividade.MapPost("/comentarios", Comentarios.CriarAsync).RequireRateLimiting(ComentariosPolicy);
    }
}
