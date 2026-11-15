namespace Wrexercise.Features.Integracoes;

/// <summary>
/// O que o app vê do rascunho. <c>Url</c> é o caminho no Wrexercise que o app abre: a tela de importar
/// enquanto está pendente, a atividade depois de postada.
/// </summary>
internal sealed record RascunhoSituacaoResponse(
    Guid Id,
    string Situacao,
    string Url,
    Guid? AtividadeId,
    DateTimeOffset ExpiresAt);

internal static class RespostasIntegracao
{
    public static RascunhoSituacaoResponse BuildSituacao(
        Guid id,
        Guid? atividadeId,
        DateTimeOffset expiresAt,
        DateTimeOffset agora) =>
        new(
            id,
            SituacaoRascunho.Calcular(atividadeId, expiresAt, agora),
            atividadeId is { } atividade ? $"/atividades/{atividade}" : $"/importar/{id}",
            atividadeId,
            expiresAt);
}
