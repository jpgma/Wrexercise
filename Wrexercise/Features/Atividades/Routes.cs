using System.Threading.RateLimiting;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;
namespace Wrexercise.Features.Atividades;

internal static class AtividadesRoutes
{
    public const string UploadPolicy = "upload";

    public static void AddAtividadesRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(UploadPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });

    public static void MapAtividadesRoutes(this IEndpointRouteBuilder routes)
    {
        var autenticado = routes.MapGroup("").RequireSessao();
        autenticado.MapGet("/api/atividades/recentes", ListarRecentes.HandleAsync);
        autenticado.MapGet("/api/atividades/em-andamento", ObterTreinoAtual.HandleAsync);

        var doGrupo = routes.MapGroup("/api/grupos/{grupoId:guid}/atividades").RequireSessao();
        doGrupo.MapGet("/", ListarAtividades.HandleAsync);
        doGrupo.MapPost("/", CriarAtividade.HandleAsync);
        doGrupo.MapPost("/em-andamento", ComecarTreino.HandleAsync);
        doGrupo.MapGet("/para-avaliar", ListarParaAvaliar.HandleAsync);

        var atividade = routes.MapGroup("/api/atividades/{id:guid}").RequireSessao();
        atividade.MapGet("/", ObterAtividade.HandleAsync);
        atividade.MapDelete("/", ExcluirAtividade.HandleAsync);
        atividade.MapPut("/duracao", CorrigirDuracao.HandleAsync);
        atividade.MapPost("/terminar", TerminarTreino.HandleAsync);
        atividade.MapGet("/mapa/{dx:int}/{dy:int}", ObterMapaAtividade.HandleAsync);

        var upload = atividade.MapGroup("");
        upload.MapPost("/evidencias", AnexarEvidencia.HandleAsync)
            .RequireRateLimiting(UploadPolicy)
            .AddEndpointFilter<LimiteUploadFilter>();
        upload.MapGet("/evidencias/{evidenciaId:guid}", ObterEvidencia.HandleAsync);
        upload.MapGet("/evidencias/{evidenciaId:guid}/miniatura", ObterMiniatura.HandleAsync);
        upload.MapGet("/evidencias/{evidenciaId:guid}/mapa/{dx:int}/{dy:int}", ObterMapaEvidencia.HandleAsync);
    }
}
