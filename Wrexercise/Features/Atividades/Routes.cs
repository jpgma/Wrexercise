using System.Threading.RateLimiting;
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Files;
namespace Wrexercise.Features.Atividades;

internal static class AtividadesRoutes
{
    public const string UploadPolicy = "upload";

    /// <summary>Os lotes de pontos do Percurso: um a cada 30 segundos, com folga para reenvios (ADR 0084).</summary>
    public const string PercursoPolicy = "percurso";

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
            options.AddPolicy(PercursoPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
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
        atividade.MapPost("/percurso/pontos", EnviarPontosPercurso.HandleAsync).RequireRateLimiting(PercursoPolicy);
        atividade.MapGet("/percurso", ObterPercurso.HandleAsync);
        atividade.MapGet("/percurso/mapa/{z:int}/{x:int}/{y:int}", ObterMapaPercurso.HandleAsync);

        var upload = atividade.MapGroup("");
        upload.MapPost("/evidencias", AnexarEvidencia.HandleAsync)
            .RequireRateLimiting(UploadPolicy)
            .AddEndpointFilter<LimiteUploadFilter>();
        upload.MapPost("/percurso/imagem", AnexarImagemPercurso.HandleAsync)
            .RequireRateLimiting(UploadPolicy)
            .AddEndpointFilter<LimiteUploadFilter>();
        upload.MapGet("/evidencias/{evidenciaId:guid}", ObterEvidencia.HandleAsync);
        upload.MapGet("/evidencias/{evidenciaId:guid}/miniatura", ObterMiniatura.HandleAsync);
        upload.MapGet("/evidencias/{evidenciaId:guid}/mapa/{dx:int}/{dy:int}", ObterMapaEvidencia.HandleAsync);
    }
}
