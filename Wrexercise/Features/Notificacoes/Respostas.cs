namespace Wrexercise.Features.Notificacoes;

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
    DateTimeOffset UpdatedAt);

internal sealed record NotificacoesResponse(IReadOnlyList<NotificacaoResponse> Notificacoes);

internal sealed record NotificacoesNaoLidasResponse(int Total);
