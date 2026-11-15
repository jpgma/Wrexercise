using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Integracoes;

internal static class IntegracoesRoutes
{
    public const string IntegracaoPolicy = "integracao";

    public static void AddIntegracoesRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(IntegracaoPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });

    public static void MapIntegracoesRoutes(this IEndpointRouteBuilder routes)
    {
        // Com a sessão do navegador: a pessoa autoriza, revê e desconecta.
        var autenticado = routes.MapGroup("/api").RequireSessao();
        autenticado.MapPost("/integracoes/autorizacoes", AutorizarApp.HandleAsync);
        autenticado.MapGet("/rascunhos/{id:guid}", ObterRascunho.HandleAsync);
        autenticado.MapGet("/rascunhos/{id:guid}/imagem", ObterImagemRascunho.HandleAsync);
        autenticado.MapGet("/apps-conectados", ListarAppsConectados.HandleAsync);
        autenticado.MapDelete("/apps-conectados/{id:guid}", DesconectarApp.HandleAsync);

        // Do app, sem cookie (ADR 0065).
        var integracoes = routes.MapGroup("/api/integracoes").RequireRateLimiting(IntegracaoPolicy);
        integracoes.MapPost("/token", TrocarCodigo.HandleAsync);

        var doApp = integracoes.MapGroup("").RequireTokenApp();
        doApp.MapDelete("/token", RevogarToken.HandleAsync);
        doApp.MapGet("/rascunhos/{externalId}", ObterSituacaoRascunho.HandleAsync);
        doApp.MapPut("/rascunhos/{externalId}", SalvarRascunho.HandleAsync)
            .AddEndpointFilter(async (invocation, next) =>
            {
                // A imagem do resumo e a miniatura; o resto da API continua com 1 MB.
                var feature = invocation.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (feature is not null && !feature.IsReadOnly)
                {
                    feature.MaxRequestBodySize =
                        ValidacaoMidiaEvidencia.MaxBytes + ValidacaoMidiaEvidencia.MaxBytesMiniatura + (16 * 1024);
                }

                return await next(invocation);
            });
    }
}
