using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Infrastructure.Authentication;

/// <summary>
/// Proteção contra CSRF: escritas na API só são aceitas quando vêm da mesma origem. A exceção são as rotas
/// de apps conectados (ADR 0065) chamadas sem o cookie de sessão: sem cookie, o navegador não empresta
/// a identidade de ninguém, e essas rotas se autenticam pelo token do app.
/// </summary>
internal static class OriginCheck
{
    public static IApplicationBuilder UseOriginCheck(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (request.Path.StartsWithSegments("/api")
                && IsWrite(request.Method)
                && !IsAppSemCookie(context)
                && !IsSameOrigin(request.Headers.Origin, request.Headers["Sec-Fetch-Site"], request.Host.Value))
            {
                await ApiProblem.WriteAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "ORIGEM_INVALIDA",
                    "Requisição recusada.");
                return;
            }

            await next(context);
        });

    public static bool IsSameOrigin(string? origin, string? secFetchSite, string? host)
    {
        if (string.IsNullOrEmpty(origin))
        {
            return string.Equals(secFetchSite, "same-origin", StringComparison.OrdinalIgnoreCase);
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && string.Equals(uri.Authority, host, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAppSemCookie(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api/integracoes")
        && SessaoCookie.Read(context) is null;

    private static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}
