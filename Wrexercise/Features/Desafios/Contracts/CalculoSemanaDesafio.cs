namespace Wrexercise.Features.Desafios.Contracts;

internal sealed record SemanaDesafio(int Numero, DateOnly Inicio, DateOnly Fim);

internal sealed record ProgressoSemanalDesafio(int Verificadas, int Provisorias, bool MetaCumprida);

/// <summary>Semanas alinhadas a segunda–domingo e índice SQL em ContagensAtividadeDesafio.</summary>
internal static class CalculoSemanaDesafio
{
    private const int DiasPorSemana = 7;

    public static int CountSemanas(DateOnly inicio, DateOnly fim) =>
        (fim.DayNumber - inicio.DayNumber + 1) / DiasPorSemana;

    /// <returns>A semana que contém <paramref name="hoje"/>, ou <c>null</c> fora do período.</returns>
    public static SemanaDesafio? GetSemana(DateOnly inicio, DateOnly fim, DateOnly hoje)
    {
        if (hoje < inicio || hoje > fim)
        {
            return null;
        }

        var indice = (hoje.DayNumber - inicio.DayNumber) / DiasPorSemana;
        var inicioSemana = inicio.AddDays(indice * DiasPorSemana);
        return new SemanaDesafio(indice + 1, inicioSemana, inicioSemana.AddDays(DiasPorSemana - 1));
    }

    /// <returns>Os dias que faltam para a semana fechar, contando hoje: 7 na segunda, 1 no domingo.</returns>
    public static int CountDiasRestantes(SemanaDesafio semana, DateOnly hoje) =>
        semana.Fim.DayNumber - hoje.DayNumber + 1;

    public static int GetIndiceSemana(DateOnly inicio, DateOnly diaLocal) =>
        (diaLocal.DayNumber - inicio.DayNumber) / DiasPorSemana;

    /// <summary>
    /// Uma semana conta para o membro se começou no dia da entrada dele ou depois: quem entra
    /// no meio de uma semana começa a contar na segunda-feira seguinte.
    /// </summary>
    public static bool ParticipaDaSemana(DateOnly inicioSemana, DateOnly entradaLocal) =>
        inicioSemana >= entradaLocal;

    /// <returns>O índice da semana em curso, da última se o desafio acabou, ou -1 antes do início.</returns>
    public static int GetIndiceSemanaMaisRecente(DateOnly inicio, DateOnly fim, DateOnly hoje)
    {
        if (hoje < inicio)
        {
            return -1;
        }

        return GetIndiceSemana(inicio, hoje > fim ? fim : hoje);
    }

    /// <summary>Semanas cumpridas em sequência, da mais recente para trás.</summary>
    public static int GetSequenciaAtual(IReadOnlyList<bool> cumpridas, int indiceRecente)
    {
        var sequencia = 0;
        for (var indice = indiceRecente; indice >= 0 && cumpridas[indice]; indice--)
        {
            sequencia++;
        }

        return sequencia;
    }

    /// <summary>A maior sequência de semanas consecutivas entre os índices cumpridos de um desafio.</summary>
    public static int GetMelhorSequencia(IEnumerable<int> indicesCumpridos)
    {
        var melhor = 0;
        var atual = 0;
        int? anterior = null;
        foreach (var indice in indicesCumpridos.Distinct().Order())
        {
            atual = indice - 1 == anterior ? atual + 1 : 1;
            melhor = Math.Max(melhor, atual);
            anterior = indice;
        }

        return melhor;
    }

    /// <summary>Só as verificadas cumprem a meta. As pendentes aparecem apenas no progresso provisório.</summary>
    public static ProgressoSemanalDesafio CalculateProgresso(int verificadas, int pendentes, int sessoesPorSemana) =>
        new(verificadas, verificadas + pendentes, verificadas >= sessoesPorSemana);
}
