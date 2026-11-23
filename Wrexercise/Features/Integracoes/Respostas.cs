using Wrexercise.Features.Atividades.Contracts;

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

    public static TreinoAppResponse BuildTreino(TreinoAppSituacao treino) =>
        new(
            treino.AtividadeId,
            treino.GrupoId,
            treino.GrupoNome,
            treino.Situacao,
            treino.Situacao == "EmAndamento"
                ? $"/grupos/{treino.GrupoId}/treino"
                : $"/atividades/{treino.AtividadeId}",
            treino.StartedAt,
            treino.FimInformado,
            treino.ResumoAnexado,
            treino.PercursoUltimoSeq);
}

/// <summary>
/// O treino ao vivo que o app começou (ADR 0081). <c>Url</c> é o cronômetro enquanto está aberto e a
/// atividade depois que a pessoa termina. <c>PercursoUltimoSeq</c>: o treino grava o Percurso que o app manda
/// (ADR 0085) e este é o maior <c>seq</c> já guardado (−1 sem nenhum); <c>null</c> sem percurso.
/// </summary>
internal sealed record TreinoAppResponse(
    Guid AtividadeId,
    Guid GrupoId,
    string GrupoNome,
    string Situacao,
    string Url,
    DateTimeOffset StartedAt,
    bool FimInformado,
    bool ResumoAnexado,
    int? PercursoUltimoSeq);

/// <summary>Um grupo de quem conectou o app: só o nome e o identificador (ADR 0081).</summary>
internal sealed record GrupoAppResponse(Guid Id, string Nome);

internal sealed record GruposAppResponse(IReadOnlyList<GrupoAppResponse> Grupos);
