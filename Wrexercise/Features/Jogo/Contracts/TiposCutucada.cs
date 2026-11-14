namespace Wrexercise.Features.Jogo.Contracts;

/// <summary>Uma das mensagens prontas da Cutucada (decisão 0063).</summary>
internal sealed record TipoCutucada(string Codigo, string Emoji, string Texto);

/// <summary>
/// As mensagens prontas da Cutucada. O texto livre usa o tipo <see cref="Livre"/> (decisão 0064). Os códigos estão também no CHECK de
/// <c>cutucadas.tipo</c> e os textos em <c>webapp/src/features/jogo/textosCutucada.ts</c>: mude os três juntos.
/// </summary>
internal static class TiposCutucada
{
    /// <summary>A cutucada com texto livre de quem cutuca (decisão 0064). Não está em <see cref="Todos"/>.</summary>
    public const string Livre = "Livre";

    public const string EmojiLivre = "💬";

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
