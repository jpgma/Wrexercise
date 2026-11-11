using Microsoft.AspNetCore.StaticFiles;

namespace Wrexercise.Infrastructure.StaticFiles;

/// <summary>
/// Evita que o navegador ou a CDN (Cloudflare) mantenham um arquivo antigo depois de um novo build.
/// Só `/assets` tem hash no nome; todo o resto (index.html, sw.js, manifest e os arquivos de `public/`,
/// como `avatar-base-masculino.png`) tem nome fixo e é sempre revalidado (resposta 304 quando não mudou).
/// </summary>
internal static class CacheControleEstatico
{
    public static void Aplicar(StaticFileResponseContext contexto)
    {
        contexto.Context.Response.Headers.CacheControl = contexto.Context.Request.Path.StartsWithSegments("/assets")
            ? "public, max-age=31536000, immutable"
            : "no-cache, must-revalidate";
    }
}
