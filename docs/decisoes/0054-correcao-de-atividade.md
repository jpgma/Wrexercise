# 0054 — Correção de atividade: o autor apaga, o administrador anula

**Status:** DECIDIDA. Complementa `0011-atividades.md`, `0015-reacoes-em-revisao.md` e `0053-chave-de-idempotencia.md`.

## Contexto

Ninguém podia editar nem apagar uma atividade. Um tipo errado, um erro de digitação na duração ou um registro em dobro ficavam para sempre, e a única saída era o grupo rejeitar. Rejeitar acusa a pessoa de trapaça por um engano, e o grupo precisa juntar rejeições suficientes.

A Energia do Jogo só cresce porque `Verificada` é final (`CalculoEnergia`), e Rivais e Jogadas também dependem disso. Uma atividade verificada pode já ter virado Energia gasta na Loja. Por isso, a correção vale só antes da decisão.

## Alternativas consideradas

- **Editar a atividade:** exige formulário de edição, recalcular os avisos de 0052 e decidir o que fazer com as reações já dadas. Apagar e registrar de novo resolve o mesmo caso com menos código.
- **Apagar sem prazo:** quem recebe Suspeito apagaria e registraria de novo com outra foto, fugindo da revisão.
- **Anular uma atividade verificada:** desfaria Energia, Rivais e Jogadas já concluídos.
- **Administrador anula com `Rejeitada`:** reaproveita tudo, mas o autor vê "Seu treino foi rejeitado" por um engano.
- **Autor apaga cedo; administrador anula com uma situação própria:** escolhida.

## Decisão

### O autor apaga

- `DELETE /api/atividades/{id}`. Só o autor, membro ativo do grupo, nos primeiros 15 minutos depois do registro (`submitted_at`), e só enquanto a atividade está em `AguardandoFeedback`. Um `Kudos` não impede; um `Suspeito` ou `Rejeitar` leva a `EmRevisao` e fecha a exclusão.
- Fora disso: `409 EXCLUSAO_NAO_PERMITIDA`. Outra pessoa do grupo: `403 ACESSO_NEGADO`.
- A exclusão é definitiva. Revisores, reações, comentários, notificações e histórico saem por `ON DELETE CASCADE`. Os avisos e as evidências da atividade saem na mesma transação, e os arquivos das fotos e miniaturas são apagados depois do commit.
- O aviso `FotoParecida` de outra atividade que apontava para esta continua, sem o link (`atividade_relacionada_id = NULL`).
- A foto apagada volta a valer: o autor pode registrar de novo com a mesma imagem sem `EVIDENCIA_DUPLICADA`.
- `GET /api/atividades/{id}` devolve `exclusaoPermitidaAte`, preenchido só para o autor enquanto ele pode apagar. A tela mostra "Registrou errado? Dá para apagar até HH:MM" e pede confirmação.
- A notificação push "registrou um treino" já enviada não é recolhida; a notificação no aplicativo some com a atividade.

### O administrador anula

- Nova situação final `Anulada`. O proprietário ou um administrador usa a revisão administrativa já existente (`POST /api/atividades/{id}/revisao`) com `decisao: "Anulada"` e justificativa obrigatória.
- Vale para `AguardandoFeedback` e `EmRevisao`. Aprovar ou rejeitar continuam só em `EmRevisao`. `Verificada` e `Rejeitada` não mudam.
- Ninguém anula a própria atividade (como em 0052): o autor apaga dentro do prazo ou pede a outro administrador.
- `Anulada` conta como `Rejeitada` em todo cálculo: não soma na meta, na Energia, nas Jogadas nem nos gráficos do perfil. Não recebe reações nem evidências e sai da fila de avaliação.
- O autor recebe a notificação `SituacaoAlterada` "Seu treino foi anulado". A decisão fica em `historico_revisao`.
- A atividade continua no feed com a etiqueta "Anulada", para o grupo entender por que ela sumiu da meta.

## Impacto

- Migração `029-add-situacao-anulada.sql`: `Anulada` nos CHECK de `atividades.situacao_verificacao` e `historico_revisao.decisao`.
- Os filtros `situacao_verificacao <> 'Rejeitada'` passam a `NOT IN ('Rejeitada', 'Anulada')`.
- Comentários continuam sem edição nem exclusão. Contas e grupos continuam sem exclusão (Q029).
