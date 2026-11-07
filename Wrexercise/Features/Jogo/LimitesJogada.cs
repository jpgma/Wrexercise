namespace Wrexercise.Features.Jogo;

/// <summary>Uma Jogada já existente, do ponto de vista dos limites de envio.</summary>
internal sealed record JogadaLimite(Guid RemetenteId, Guid DestinatarioId, DateTimeOffset EnviadaEm, string Situacao);

/// <summary>
/// Os limites de envio (docs/gamificacao.md §6.3). Ficam em constantes para ajustar depois de observar o uso.
/// </summary>
internal static class LimitesJogada
{
    public const int CartasNaMao = 5;
    public const int RecebidasAtivas = 2;
    public const int EnviadasAtivas = 2;
    public static readonly TimeSpan IntervaloMesmaPessoa = TimeSpan.FromHours(24);

    public const string ErroRecebidas = "LIMITE_JOGADAS_RECEBIDAS";
    public const string ErroEnviadas = "LIMITE_JOGADAS_ENVIADAS";
    public const string ErroPar = "JOGADA_ATIVA_COM_PESSOA";
    public const string ErroRecente = "JOGADA_RECENTE";

    /// <param name="jogadas">As Jogadas em que o remetente ou o destinatário participam, em qualquer grupo.</param>
    /// <returns>O código do limite atingido, ou <c>null</c>.</returns>
    public static string? Verificar(
        Guid remetenteId,
        Guid destinatarioId,
        IReadOnlyList<JogadaLimite> jogadas,
        DateTimeOffset agora)
    {
        var ativas = jogadas.Where(j => SituacaoJogada.IsAtiva(j.Situacao)).ToList();

        if (ativas.Any(j => IsPar(j, remetenteId, destinatarioId)))
        {
            return ErroPar;
        }

        if (jogadas.Any(j => j.RemetenteId == remetenteId
            && j.DestinatarioId == destinatarioId
            && agora - j.EnviadaEm < IntervaloMesmaPessoa))
        {
            return ErroRecente;
        }

        if (ativas.Count(j => j.RemetenteId == remetenteId) >= EnviadasAtivas)
        {
            return ErroEnviadas;
        }

        if (ativas.Count(j => j.DestinatarioId == destinatarioId) >= RecebidasAtivas)
        {
            return ErroRecebidas;
        }

        return null;
    }

    private static bool IsPar(JogadaLimite jogada, Guid a, Guid b) =>
        (jogada.RemetenteId == a && jogada.DestinatarioId == b)
        || (jogada.RemetenteId == b && jogada.DestinatarioId == a);
}
