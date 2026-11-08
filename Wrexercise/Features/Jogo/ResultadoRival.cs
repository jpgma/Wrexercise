namespace Wrexercise.Features.Jogo;

/// <summary>
/// O Rival da semana (docs/gamificacao.md §8 e decisão 0038). Vence quem cumpre a meta primeiro, pelo
/// <c>verified_at</c> da sessão que completou a meta. A situação é sempre do ponto de vista de quem escolheu.
/// </summary>
internal static class ResultadoRival
{
    public const string EmAndamento = "EmAndamento";
    public const string Venceu = "Venceu";
    public const string Perdeu = "Perdeu";
    public const string SemVencedor = "SemVencedor";

    /// <summary>A Energia para quem escolheu o Rival e venceu.</summary>
    public const int Energia = 15;

    /// <summary>
    /// Depois do domingo, a janela de verificação (48 horas) ainda pode completar a meta de alguém.
    /// Só depois dela "ninguém cumpriu" vira resultado.
    /// </summary>
    public const int DiasParaVerificar = 2;

    /// <param name="metaEscolhedor">Quando quem escolheu cumpriu a meta da semana, ou <c>null</c>.</param>
    /// <param name="metaRival">Quando o Rival cumpriu a meta da semana, ou <c>null</c>.</param>
    /// <param name="semanaInicio">A segunda-feira da semana.</param>
    /// <param name="hoje">Hoje, no fuso do grupo.</param>
    /// <remarks>
    /// Venceu e Perdeu são finais assim que aparecem: <c>Verificada</c> é final, e quem cumprir depois
    /// recebe um <c>verified_at</c> mais novo. Por isso a Energia do Rival só cresce.
    /// </remarks>
    public static string Calcular(
        DateTimeOffset? metaEscolhedor,
        DateTimeOffset? metaRival,
        DateOnly semanaInicio,
        DateOnly hoje)
    {
        if (metaEscolhedor is { } escolhedor && (metaRival is not { } rival || escolhedor < rival))
        {
            return Venceu;
        }

        if (metaRival is { } r && (metaEscolhedor is not { } e || r < e))
        {
            return Perdeu;
        }

        // Empate exato, ou ninguém cumpriu depois da semana e da janela de verificação.
        if (metaEscolhedor is not null || hoje > semanaInicio.AddDays(6 + DiasParaVerificar))
        {
            return SemVencedor;
        }

        return EmAndamento;
    }

    public static bool IsFinal(string situacao) => situacao != EmAndamento;

    /// <summary>
    /// Só antes de alguém dos dois cumprir a meta, contando as atividades ainda sem veredito: assim
    /// ninguém escolhe um Rival já sabendo que venceu.
    /// </summary>
    /// <param name="provisorias">As sessões verificadas mais as pendentes da semana, de cada um.</param>
    public static bool PodeEscolher(int provisoriasEscolhedor, int provisoriasRival, int sessoesPorSemana) =>
        provisoriasEscolhedor < sessoesPorSemana && provisoriasRival < sessoesPorSemana;
}
