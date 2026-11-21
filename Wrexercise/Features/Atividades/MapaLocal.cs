using Microsoft.Net.Http.Headers;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Files;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// O mini-mapa do local de uma evidência ou de um treino ao vivo (ADR 0080). O navegador pede só o deslocamento,
/// de −1 a 1, em volta do tile do ponto: as coordenadas vêm sempre do banco, e a rota nunca vira um proxy aberto.
/// </summary>
internal static class MapaLocal
{
    public static bool IsDeslocamentoValido(int dx, int dy) => dx is >= -1 and <= 1 && dy is >= -1 and <= 1;

    public static async Task<IResult> ResponderAsync(
        double latitude,
        double longitude,
        int dx,
        int dy,
        CacheMapas cache,
        HttpContext context,
        Func<IResult> naoEncontrado,
        CancellationToken cancellationToken)
    {
        if (TileMapa.De(latitude, longitude).Deslocar(dx, dy) is not { } tile)
        {
            return naoEncontrado();
        }

        return await ResponderTileAsync(tile, cache, context, cancellationToken);
    }

    /// <summary>O PNG do tile, do cache do servidor; sem o servidor de mapas, <c>503 MAPA_INDISPONIVEL</c>.</summary>
    public static async Task<IResult> ResponderTileAsync(
        TileMapa tile,
        CacheMapas cache,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (await cache.ObterAsync(tile, cancellationToken) is not { } caminho)
        {
            return ApiProblem.Create(
                StatusCodes.Status503ServiceUnavailable,
                "MAPA_INDISPONIVEL",
                "O mapa está indisponível no momento.");
        }

        // O tile em si não é privado, mas a URL diz que esta evidência tem local: nada de cache compartilhado.
        context.Response.Headers[HeaderNames.CacheControl] = "private, max-age=604800";
        return Results.File(caminho, "image/png");
    }
}
