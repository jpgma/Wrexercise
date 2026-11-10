namespace Wrexercise.Features.Atividades;

internal static class TipoComprovacao
{
    public const string RegistroManual = "RegistroManual";
    public const string Fotografia = "Fotografia";
    public const string CapturaDeTela = "CapturaDeTela";

    private static readonly HashSet<string> Validos =
    [
        RegistroManual,
        Fotografia,
        CapturaDeTela,
    ];

    public static bool IsValid(string? tipo) => tipo is not null && Validos.Contains(tipo);

    public static bool ExigeEvidencia(string tipo) =>
        tipo is Fotografia or CapturaDeTela;
}

internal static class SituacaoVerificacao
{
    public const string AguardandoFeedback = "AguardandoFeedback";
    public const string Verificada = "Verificada";
    public const string EmRevisao = "EmRevisao";
    public const string Contestada = "Contestada";
    public const string Rejeitada = "Rejeitada";

    public static bool ContaComoPendente(string situacao) =>
        situacao is AguardandoFeedback or EmRevisao or Contestada;
}

internal static class ValidacaoAtividade
{
    private const int MaxDescricaoLength = 500;
    private const int MinDuracaoSegundos = 60;
    private const int MaxDuracaoSegundos = 24 * 60 * 60;
    public const int MaxEvidencias = 3;
    public const int LimiteFeedPadrao = 20;
    public const int LimiteFeedMaximo = 50;

    public static bool IsValidDuracao(int? segundos) =>
        segundos is >= MinDuracaoSegundos and <= MaxDuracaoSegundos;

    public static bool IsValidDescricao(string? descricao) =>
        (descricao?.Trim().Length ?? 0) <= MaxDescricaoLength;

    public static string? NormalizeDescricao(string? descricao) =>
        string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();

    public static bool IsValidStartedAt(DateTimeOffset? startedAt) =>
        startedAt is { } instante && instante <= DateTimeOffset.UtcNow.AddHours(1);

    /// <summary>Traduz o filtro da lista para SQL fixo; $1 é sempre quem consulta.</summary>
    /// <returns><c>false</c> para um filtro desconhecido.</returns>
    public static bool TryGetFiltroSql(string? filtro, out string sql)
    {
        string? traduzido = filtro switch
        {
            null or "" => string.Empty,
            "Minhas" => "AND a.usuario_id = $1",
            "Pendentes" => "AND a.situacao_verificacao IN ('AguardandoFeedback', 'EmRevisao', 'Contestada')",
            "Verificadas" => "AND a.situacao_verificacao = 'Verificada'",
            "EmRevisao" => "AND a.situacao_verificacao = 'EmRevisao'",
            _ => null,
        };
        sql = traduzido ?? string.Empty;
        return traduzido is not null;
    }

    public static int NormalizeLimite(int? limite) =>
        limite is > 0 and <= LimiteFeedMaximo ? limite.Value : LimiteFeedPadrao;
}
