namespace Wrexercise.Features.Notificacoes;

/// <param name="Cutucada">Só no tipo <c>Cutucada</c>: o código da mensagem pronta, ou <c>Livre</c> (decisões 0063 e 0064).</param>
/// <param name="CutucadaTexto">Só na Cutucada <c>Livre</c>: o texto que quem cutucou escreveu.</param>
/// <param name="Energia">
/// Só em <c>CartaRecusada</c> e <c>CartaNaoCumprida</c>: a Energia que voltou para quem enviou (decisão 0087).
/// </param>
internal sealed record NotificacaoResponse(
    Guid Id,
    string Tipo,
    Guid? AtividadeId,
    Guid? JogadaId,
    Guid GrupoId,
    string GrupoNome,
    string? AtorNome,
    string? Situacao,
    int Total,
    bool Lida,
    DateTimeOffset UpdatedAt,
    string? Cutucada = null,
    string? CutucadaTexto = null,
    int? Energia = null);

internal sealed record NotificacoesResponse(IReadOnlyList<NotificacaoResponse> Notificacoes);

internal sealed record NotificacoesNaoLidasResponse(int Total);
