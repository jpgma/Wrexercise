namespace Wrexercise.Features.Jogo;

/// <param name="RecebeCartas">Se a pessoa está no modo Jogo: só assim pode receber cartas.</param>
/// <param name="DormindoDesde">Só com a pose <c>dormindo</c>: o início do sono, mostrado no Foco.</param>
/// <param name="Cutucadas">Só com a pose <c>dormindo</c>: as cutucadas recebidas nas últimas 24 horas.</param>
/// <param name="CutucadoPorMimEm">Só com a pose <c>dormindo</c>: quando quem vê cutucou este membro nas últimas 24 horas.</param>
/// <param name="Fadiga">A FADIGA do membro, que todo o grupo vê (decisão 0089), ou <c>null</c>.</param>
internal sealed record MembroCenaResponse(
    Guid UsuarioId,
    string NomeExibicao,
    string Pose,
    bool RecebeCartas,
    DateTimeOffset? DormindoDesde,
    CutucadasMembroResponse? Cutucadas = null,
    DateTimeOffset? CutucadoPorMimEm = null,
    TreinoAoVivoResponse? TreinoAoVivo = null,
    FadigaMembroResponse? Fadiga = null);

/// <summary>A bateria fraca de um colega (decisão 0089): as cargas e o motivo, com a carta e quem a mandou.</summary>
/// <param name="Restantes">Quantos treinos verificados faltam para recarregar (1 a 5).</param>
/// <param name="Motivo"><c>Recusa</c>, <c>Falha</c> ou <c>Expiracao</c>.</param>
/// <param name="Carta">O nome da carta, como "+20 MIN" ou "+10 MIN + VARIEDADE" na Fusão.</param>
/// <param name="RemetenteNome">Quem mandou a carta.</param>
internal sealed record FadigaMembroResponse(int Restantes, string? Motivo, string? Carta, string? RemetenteNome);

/// <summary>
/// O treino ao vivo de um membro (ADR 0078). <c>TorcidaPorMimEm</c>: quando quem vê torceu por último, para o
/// botão "Torcer" esperar o intervalo.
/// </summary>
internal sealed record TreinoAoVivoResponse(
    Guid AtividadeId,
    string Tipo,
    DateTimeOffset StartedAt,
    int Torcidas,
    DateTimeOffset? TorcidaPorMimEm,
    // A distância até agora, quando o treino grava o Percurso (ADR 0084): só aparece no Foco, nunca pontua.
    int? DistanciaMetros);

/// <param name="UltimoTipo">O código da mensagem pronta da cutucada mais recente.</param>
internal sealed record CutucadasMembroResponse(int Total, string UltimoTipo);

internal sealed record CenaResponse(Guid GrupoId, IReadOnlyList<MembroCenaResponse> Membros);

/// <summary>A Energia ganha por origem. A soma é a Energia ganha total.</summary>
/// <param name="Fadiga">O que a FADIGA deixou de render, como valor negativo ou zero.</param>
/// <param name="Despertador">O que rendeu acordar colegas com a Cutucada.</param>
/// <param name="Devolucoes">Metade do preço das cartas enviadas e não cumpridas (decisão 0087).</param>
/// <param name="Convocacoes">As recompensas e as devoluções das Convocações (decisão 0091).</param>
internal sealed record EnergiaDetalhamentoResponse(
    int Atividades,
    int Bonus,
    int Metas,
    int Sequencias,
    int Desafios,
    int Jogadas,
    int Rivais,
    int Fadiga = 0,
    int Despertador = 0,
    int Devolucoes = 0,
    int Convocacoes = 0);

/// <param name="FadigaRestante">Quantos dos próximos dias verificados ainda rendem metade da base (0 a 3).</param>
internal sealed record EnergiaResponse(int Saldo, EnergiaDetalhamentoResponse Detalhamento, int FadigaRestante = 0);

internal sealed record CartaCatalogoResponse(string Codigo, string Nome, string Tipo, int Preco, string Efeito);

internal sealed record CartaMaoResponse(Guid Id, string Codigo, string Nome, string Tipo, int Preco, string Efeito);

internal sealed record LojaResponse(
    int Saldo,
    int LimiteMao,
    IReadOnlyList<CartaCatalogoResponse> Catalogo,
    IReadOnlyList<CartaMaoResponse> Mao);

internal sealed record ComprarCartaRequest(string? Codigo);

/// <param name="CartaFusaoId">A segunda Provocação da Mão, para jogar uma Fusão (decisão 0070).</param>
internal sealed record JogarCartaRequest(Guid? CartaId, Guid? DestinatarioId, Guid? CartaFusaoId = null);

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
/// <param name="CartaFusao">A segunda Provocação de uma Fusão, ou <c>null</c> (decisão 0070).</param>
/// <param name="Recompensa">A Energia de quem cumpre, já com o bônus da Fusão e a APOSTA.</param>
/// <param name="JaCumpre">
/// Na carta recebida que espera resposta: um treino feito depois que ela chegou já cumpre o objetivo (decisão 0076).
/// </param>
/// <param name="Devolucao">
/// Para quem enviou: a Energia que voltou da carta recusada, não cumprida ou expirada (decisão 0087). Senão, 0.
/// </param>
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
    bool Nova,
    CartaCatalogoResponse? CartaFusao,
    int Recompensa,
    DateTimeOffset? RespondidaEm,
    bool JaCumpre,
    int Devolucao = 0);

internal sealed record JogadasResponse(IReadOnlyList<JogadaResponse> Jogadas);

/// <summary>Uma carta recebida que espera resposta, em qualquer grupo: a Chegada (decisão 0076).</summary>
internal sealed record JogadaPendenteResponse(Guid Id, Guid GrupoId, DateTimeOffset EnviadaEm, DateTimeOffset ExpiraEm);

internal sealed record JogadasPendentesResponse(IReadOnlyList<JogadaPendenteResponse> Jogadas);

/// <param name="Folha">O endereço da folha importada; quem não importou nem aparece no mapa.</param>
/// <param name="Codigo">O Enfeite (a família).</param>
/// <param name="Conquista">A conquista do nível que define o desenho: <c>/enfeite-{conquista em minúsculas}.png</c>.</param>
internal sealed record EnfeiteResponse(string Codigo, string Conquista, string Nome);

/// <param name="TituloConquista">A conquista que libera o Enfeite, como o Perfil a mostra.</param>
/// <param name="Atual">O progresso da pessoa na conquista, de <paramref name="Alvo"/>.</param>
/// <param name="Falta">O que falta para liberar, por extenso ("Faltam 2 treinos no fim de semana").</param>
internal sealed record EnfeiteBloqueadoResponse(
    string Codigo,
    string Conquista,
    string Nome,
    string TituloConquista,
    int Atual,
    int Alvo,
    string? Falta);

/// <summary>Uma conquista que a pessoa ainda não comemorou, com o Enfeite que ela liberou ou evoluiu.</summary>
/// <param name="Conquista">O código da conquista; define o desenho do Enfeite.</param>
/// <param name="Titulo">O título da conquista, como o Perfil a mostra.</param>
/// <param name="Enfeite">O código do Enfeite (a família).</param>
internal sealed record ConquistaNovaResponse(string Conquista, string Titulo, string Enfeite, string NomeEnfeite);

/// <param name="Folha">O endereço da folha importada; sem ela vale a folha padrão.</param>
/// <param name="Vitrine">Os Enfeites da vitrine; vazia para quem está no modo <c>App</c>.</param>
internal sealed record AvatarMembroResponse(Guid UsuarioId, string? Folha, IReadOnlyList<EnfeiteResponse> Vitrine);

internal sealed record MeuAvatarResponse(
    string? Folha,
    IReadOnlyList<EnfeiteResponse> Vitrine,
    IReadOnlyList<EnfeiteResponse> Liberados,
    IReadOnlyList<string> Novos,
    IReadOnlyList<EnfeiteBloqueadoResponse> Bloqueados,
    IReadOnlyList<ConquistaNovaResponse> ConquistasNovas);

internal sealed record AvataresResponse(IReadOnlyList<AvatarMembroResponse> Avatares);

internal sealed record FolhaEnviadaResponse(string Folha);

/// <summary>Uma pessoa convidada para a Convocação (decisão 0091).</summary>
/// <param name="Situacao">A participação calculada (<see cref="SituacaoConvocacao"/>).</param>
internal sealed record ParticipanteConvocacaoResponse(Guid UsuarioId, string NomeExibicao, string Situacao, bool Anfitriao);

/// <summary>Uma Convocação do grupo, do ponto de vista de quem consulta.</summary>
/// <param name="Situacao"><c>Ativa</c>, <c>AguardandoVerificacao</c> ou <c>Encerrada</c>.</param>
/// <param name="MinhaSituacao">A participação de quem consulta, ou <c>null</c> se não foi convidado.</param>
/// <param name="MinhaRecompensa">A Energia que quem consulta ganhou cumprindo o objetivo.</param>
/// <param name="Devolucao">Só para o anfitrião: a Energia que já voltou da carta.</param>
/// <param name="DevolucaoPorPessoa">Só para o anfitrião: a parte do preço que cada convidado devolve.</param>
/// <param name="PodeSair">Se quem consulta pode desistir agora.</param>
internal sealed record ConvocacaoResponse(
    Guid Id,
    Guid GrupoId,
    string Codigo,
    string Nome,
    string TipoAtividade,
    Guid AnfitriaoId,
    string AnfitriaoNome,
    DateTimeOffset CriadaEm,
    DateTimeOffset ExpiraEm,
    string Situacao,
    int Concluidas,
    string? MinhaSituacao,
    int MinhaRecompensa,
    int? Devolucao,
    int? DevolucaoPorPessoa,
    bool PodeSair,
    IReadOnlyList<ParticipanteConvocacaoResponse> Participantes);

/// <param name="Destaque">A Convocação ativa, ou a última encerrada há até 3 dias, para o painel da praça.</param>
/// <param name="Recentes">As Convocações mais recentes do grupo, com a do destaque.</param>
internal sealed record ConvocacoesResponse(ConvocacaoResponse? Destaque, IReadOnlyList<ConvocacaoResponse> Recentes);

/// <summary>O que o TODO MUNDO vai pedir, antes de convocar.</summary>
/// <param name="Tipos">Os tipos de treino que o anfitrião pode escolher.</param>
/// <param name="Convidados">Quantos colegas no modo Jogo serão convidados, sem o anfitrião.</param>
/// <param name="Bloqueio">O código do erro que impede convocar agora, ou <c>null</c>.</param>
/// <param name="Mensagem">O texto do bloqueio, para a interface.</param>
internal sealed record PreviaConvocacaoResponse(
    IReadOnlyList<string> Tipos,
    int Convidados,
    int Recompensa,
    int DevolucaoPorPessoa,
    int DuracaoDias,
    string? Bloqueio,
    string? Mensagem);

internal sealed record ConvocarRequest(Guid? CartaId, string? Tipo);

/// <param name="Decisao"><c>Aceita</c>, <c>Recusada</c> ou <c>Desistiu</c>.</param>
internal sealed record ResponderConvocacaoRequest(string? Decisao);
