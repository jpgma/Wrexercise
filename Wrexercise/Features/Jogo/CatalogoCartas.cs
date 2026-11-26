namespace Wrexercise.Features.Jogo;

internal static class TipoCarta
{
    public const string Provocacao = "Provocacao";
    public const string Resposta = "Resposta";

    /// <summary>Vale para o grupo inteiro, não para uma pessoa (decisão 0091).</summary>
    public const string Global = "Global";
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
    public const string TodoMundo = "TodoMundo";
}

internal sealed record Carta(string Codigo, string Nome, string Tipo, int Preco, string Efeito);

/// <summary>
/// O catálogo fica no código, sem tabela (docs/gamificacao.md §6.1). O efeito é o texto curto que a interface
/// mostra; a regra exata de cada carta está na documentação.
/// </summary>
internal static class CatalogoCartas
{
    public static readonly IReadOnlyList<Carta> Todas =
    [
        new(CodigoCarta.Mais10Min, "+10 MIN", TipoCarta.Provocacao, 30,
            "Num mesmo dia, treinar 10 min a mais que o mínimo do desafio."),
        new(CodigoCarta.Mais20Min, "+20 MIN", TipoCarta.Provocacao, 45,
            "Num mesmo dia, treinar 20 min a mais que o mínimo do desafio."),
        new(CodigoCarta.Variedade, "VARIEDADE", TipoCarta.Provocacao, 35,
            "Um treino de outro tipo: não vale repetir o do último treino."),
        new(CodigoCarta.FimDeSemana, "FIM DE SEMANA", TipoCarta.Provocacao, 35,
            "Um treino no sábado ou no domingo."),
        new(CodigoCarta.AntesDas8, "ANTES DAS 8", TipoCarta.Provocacao, 40,
            "Um treino que termine até as 8h."),
        new(CodigoCarta.DesafioDuplo, "DESAFIO DUPLO", TipoCarta.Resposta, 40,
            "Você aceita e devolve o mesmo desafio pra quem mandou."),
        new(CodigoCarta.Aposta, "APOSTA", TipoCarta.Resposta, 25,
            "Você aceita valendo o dobro. Se não cumprir, a APOSTA se perde."),
        new(CodigoCarta.PrazoExtra, "PRAZO EXTRA", TipoCarta.Resposta, 20,
            "Você aceita com 24 horas a mais de prazo."),
        new(CodigoCarta.MudancaDePlanos, "MUDANÇA DE PLANOS", TipoCarta.Resposta, 25,
            "Você aceita, mas troca o objetivo por outro do mesmo nível."),
        new(CodigoCarta.TodoMundo, "TODO MUNDO", TipoCarta.Global, 150,
            "Essa semana todo mundo vai de um treino que você escolhe. Quem cumprir ganha +30 EN."),
    ];

    public static Carta? Find(string? codigo) => Todas.FirstOrDefault(c => c.Codigo == codigo);

    public static bool IsDuracao(string codigo) => codigo is CodigoCarta.Mais10Min or CodigoCarta.Mais20Min;

    /// <summary>O bônus de quem cumpre uma Fusão, em % da soma dos preços das duas cartas (decisão 0070).</summary>
    public const int BonusFusaoPorCento = 20;

    /// <summary>Uma Fusão junta duas Provocações diferentes numa Jogada só (decisão 0070).</summary>
    public static bool PodeFundir(Carta a, Carta b) =>
        a.Tipo == TipoCarta.Provocacao && b.Tipo == TipoCarta.Provocacao && a.Codigo != b.Codigo;

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
