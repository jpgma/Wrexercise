namespace Wrexercise.Features.Jogo;

/// <param name="RecebeCartas">Se a pessoa está no modo Jogo: só assim pode receber cartas.</param>
/// <param name="DormindoDesde">Só com a pose <c>dormindo</c>: o início do sono, mostrado no Foco.</param>
internal sealed record MembroCenaResponse(
    Guid UsuarioId,
    string NomeExibicao,
    string Pose,
    bool RecebeCartas,
    DateTimeOffset? DormindoDesde);

internal sealed record CenaResponse(Guid GrupoId, IReadOnlyList<MembroCenaResponse> Membros);

/// <summary>A Energia ganha por origem. A soma é a Energia ganha total.</summary>
internal sealed record EnergiaDetalhamentoResponse(
    int Atividades,
    int Bonus,
    int Metas,
    int Sequencias,
    int Desafios,
    int Jogadas,
    int Rivais);

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

/// <param name="CartaRespostaId">A Resposta da Mão usada para aceitar, se houver.</param>
/// <param name="Objetivo">O objetivo novo, só com a MUDANÇA DE PLANOS.</param>
internal sealed record ResponderJogadaRequest(string? Resposta, Guid? CartaRespostaId = null, string? Objetivo = null);

internal sealed record MarcarJogadasVistasRequest(IReadOnlyList<Guid>? Ids);

internal sealed record MarcarRivaisVistosRequest(IReadOnlyList<Guid>? Ids);

/// <param name="Situacao">Do ponto de vista de quem escolheu: <c>EmAndamento</c>, <c>Venceu</c>, <c>Perdeu</c> ou <c>SemVencedor</c>.</param>
/// <param name="Escolhido">Se quem consulta escolheu este Rival; senão, foi escolhido.</param>
/// <param name="Nova">Se quem consulta ainda não viu a provocação (Rival) ou o resultado final (quem escolheu).</param>
internal sealed record RivalResponse(
    Guid Id,
    Guid EscolhedorId,
    string EscolhedorNome,
    Guid RivalId,
    string RivalNome,
    DateOnly SemanaInicio,
    string Situacao,
    bool Escolhido,
    bool Nova);

/// <param name="SemanaInicio">A segunda-feira da semana em curso do desafio ativo, ou <c>null</c> sem ele.</param>
/// <param name="Candidatos">Os colegas que quem consulta pode escolher agora.</param>
/// <param name="Vitorias">Quantas vezes quem consulta venceu o Rival que escolheu neste grupo.</param>
internal sealed record RivaisResponse(
    DateOnly? SemanaInicio,
    bool PodeEscolher,
    IReadOnlyList<Guid> Candidatos,
    int Vitorias,
    IReadOnlyList<RivalResponse> Rivais);

/// <param name="Objetivo">O código do objetivo em vigor: o da carta ou o trocado pela MUDANÇA DE PLANOS.</param>
/// <param name="CartaResposta">A Resposta usada no aceite, se houver.</param>
/// <param name="DesafioDuplo">Se a Jogada foi devolvida por um DESAFIO DUPLO.</param>
/// <param name="ObjetivosEquivalentes">
/// Os objetivos que a MUDANÇA DE PLANOS pode escolher. Só na Jogada recebida que espera resposta.
/// </param>
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
    string Objetivo,
    CartaCatalogoResponse? CartaResposta,
    bool DesafioDuplo,
    IReadOnlyList<string> ObjetivosEquivalentes,
    string Situacao,
    DateTimeOffset EnviadaEm,
    DateTimeOffset ExpiraEm,
    int? DuracaoAlvoSegundos,
    string? TipoEvitar,
    bool Recebida,
    bool Nova);

internal sealed record JogadasResponse(IReadOnlyList<JogadaResponse> Jogadas);

/// <param name="Folha">O endereço da folha importada; quem não importou nem aparece no mapa.</param>
/// <param name="Codigo">O Enfeite (a família).</param>
/// <param name="Conquista">A conquista do nível que define o desenho: <c>/enfeite-{conquista em minúsculas}.png</c>.</param>
internal sealed record EnfeiteResponse(string Codigo, string Conquista, string Nome);

/// <param name="TituloConquista">A conquista que libera o Enfeite, como o Perfil a mostra.</param>
/// <param name="Atual">O progresso da pessoa na conquista, de <paramref name="Alvo"/>.</param>
internal sealed record EnfeiteBloqueadoResponse(
    string Codigo,
    string Conquista,
    string Nome,
    string TituloConquista,
    int Atual,
    int Alvo);

/// <param name="Folha">O endereço da folha importada; sem ela vale a folha padrão.</param>
/// <param name="Vitrine">Os Enfeites da vitrine; vazia para quem está no modo <c>App</c>.</param>
internal sealed record AvatarMembroResponse(Guid UsuarioId, string? Folha, IReadOnlyList<EnfeiteResponse> Vitrine);

internal sealed record MeuAvatarResponse(
    string? Folha,
    IReadOnlyList<EnfeiteResponse> Vitrine,
    IReadOnlyList<EnfeiteResponse> Liberados,
    IReadOnlyList<string> Novos,
    IReadOnlyList<EnfeiteBloqueadoResponse> Bloqueados);

internal sealed record AvataresResponse(IReadOnlyList<AvatarMembroResponse> Avatares);

internal sealed record FolhaEnviadaResponse(string Folha);
