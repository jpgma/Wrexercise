using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Features.Integracoes;

/// <summary>Os apps que podem se conectar (ADR 0065). Só o Movr, por enquanto.</summary>
internal static class AppIntegracao
{
    public const string Movr = "Movr";

    public static bool IsValid(string? app) => app is Movr;
}

/// <summary>Situação de um rascunho, como o app a vê.</summary>
internal static class SituacaoRascunho
{
    public const string Pendente = "Pendente";
    public const string Postada = "Postada";
    public const string Expirada = "Expirada";

    public static string Calcular(Guid? atividadeId, DateTimeOffset expiresAt, DateTimeOffset agora) =>
        atividadeId is not null ? Postada
        : expiresAt <= agora ? Expirada
        : Pendente;
}

/// <summary>Regras puras da autorização: PKCE S256 e a lista fechada de endereços de retorno.</summary>
internal static class AutorizacaoIntegracao
{
    public static readonly TimeSpan ValidadeCodigo = TimeSpan.FromMinutes(5);

    /// <summary>O rascunho vale enquanto o registro aceitaria a atividade: 48 horas depois do início (ADR 0052).</summary>
    public static readonly TimeSpan ValidadeRascunho = TimeSpan.FromHours(48);

    private const string RetornoMovr = "movr://integracoes/wrexercise";
    private const string RetornoLocalPrefixo = "http://127.0.0.1:";
    private const string RetornoLocalCaminho = "/wx-callback";

    /// <summary>
    /// O app no celular recebe pelo esquema <c>movr://</c>; no computador de desenvolvimento, por um
    /// endereço local numa porta qualquer (RFC 8252). Nenhum outro endereço recebe um código.
    /// </summary>
    public static bool IsRedirectUriPermitida(string? app, string? redirectUri)
    {
        if (app != AppIntegracao.Movr || string.IsNullOrEmpty(redirectUri))
        {
            return false;
        }

        if (redirectUri == RetornoMovr)
        {
            return true;
        }

        if (!redirectUri.StartsWith(RetornoLocalPrefixo, StringComparison.Ordinal)
            || !redirectUri.EndsWith(RetornoLocalCaminho, StringComparison.Ordinal))
        {
            return false;
        }

        var porta = redirectUri.AsSpan(RetornoLocalPrefixo.Length, redirectUri.Length - RetornoLocalPrefixo.Length - RetornoLocalCaminho.Length);
        return porta.Length is > 0 and <= 5
            && int.TryParse(porta, out var numero)
            && numero is > 0 and <= 65535
            && porta[0] != '0';
    }

    /// <summary>O desafio do PKCE: Base64Url do SHA-256 do verificador, 43 caracteres.</summary>
    public static bool IsValidCodeChallenge(string? challenge) =>
        challenge is { Length: 43 } && challenge.All(IsBase64UrlChar);

    /// <summary>RFC 7636: o verificador tem de 43 a 128 caracteres não reservados.</summary>
    public static bool IsValidCodeVerifier(string? verifier) =>
        verifier is { Length: >= 43 and <= 128 } && verifier.All(c => IsBase64UrlChar(c) || c is '.' or '~');

    public static bool VerificarPkce(string verifier, string challenge)
    {
        var calculado = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(calculado),
            Encoding.ASCII.GetBytes(challenge));
    }

    /// <summary>O identificador do treino no app: letras, números, hífen e sublinhado, até 64 caracteres.</summary>
    public static bool IsValidExternalId(string? externalId) =>
        externalId is { Length: >= 1 and <= 64 } && externalId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static bool IsBase64UrlChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_';
}

internal static class ErrosIntegracao
{
    public static IResult InvalidData() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "DADOS_INVALIDOS",
        "Verifique os dados informados.");

    public static IResult CodigoInvalido() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "CODIGO_INVALIDO",
        "O código de conexão expirou ou já foi usado. Conecte de novo.");

    public static IResult RascunhoNaoEncontrado() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "RASCUNHO_NAO_ENCONTRADO",
        "Rascunho não encontrado.");

    public static IResult RascunhoJaPostado() => ApiProblem.Create(
        StatusCodes.Status409Conflict,
        "RASCUNHO_JA_POSTADO",
        "Esta atividade já foi postada.");

    public static IResult AtividadeMuitoAntiga() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "ATIVIDADE_MUITO_ANTIGA",
        "Registre atividades das últimas 48 horas.");

    public static IResult TreinoNaoEncontrado() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "TREINO_NAO_ENCONTRADO",
        "O treino ao vivo foi cancelado ou expirou.");

    public static IResult TreinoNaoEmAndamento() => ApiProblem.Create(
        StatusCodes.Status409Conflict,
        "TREINO_NAO_EM_ANDAMENTO",
        "Este treino já terminou.");

    public static IResult AppNaoEncontrado() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "APP_NAO_ENCONTRADO",
        "App conectado não encontrado.");
}
