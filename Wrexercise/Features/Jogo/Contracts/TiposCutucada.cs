namespace Wrexercise.Features.Jogo.Contracts;

/// <summary>Uma das mensagens prontas da Cutucada (decisão 0063).</summary>
internal sealed record TipoCutucada(string Codigo, string Emoji, string Texto);

/// <summary>
/// As mensagens prontas da Cutucada, sem texto livre. Os códigos estão também no CHECK de
/// <c>cutucadas.tipo</c> e os textos em <c>webapp/src/features/jogo/textosCutucada.ts</c>: mude os três juntos.
/// </summary>
internal static class TiposCutucada
{
    public static readonly IReadOnlyList<TipoCutucada> Todos =
    [
        new("Sofa", "🛋️", "Levanta desse sofá!"),
        new("Frango", "🐔", "Tá com medo do treino?"),
        new("Meta", "📉", "Tua meta tá chorando."),
        new("Halter", "🏋️", "O halter sente sua falta."),
        new("Despertador", "⏰", "Acorda, dorminhoco!"),
        new("Fantasma", "👻", "Sumiu, foi?"),
    ];

    public static TipoCutucada? Find(string? codigo) => Todos.FirstOrDefault(t => t.Codigo == codigo);
}
