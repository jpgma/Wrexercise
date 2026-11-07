namespace Wrexercise.Features.Jogo;

/// <summary>Poses do Avatar na cena do grupo (ver docs/gamificacao.md §3.2).</summary>
internal static class PoseAvatar
{
    public const string Parado = "parado";
    public const string Treinando = "treinando";
    public const string Comemorando = "comemorando";

    /// <summary>Treinar vence comemorar, que vence ficar parado.</summary>
    public static string Calcular(bool temAtividadePendente, bool metaCumprida)
    {
        if (temAtividadePendente)
        {
            return Treinando;
        }

        return metaCumprida ? Comemorando : Parado;
    }
}
