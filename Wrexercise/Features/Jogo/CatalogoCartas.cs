namespace Wrexercise.Features.Jogo;

internal static class TipoCarta
{
    public const string Provocacao = "Provocacao";
    public const string Resposta = "Resposta";
}

internal static class CodigoCarta
{
    public const string Mais10Min = "Mais10Min";
    public const string Mais20Min = "Mais20Min";
    public const string Variedade = "Variedade";
    public const string FimDeSemana = "FimDeSemana";
    public const string AntesDas8 = "AntesDas8";
    public const string DesafioDuplo = "DesafioDuplo";
    public const string Aposta = "Aposta";
    public const string PrazoExtra = "PrazoExtra";
    public const string MudancaDePlanos = "MudancaDePlanos";
}

internal sealed record Carta(string Codigo, string Nome, string Tipo, int Preco, string Efeito);

/// <summary>O catálogo fica no código, sem tabela (docs/gamificacao.md §6.1).</summary>
internal static class CatalogoCartas
{
    public static readonly IReadOnlyList<Carta> Todas =
    [
        new(CodigoCarta.Mais10Min, "+10 MIN", TipoCarta.Provocacao, 30,
            "Somar num mesmo dia pelo menos a duração mínima do desafio + 10 minutos."),
        new(CodigoCarta.Mais20Min, "+20 MIN", TipoCarta.Provocacao, 45,
            "Somar num mesmo dia pelo menos a duração mínima do desafio + 20 minutos."),
        new(CodigoCarta.Variedade, "VARIEDADE", TipoCarta.Provocacao, 35,
            "Uma atividade de tipo diferente da última atividade verificada."),
        new(CodigoCarta.FimDeSemana, "FIM DE SEMANA", TipoCarta.Provocacao, 35,
            "Uma atividade que comece no sábado ou no domingo."),
        new(CodigoCarta.AntesDas8, "ANTES DAS 8", TipoCarta.Provocacao, 40,
            "Uma atividade que comece antes das 08:00."),
        new(CodigoCarta.DesafioDuplo, "DESAFIO DUPLO", TipoCarta.Resposta, 40,
            "Aceita a Provocação e devolve o mesmo objetivo para quem enviou."),
        new(CodigoCarta.Aposta, "APOSTA", TipoCarta.Resposta, 25,
            "Aceita a Provocação. Se cumprir, a recompensa dobra. Se não, a carta está perdida."),
        new(CodigoCarta.PrazoExtra, "PRAZO EXTRA", TipoCarta.Resposta, 20,
            "Aceita a Provocação com 24 horas a mais de prazo."),
        new(CodigoCarta.MudancaDePlanos, "MUDANÇA DE PLANOS", TipoCarta.Resposta, 25,
            "Aceita a Provocação trocando o objetivo por um equivalente."),
    ];

    public static Carta? Find(string? codigo) => Todas.FirstOrDefault(c => c.Codigo == codigo);

    public static bool IsDuracao(string codigo) => codigo is CodigoCarta.Mais10Min or CodigoCarta.Mais20Min;

    /// <summary>
    /// Os objetivos que a MUDANÇA DE PLANOS pode pôr no lugar de uma Provocação: os da mesma faixa de preço
    /// (decisão 0036).
    /// </summary>
    public static IReadOnlyList<string> GetEquivalentes(string codigo) => codigo switch
    {
        CodigoCarta.Mais10Min => [CodigoCarta.Variedade, CodigoCarta.FimDeSemana],
        CodigoCarta.Variedade => [CodigoCarta.Mais10Min, CodigoCarta.FimDeSemana],
        CodigoCarta.FimDeSemana => [CodigoCarta.Mais10Min, CodigoCarta.Variedade],
        CodigoCarta.Mais20Min => [CodigoCarta.AntesDas8],
        CodigoCarta.AntesDas8 => [CodigoCarta.Mais20Min],
        _ => [],
    };

    /// <summary>Os minutos a mais sobre a duração mínima do desafio, nas cartas de duração.</summary>
    public static int GetMinutosExtras(string codigo) => codigo switch
    {
        CodigoCarta.Mais10Min => 10,
        CodigoCarta.Mais20Min => 20,
        _ => 0,
    };
}
