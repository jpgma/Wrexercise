namespace Wrexercise.Features.Notificacoes;

/// <param name="Cutucada">Só no tipo <c>Cutucada</c>: o código da mensagem pronta (decisão 0063).</param>
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
    string? Cutucada = null);

internal sealed record NotificacoesResponse(IReadOnlyList<NotificacaoResponse> Notificacoes);

internal sealed record NotificacoesNaoLidasResponse(int Total);
