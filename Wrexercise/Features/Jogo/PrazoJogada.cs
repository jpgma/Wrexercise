namespace Wrexercise.Features.Jogo;

/// <summary>
/// O prazo de uma Jogada, no horário local do grupo (docs/gamificacao.md §6.4 e decisão 0035).
/// O SQL converte o resultado para <c>timestamptz</c> com o fuso do grupo.
/// </summary>
internal static class PrazoJogada
{
    public static readonly TimeSpan Padrao = TimeSpan.FromHours(48);

    public static DateTime Calcular(string codigo, DateTime enviadaLocal) => codigo switch
    {
        // Até o fim do primeiro domingo depois do dia do envio: enviada num domingo, vale até o seguinte.
        CodigoCarta.FimDeSemana => enviadaLocal.Date.AddDays(DiasAteODomingoSeguinte(enviadaLocal.DayOfWeek) + 1),
        // Até as 08:00 da segunda manhã depois do envio: sempre há duas manhãs inteiras.
        CodigoCarta.AntesDas8 => enviadaLocal.Date.AddDays(2).AddHours(8),
        _ => enviadaLocal + Padrao,
    };

    private static int DiasAteODomingoSeguinte(DayOfWeek dia) =>
        dia == DayOfWeek.Sunday ? 7 : 7 - (int)dia;
}
