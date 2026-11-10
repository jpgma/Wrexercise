namespace Wrexercise.Features.Atividades.Contracts;

/// <summary>Os tipos de atividade. Desafios os usa para validar as metas por tipo.</summary>
internal static class TipoAtividade
{
    public const string Musculacao = "Musculacao";
    public const string Corrida = "Corrida";
    public const string Caminhada = "Caminhada";
    public const string Ciclismo = "Ciclismo";
    public const string Natacao = "Natacao";
    public const string Esporte = "Esporte";
    public const string Yoga = "Yoga";
    public const string Calistenia = "Calistenia";
    public const string Outro = "Outro";

    private static readonly HashSet<string> Validos =
    [
        Musculacao,
        Corrida,
        Caminhada,
        Ciclismo,
        Natacao,
        Esporte,
        Yoga,
        Calistenia,
        Outro,
    ];

    public static bool IsValid(string? tipo) => tipo is not null && Validos.Contains(tipo);
}
