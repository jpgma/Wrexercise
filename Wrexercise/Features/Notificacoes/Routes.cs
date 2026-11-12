using Wrexercise.Infrastructure.Authentication;

namespace Wrexercise.Features.Notificacoes;

internal static class NotificacoesRoutes
{
    public static void MapNotificacoesRoutes(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/notificacoes").RequireSessao();
        group.MapGet("/", ListarNotificacoes.HandleAsync);
        group.MapGet("/nao-lidas", ListarNotificacoes.ContarNaoLidasAsync);
        group.MapPost("/lidas", MarcarLidas.MarcarTodasAsync);
        group.MapPost("/{id:guid}/lida", MarcarLidas.MarcarUmaAsync);
        group.MapGet("/push", InscricoesPush.ObterConfiguracao);
        group.MapPut("/push", InscricoesPush.SalvarAsync);
        group.MapDelete("/push", InscricoesPush.RemoverAsync);
    }
}
