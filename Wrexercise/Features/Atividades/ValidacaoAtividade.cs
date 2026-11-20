using Wrexercise.Features.Grupos.Contracts;

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
    /// <summary>Treino ao vivo ainda não terminado (ADR 0078): não conta para nada e ninguém o avalia.</summary>
    public const string EmAndamento = "EmAndamento";
    public const string AguardandoFeedback = "AguardandoFeedback";
    public const string Verificada = "Verificada";
    public const string EmRevisao = "EmRevisao";
    public const string Contestada = "Contestada";
    public const string Rejeitada = "Rejeitada";
    public const string Anulada = "Anulada";

    public static bool ContaComoPendente(string situacao) =>
        situacao is AguardandoFeedback or EmRevisao or Contestada;
}

internal static class ValidacaoAtividade
{
    private const int MaxDescricaoLength = 500;
    private const int MinDuracaoSegundos = 60;
    private const int MaxDuracaoSegundos = 24 * 60 * 60;
    public const int MaxEvidencias = 10;
    public const int LimiteFeedPadrao = 20;
    public const int LimiteFeedMaximo = 50;

    public static bool IsValidDuracao(int? segundos) =>
        segundos is >= MinDuracaoSegundos and <= MaxDuracaoSegundos;

    public static bool IsValidDescricao(string? descricao) =>
        (descricao?.Trim().Length ?? 0) <= MaxDescricaoLength;

    public static string? NormalizeDescricao(string? descricao) =>
        string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();

    public static bool IsValidStartedAt(DateTimeOffset? startedAt) =>
        startedAt is { } instante && instante <= DateTimeOffset.UtcNow + AvisosEvidencia.ToleranciaFuturo;

    /// <summary>Até 48 horas no passado (ADR 0052): o mesmo prazo da janela de avaliação.</summary>
    public static bool IsStartedAtDentroDoLimite(DateTimeOffset startedAt) =>
        AvisosEvidencia.IsDentroDoLimiteNoPassado(startedAt, DateTimeOffset.UtcNow);

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

/// <summary>
/// ADR 0068: o proprietário ou um administrador corrige a duração da atividade de outra pessoa, também
/// depois de verificada. Rejeitada e Anulada não contam para nada, então não há o que corrigir.
/// </summary>
internal static class CorrecaoDuracao
{
    private const int MaxJustificativaLength = 500;

    public static bool Permitida(bool autor, string? papel, string situacao) =>
        !autor && PodeCorrigir(papel) && SituacaoPermite(situacao);

    public static bool PodeCorrigir(string? papel) =>
        papel is PapelMembro.Proprietario or PapelMembro.Administrador;

    public static bool SituacaoPermite(string situacao) =>
        situacao is not (SituacaoVerificacao.Rejeitada or SituacaoVerificacao.Anulada or SituacaoVerificacao.EmAndamento);

    public static bool IsValidJustificativa(string? justificativa) =>
        justificativa?.Trim().Length is >= 1 and <= MaxJustificativaLength;
}

/// <summary>
/// ADR 0054: o autor apaga a própria atividade logo depois de registrá-la, para desfazer um registro em
/// dobro ou com erro. Só enquanto ninguém a contestou: com Suspeito ou Rejeitar ela já está em revisão.
/// </summary>
internal static class ExclusaoAtividade
{
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Nos 15 minutos, só <c>Verificada</c> fecha a exclusão: ela já pode ter virado Energia e Jogada
    /// concluída. Uma reação de qualquer tipo não impede (ADR 0073).
    /// </summary>
    /// <returns>Até quando o autor pode apagar, ou <c>null</c> se não pode.</returns>
    public static DateTimeOffset? PermitidaAte(
        bool autor,
        string situacao,
        DateTimeOffset submittedAt,
        DateTimeOffset agora)
    {
        var limite = submittedAt + Janela;
        return autor && situacao != SituacaoVerificacao.Verificada && agora < limite ? limite : null;
    }

    /// <summary>O treino ao vivo se cancela a qualquer momento, até terminar (ADR 0078).</summary>
    public static bool Permitida(bool autor, string situacao, DateTimeOffset submittedAt, DateTimeOffset agora) =>
        (autor && situacao == SituacaoVerificacao.EmAndamento)
        || PermitidaAte(autor, situacao, submittedAt, agora) is not null;
}

/// <summary>
/// ADR 0078: o treino ao vivo. A pessoa começa agora e termina depois; o início fica travado e a duração
/// só diminui (quem esqueceu de terminar ajusta para menos).
/// </summary>
internal static class TreinoAoVivo
{
    /// <summary>Sem terminar até aqui, o servidor lembra a pessoa ("Ainda treinando?").</summary>
    public static readonly TimeSpan Lembrete = TimeSpan.FromHours(3);

    /// <summary>Esquecido por mais que a duração máxima, o treino expira e sai, com as evidências.</summary>
    public static readonly TimeSpan Expiracao = TimeSpan.FromHours(24);

    /// <summary>Duração provisória gravada ao começar; a real vem ao terminar.</summary>
    public const int DuracaoProvisoriaSegundos = 60;

    /// <summary>Segundos decorridos desde o início, arredondados para baixo.</summary>
    public static int Decorridos(DateTimeOffset startedAt, DateTimeOffset agora) =>
        (int)Math.Max(0, Math.Floor((agora - startedAt).TotalSeconds));

    /// <summary>A duração informada ao terminar: de 60 segundos até o tempo decorrido (e no máximo 24 horas).</summary>
    public static bool IsValidDuracaoFinal(int? segundos, DateTimeOffset startedAt, DateTimeOffset agora) =>
        ValidacaoAtividade.IsValidDuracao(segundos) && segundos <= Decorridos(startedAt, agora);

    /// <summary>Com evidências já enviadas, o treino não pode terminar como registro manual.</summary>
    public static bool IsValidComprovacaoFinal(string? tipo, int totalEvidencias) =>
        TipoComprovacao.IsValid(tipo) && (totalEvidencias == 0 || TipoComprovacao.ExigeEvidencia(tipo!));
}
