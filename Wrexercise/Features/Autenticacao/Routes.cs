using System.Threading.RateLimiting;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Autenticacao;

internal static class AutenticacaoRoutes
{
    private const string TentativasPolicy = "autenticacao-tentativas";

    public static void AddAutenticacaoRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.OnRejected = (rejected, _) => new ValueTask(ApiProblem.WriteAsync(
                rejected.HttpContext,
                StatusCodes.Status429TooManyRequests,
                "MUITAS_TENTATIVAS",
                "Muitas tentativas. Aguarde um minuto e tente novamente."));

            options.AddPolicy(TentativasPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });

    public static void MapAutenticacaoRoutes(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/autenticacao");

        group.MapGet("/configuracao-inicial", ObterConfiguracaoInicial.HandleAsync);
        group.MapPost("/configuracao-inicial", ConfigurarPrimeiroUsuario.HandleAsync)
            .RequireRateLimiting(TentativasPolicy);
        group.MapPost("/registrar", Registrar.HandleAsync).RequireRateLimiting(TentativasPolicy);
        group.MapPost("/entrar", Entrar.HandleAsync).RequireRateLimiting(TentativasPolicy);
        group.MapPost("/sair", Sair.HandleAsync);
        group.MapGet("/sessao", ObterSessao.HandleAsync).RequireSessao();
        group.MapPost("/redefinir-senha", RedefinirSenha.HandleAsync).RequireRateLimiting(TentativasPolicy);

        var conta = routes.MapGroup("/api/usuarios/eu").RequireSessao();
        conta.MapPut("/", AlterarNome.HandleAsync);
        conta.MapPut("/modo-inicio", AlterarModoInicio.HandleAsync);
        conta.MapPost("/senha", AlterarSenha.HandleAsync).RequireRateLimiting(TentativasPolicy);
    }
}
