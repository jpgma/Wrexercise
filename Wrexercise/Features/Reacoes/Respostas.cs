namespace Wrexercise.Features.Reacoes;

internal sealed record ReacaoRequest(string? Tipo);

internal sealed record RevisaoRequest(string? Decisao, string? Justificativa);

internal sealed record ReacaoItemResponse(
    Guid UsuarioId,
    string NomeExibicao,
    string Tipo,
    DateTimeOffset UpdatedAt);

internal sealed record ReacoesResponse(
    int TotalRevisores,
    int RejeicoesNecessarias,
    int Kudos,
    int Suspeito,
    int Rejeitar,
    string? SuaReacao,
    bool VoceERevisor,
    bool JanelaAberta,
    DateTimeOffset RevisaoEncerraEm,
    IReadOnlyList<ReacaoItemResponse> Reacoes);

internal sealed record ComentarioRequest(string? Texto);

/// <summary><c>Texto</c> e <c>Midia</c>: um dos dois sempre vem; os dois juntos, quando a foto tem legenda (ADR 0083).</summary>
internal sealed record ComentarioResponse(
    Guid Id,
    Guid UsuarioId,
    string NomeExibicao,
    string? Texto,
    DateTimeOffset CreatedAt,
    ComentarioMidiaResponse? Midia);

/// <summary>A foto ou o vídeo do comentário. <c>Tipo</c>: <c>Foto</c> ou <c>Video</c>; <c>DuracaoMs</c> só no vídeo.</summary>
internal sealed record ComentarioMidiaResponse(string Url, string? MiniaturaUrl, string Tipo, int? DuracaoMs);

internal sealed record ComentariosResponse(IReadOnlyList<ComentarioResponse> Comentarios);

internal sealed record RevisaoRegistradaResponse(
    string SituacaoVerificacao,
    string Decisao,
    DateTimeOffset RegistradaEm);
