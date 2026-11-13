namespace Wrexercise.Features.Jogo;

/// <summary>Poses do Avatar na cena do grupo (ver docs/gamificacao.md §3.2).</summary>
internal static class PoseAvatar
{
    public const string Parado = "parado";
    public const string Treinando = "treinando";
    public const string Comemorando = "comemorando";
    public const string Dormindo = "dormindo";

    /// <summary>Sem atividade no grupo por este tempo, o Avatar dorme.</summary>
    public static readonly TimeSpan TempoParaDormir = TimeSpan.FromHours(48);

    /// <summary>Treinar vence comemorar, que vence dormir, que vence ficar parado.</summary>
    public static string Calcular(bool temAtividadePendente, bool metaCumprida, bool dormindo)
    {
        if (temAtividadePendente)
        {
            return Treinando;
        }

        if (metaCumprida)
        {
            return Comemorando;
        }

        return dormindo ? Dormindo : Parado;
    }

    /// <summary>
    /// <paramref name="acordadoEm"/> é a última atividade válida no grupo ou, se for mais recente, a
    /// entrada no grupo: quem acabou de entrar ainda não dorme.
    /// </summary>
    public static bool IsDormindo(DateTimeOffset acordadoEm, DateTimeOffset agora) =>
        agora - acordadoEm >= TempoParaDormir;
}
