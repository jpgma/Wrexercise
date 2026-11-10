namespace Wrexercise.Features.Desafios;

/// <param name="Papel">O papel de quem consulta, para a tela decidir o que oferecer.</param>
/// <param name="Participando">Se quem consulta tem ao menos uma semana contando neste desafio.</param>
internal sealed record DesafioResponse(
    Guid Id,
    Guid GrupoId,
    string Nome,
    string? Descricao,
    DateOnly DataInicio,
    DateOnly DataFim,
    int DuracaoMinimaSegundos,
    int SessoesPorSemana,
    string Situacao,
    string FusoHorario,
    string Papel,
    bool Participando,
    int TotalParticipantes,
    IReadOnlyList<MetaPorTipoResponse> MetasPorTipo);

/// <summary>Uma alternativa à duração mínima do desafio, na ordem em que foi criada (decisão 0043).</summary>
internal sealed record MetaPorTipoResponse(
    Guid Id,
    string? Nome,
    IReadOnlyList<string> Tipos,
    int DuracaoMinimaSegundos);

internal sealed record DesafiosResponse(IReadOnlyList<DesafioResponse> Desafios);

internal sealed record SemanaResponse(int Numero, DateOnly Inicio, DateOnly Fim);

/// <param name="SessoesVerificadas">O progresso definitivo da semana.</param>
/// <param name="SessoesProvisorias">As verificadas somadas às que ainda aguardam verificação.</param>
/// <param name="ContaNestaSemana">Falso para quem entrou no grupo depois do início da semana atual.</param>
internal sealed record ProgressoParticipanteResponse(
    Guid UsuarioId,
    string NomeExibicao,
    int SessoesVerificadas,
    int SessoesProvisorias,
    bool MetaCumprida,
    bool ContaNestaSemana);

/// <summary>A soma das durações não rejeitadas de quem consulta num dia, no total e por tipo.</summary>
internal sealed record SegundosDoDiaResponse(
    DateOnly Data,
    int Segundos,
    IReadOnlyList<SegundosPorTipoResponse> SegundosPorTipo);

internal sealed record SegundosPorTipoResponse(string Tipo, int Segundos);

/// <summary>Um dia que conta para a meta de quem consulta.</summary>
/// <param name="MetaPorTipoId">
/// Nulo quando o dia alcançou a duração mínima do desafio; senão, a primeira meta por tipo cumprida.
/// </param>
internal sealed record DiaQueContaResponse(DateOnly Data, Guid? MetaPorTipoId);

/// <param name="SemanaAtual">Vazia quando o desafio não está em andamento.</param>
/// <param name="SeusDias">
/// Os dias da semana atual que contam para a meta de quem consulta, contando as atividades pendentes.
/// </param>
/// <param name="SeusSegundosPorDia">Os dias da semana atual com atividade de quem consulta, mesmo abaixo do mínimo.</param>
internal sealed record ProgressoResponse(
    int SessoesPorSemana,
    int TotalSemanas,
    SemanaResponse? SemanaAtual,
    IReadOnlyList<ProgressoParticipanteResponse> Participantes,
    IReadOnlyList<DiaQueContaResponse> SeusDias,
    IReadOnlyList<SegundosDoDiaResponse> SeusSegundosPorDia);
