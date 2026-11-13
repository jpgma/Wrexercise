namespace Wrexercise.Features.Atividades;

internal sealed record CriarAtividadeRequest(
    string? Tipo,
    int? DuracaoSegundos,
    DateTimeOffset? StartedAt,
    string? Descricao,
    string? TipoComprovacao);

internal sealed record EvidenciaResponse(Guid Id, int Ordem, string Url);

internal sealed record AtividadeResumoResponse(
    Guid Id,
    Guid GrupoId,
    string? GrupoNome,
    Guid UsuarioId,
    string NomeExibicao,
    string Tipo,
    int DuracaoSegundos,
    DateTimeOffset StartedAt,
    string TipoComprovacao,
    string SituacaoVerificacao,
    int TotalEvidencias,
    DateTimeOffset SubmittedAt,
    string? Descricao,
    int Kudos,
    int Suspeito,
    int Rejeitar,
    string? MiniaturaUrl,
    int Comentarios,
    IReadOnlyList<string> Avisos);

/// <summary>Um <c>Aviso</c> sobre a atividade (ADR 0052). Todo o grupo vê, inclusive o autor.</summary>
internal sealed record AvisoResponse(string Tipo, Guid? EvidenciaId, Guid? AtividadeRelacionadaId);

internal sealed record AtividadesPaginadasResponse(
    IReadOnlyList<AtividadeResumoResponse> Atividades,
    string? ProximoCursor);

internal sealed record FilaAvaliacaoResponse(IReadOnlyList<AtividadeResumoResponse> Atividades);

internal sealed record AtividadeResponse(
    Guid Id,
    Guid GrupoId,
    string GrupoNome,
    Guid UsuarioId,
    string NomeExibicao,
    string Tipo,
    int DuracaoSegundos,
    DateTimeOffset StartedAt,
    string? Descricao,
    string TipoComprovacao,
    string SituacaoVerificacao,
    DateTimeOffset SubmittedAt,
    bool Voce,
    IReadOnlyList<EvidenciaResponse> Evidencias,
    IReadOnlyList<AvisoResponse> Avisos,
    DateTimeOffset? ExclusaoPermitidaAte);

internal sealed record EvidenciaCriadaResponse(Guid Id, int Ordem, string Url);
