namespace Wrexercise.Features.Jogo;

/// <param name="RecebeCartas">Se a pessoa está no modo Jogo: só assim pode receber cartas.</param>
internal sealed record MembroCenaResponse(Guid UsuarioId, string NomeExibicao, string Pose, bool RecebeCartas);

internal sealed record CenaResponse(Guid GrupoId, IReadOnlyList<MembroCenaResponse> Membros);

/// <summary>A Energia ganha por origem. A soma é a Energia ganha total.</summary>
internal sealed record EnergiaDetalhamentoResponse(
    int Atividades,
    int Bonus,
    int Metas,
    int Sequencias,
    int Desafios,
    int Jogadas);

internal sealed record EnergiaResponse(int Saldo, EnergiaDetalhamentoResponse Detalhamento);

internal sealed record CartaCatalogoResponse(string Codigo, string Nome, string Tipo, int Preco, string Efeito);

/// <param name="EmJogo">Se a carta está numa Jogada aberta: continua na Mão, mas não pode ser jogada.</param>
internal sealed record CartaMaoResponse(Guid Id, string Codigo, string Nome, string Tipo, int Preco, string Efeito, bool EmJogo);

internal sealed record LojaResponse(
    int Saldo,
    int LimiteMao,
    IReadOnlyList<CartaCatalogoResponse> Catalogo,
    IReadOnlyList<CartaMaoResponse> Mao);

internal sealed record ComprarCartaRequest(string? Codigo);

internal sealed record JogarCartaRequest(Guid? CartaId, Guid? DestinatarioId);

internal sealed record ResponderJogadaRequest(string? Resposta);

internal sealed record MarcarJogadasVistasRequest(IReadOnlyList<Guid>? Ids);

/// <param name="Recebida">Se quem consulta é o destinatário.</param>
/// <param name="Nova">Se quem consulta ainda não viu o que mudou: o envio (destinatário) ou a resposta (remetente).</param>
internal sealed record JogadaResponse(
    Guid Id,
    Guid GrupoId,
    Guid RemetenteId,
    string RemetenteNome,
    Guid DestinatarioId,
    string DestinatarioNome,
    CartaCatalogoResponse Carta,
    string Situacao,
    DateTimeOffset EnviadaEm,
    DateTimeOffset ExpiraEm,
    int? DuracaoAlvoSegundos,
    string? TipoEvitar,
    bool Recebida,
    bool Nova);

internal sealed record JogadasResponse(IReadOnlyList<JogadaResponse> Jogadas);
