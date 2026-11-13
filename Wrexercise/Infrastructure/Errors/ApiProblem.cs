using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Wrexercise.Infrastructure.Json;

namespace Wrexercise.Infrastructure.Errors;

/// <param name="Referencia">Só no erro interno: o código que a pessoa repassa e que está no log.</param>
internal sealed record ProblemResponse(
    string Title,
    int Status,
    string Code,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Referencia = null);

/// <summary>Formato único de erro da API: application/problem+json com um código estável.</summary>
internal static class ApiProblem
{
    private const string ContentType = "application/problem+json";

    public static IResult Create(int status, string code, string title) =>
        Results.Json(
            new ProblemResponse(title, status, code),
            AppJsonContext.Default.ProblemResponse,
            ContentType,
            status);

    /// <summary>Categoria do log do erro interno. O log do próprio middleware fica desligado em Program.cs.</summary>
    public const string CategoriaErroInterno = "Wrexercise.ErroInterno";

    // Sem 0/O e 1/I/L: a pessoa lê o código numa captura de tela e o digita.
    private const string AlfabetoReferencia = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int TamanhoReferencia = 6;

    /// <summary>Para middlewares, que escrevem na resposta sem passar por um endpoint.</summary>
    public static Task WriteAsync(
        HttpContext context,
        int status,
        string code,
        string title,
        string? referencia = null)
    {
        context.Response.StatusCode = status;

        return context.Response.WriteAsJsonAsync(
            new ProblemResponse(title, status, code, referencia),
            AppJsonContext.Default.ProblemResponse,
            ContentType,
            context.RequestAborted);
    }

    /// <summary>
    /// Registra a exceção com uma referência curta e devolve só a referência: o cliente nunca recebe
    /// detalhes. Quem recebe o erro repassa o código, e <c>erros.bat CODIGO</c> acha a linha do log.
    /// </summary>
    public static Task WriteInternalErrorAsync(HttpContext context)
    {
        var referencia = NovaReferencia();
        var falha = context.Features.Get<IExceptionHandlerPathFeature>();
        context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(CategoriaErroInterno)
            .LogError(
                falha?.Error,
                "Erro interno {Referencia} em {Metodo} {Caminho}",
                referencia,
                context.Request.Method,
                falha?.Path ?? context.Request.Path.Value);

        return WriteAsync(
            context,
            StatusCodes.Status500InternalServerError,
            "ERRO_INTERNO",
            "Erro interno do servidor.",
            referencia);
    }

    public static string NovaReferencia() =>
        RandomNumberGenerator.GetString(AlfabetoReferencia, TamanhoReferencia);
}
