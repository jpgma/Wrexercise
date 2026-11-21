namespace Wrexercise.Features.Atividades;

internal sealed record CriarAtividadeRequest(
    string? Tipo,
    int? DuracaoSegundos,
    DateTimeOffset? StartedAt,
    string? Descricao,
    string? TipoComprovacao,
    Guid? RascunhoId = null);

/// <summary>
/// <c>Tipo</c>: <c>Foto</c> ou <c>Video</c>; <c>DuracaoMs</c> só no vídeo. <c>GravadaNoApp</c>: a câmera do
/// aplicativo fez a captura (ADR 0058). <c>Print</c>: o navegador reconheceu uma captura de tela, pelo nome do arquivo ou pelo tamanho da tela (ADR 0060).
/// Os dois são sinais do navegador. <c>Latitude</c> e <c>Longitude</c>: o GPS do arquivo original, só quando o
/// autor escolheu mostrá-lo ao grupo; sem essa escolha, o navegador nem o envia (ADR 0061).
/// <c>CapturadaEm</c> e <c>OrigemDataCaptura</c>: quando a foto ou o vídeo foi feito e de onde veio essa data
/// (<c>Exif</c>, <c>Arquivo</c>, <c>Camera</c>, <c>Print</c>, <c>Integracao</c>, <c>Percurso</c>; ADR 0052, 0080, 0084). Também é sinal, não prova.
/// <c>AdicionadaDepois</c>: chegou depois do prazo da verificação e não contou para ela (ADR 0083).
/// </summary>
internal sealed record EvidenciaResponse(
    Guid Id,
    int Ordem,
    string Url,
    bool GravadaNoApp,
    string Tipo,
    int? DuracaoMs,
    bool Print,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? CapturadaEm,
    string? OrigemDataCaptura,
    bool AdicionadaDepois);

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
    IReadOnlyList<string> Avisos,
    int? MiniaturaDuracaoMs,
    // A distância e o recorde, quando o treino gravou o Percurso (ADR 0084).
    PercursoResumoResponse? Percurso);

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
    DateTimeOffset? ExclusaoPermitidaAte,
    bool CorrecaoPermitida,
    IReadOnlyList<CorrecaoDuracaoResponse> Correcoes,
    // Onde o treino ao vivo começou, só quando a pessoa escolheu mostrar o local (ADR 0080).
    double? Latitude,
    double? Longitude,
    // O autor ainda manda fotos e vídeos por aqui; depois do prazo, com o selo "Adicionada depois" (ADR 0083).
    bool AnexoPermitido,
    // O resumo do Percurso; o trajeto vem de GET /api/atividades/{id}/percurso (ADR 0084).
    PercursoResumoResponse? Percurso);

/// <summary>Uma correção da duração feita por um administrador (ADR 0068). Todo o grupo vê.</summary>
internal sealed record CorrecaoDuracaoResponse(
    string ResponsavelNome,
    int DuracaoAnteriorSegundos,
    int DuracaoNovaSegundos,
    string Justificativa,
    DateTimeOffset CriadaEm);

internal sealed record EvidenciaCriadaResponse(Guid Id, int Ordem, string Url);
