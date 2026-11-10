using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Grupos.Contracts;

namespace Wrexercise.Features.Desafios;

internal static class ValidacaoDesafio
{
    private const int MaxNomeLength = 80;
    private const int MaxDescricaoLength = 500;
    private const int MinDuracaoSegundos = 60;
    private const int MaxDuracaoSegundos = 24 * 60 * 60;
    private const int MaxSessoesPorSemana = 7;
    private const int MaxSemanas = 52;
    public const int MaxMetasPorTipo = 4;
    private const int MaxNomeMetaLength = 20;

    public static string NormalizeNome(string nome) => nome.Trim();

    public static bool IsValidNome(string? nome)
    {
        var length = nome?.Trim().Length ?? 0;
        return length is > 0 and <= MaxNomeLength;
    }

    /// <summary>A descrição é opcional: só espaços equivalem a não ter descrição.</summary>
    public static string? NormalizeDescricao(string? descricao) =>
        string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();

    public static bool IsValidDescricao(string? descricao) =>
        (descricao?.Trim().Length ?? 0) <= MaxDescricaoLength;

    public static bool IsValidDuracaoMinima(int? segundos) =>
        segundos is >= MinDuracaoSegundos and <= MaxDuracaoSegundos;

    public static bool IsValidSessoesPorSemana(int? sessoes) =>
        sessoes is > 0 and <= MaxSessoesPorSemana;

    /// <summary>
    /// As metas por tipo são alternativas à duração mínima do desafio (decisão 0043): cada uma tem tipos
    /// válidos e sem repetição, e um mínimo menor que o do desafio, senão ela nunca faria um dia contar.
    /// Duas metas com o mesmo conjunto de tipos seriam redundantes.
    /// </summary>
    public static bool IsValidMetasPorTipo(IReadOnlyList<MetaPorTipoRequest?>? metas, int duracaoMinimaSegundos)
    {
        if (metas is null)
        {
            return true;
        }

        if (metas.Count > MaxMetasPorTipo)
        {
            return false;
        }

        var conjuntos = new HashSet<string>();
        foreach (var meta in metas)
        {
            if (meta?.Tipos is not { Count: > 0 } tipos
                || !tipos.All(TipoAtividade.IsValid)
                || tipos.Distinct().Count() != tipos.Count
                || meta.DuracaoMinimaSegundos is not { } minimo
                || minimo < MinDuracaoSegundos
                || minimo >= duracaoMinimaSegundos
                || (meta.Nome?.Trim().Length ?? 0) > MaxNomeMetaLength
                || !conjuntos.Add(string.Join(',', tipos.Order(StringComparer.Ordinal))))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>O nome da meta por tipo é opcional: só espaços equivalem a não ter nome.</summary>
    public static string? NormalizeNomeMeta(string? nome) =>
        string.IsNullOrWhiteSpace(nome) ? null : nome.Trim();

    /// <summary>
    /// Só semanas inteiras, de segunda a domingo, e sem começar no passado: uma semana parcial
    /// não poderia cobrar a meta inteira, e um início retroativo mudaria semanas já vividas.
    /// </summary>
    public static bool IsValidPeriodo(DateOnly inicio, DateOnly fim, DateOnly hoje) =>
        inicio.DayOfWeek == DayOfWeek.Monday
        && fim.DayOfWeek == DayOfWeek.Sunday
        && fim > inicio
        && inicio >= hoje
        && ProgressoDesafio.CountSemanas(inicio, fim) <= MaxSemanas;

    public static bool CanGerenciar(string papel) =>
        papel is PapelMembro.Proprietario or PapelMembro.Administrador;
}
