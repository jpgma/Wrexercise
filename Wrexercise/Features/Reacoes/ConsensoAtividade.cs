namespace Wrexercise.Features.Reacoes;

internal static class TipoReacao
{
    public const string Kudos = "Kudos";
    public const string Suspeito = "Suspeito";
    public const string Rejeitar = "Rejeitar";

    private static readonly HashSet<string> Validos = [Kudos, Suspeito, Rejeitar];

    public static bool IsValid(string? tipo) => tipo is not null && Validos.Contains(tipo);
}

internal readonly record struct ContagensReacao(int Kudos, int Suspeito, int Rejeitar)
{
    public int TotalRegistradas => Kudos + Suspeito + Rejeitar;

    public bool TemReacaoNegativa => Suspeito > 0 || Rejeitar > 0;
}

internal static class ConsensoAtividade
{
    public static readonly TimeSpan DuracaoJanela = TimeSpan.FromHours(48);

    public static int CalculateRejeicoesNecessarias(int totalRevisores)
    {
        if (totalRevisores <= 0)
        {
            return int.MaxValue;
        }

        var metadeParaCima = (totalRevisores + 1) / 2;
        return Math.Min(totalRevisores, Math.Max(2, metadeParaCima));
    }

    public static bool JanelaAberta(DateTimeOffset submittedAt, DateTimeOffset now) =>
        now < submittedAt + DuracaoJanela;

    public static DateTimeOffset RevisaoEncerraEm(DateTimeOffset submittedAt) =>
        submittedAt + DuracaoJanela;

    /// <summary>
    /// Calcula a situação após reações comunitárias ou encerramento da janela.
    /// <see cref="SituacaoVerificacao.Verificada"/>, <see cref="SituacaoVerificacao.Rejeitada"/> e
    /// <see cref="SituacaoVerificacao.Anulada"/>
    /// são finais. Com a janela encerrada, <see cref="SituacaoVerificacao.EmRevisao"/> só muda
    /// por revisão administrativa. Com um aviso que bloqueia (ADR 0052), o silêncio do grupo não
    /// verifica: ao fim da janela, a atividade vai para <see cref="SituacaoVerificacao.EmRevisao"/>.
    /// </summary>
    public static string DeterminarSituacao(
        int totalRevisores,
        ContagensReacao contagens,
        bool janelaEncerrada,
        string situacaoAtual,
        bool temAvisoBloqueante = false)
    {
        if (totalRevisores == 0)
        {
            return SituacaoVerificacao.Verificada;
        }

        if (situacaoAtual is SituacaoVerificacao.Verificada or SituacaoVerificacao.Rejeitada
            or SituacaoVerificacao.Anulada)
        {
            return situacaoAtual;
        }

        if (situacaoAtual == SituacaoVerificacao.EmRevisao && janelaEncerrada)
        {
            return SituacaoVerificacao.EmRevisao;
        }

        var limite = CalculateRejeicoesNecessarias(totalRevisores);
        if (contagens.Rejeitar >= limite)
        {
            return SituacaoVerificacao.Rejeitada;
        }

        if (contagens.TemReacaoNegativa)
        {
            return SituacaoVerificacao.EmRevisao;
        }

        if (contagens.TotalRegistradas == totalRevisores)
        {
            return SituacaoVerificacao.Verificada;
        }

        if (janelaEncerrada)
        {
            return temAvisoBloqueante ? SituacaoVerificacao.EmRevisao : SituacaoVerificacao.Verificada;
        }

        return SituacaoVerificacao.AguardandoFeedback;
    }
}

internal static class SituacaoVerificacao
{
    public const string AguardandoFeedback = "AguardandoFeedback";
    public const string Verificada = "Verificada";
    public const string EmRevisao = "EmRevisao";
    public const string Rejeitada = "Rejeitada";
    public const string Anulada = "Anulada";
}

internal static class ValidacaoRevisao
{
    public const int MaxJustificativaLength = 500;

    public static bool IsValidJustificativa(string? justificativa) =>
        justificativa is { } texto
        && !string.IsNullOrWhiteSpace(texto)
        && texto.Trim().Length <= MaxJustificativaLength;

    public static bool IsValidTexto(string? texto) => IsValidJustificativa(texto);

    public static string NormalizeJustificativa(string justificativa) => justificativa.Trim();
}
