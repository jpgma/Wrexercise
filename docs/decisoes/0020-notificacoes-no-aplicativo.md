# 0020 — Notificações dentro do aplicativo

**Status:** DECIDIDA. Eventos enxugados por `0048-enxugamento-das-notificacoes.md`.

## Contexto

O novo mockup (decisão 0014) tem um sino na tela Início e, nas Configurações, preferências de notificação com lembretes semanais. Hoje quem recebe uma reação ou tem um treino para avaliar só descobre ao abrir a atividade. `arquitetura.md` §19 deixa notificações push fora do escopo, e o projeto evita filas e WebSockets.

## Alternativas consideradas

**Quando gerar**

- **Calcular na leitura:** sem tabela, mas sem estado de lida e com consultas caras sobre várias fatias.
- **Gravar na mesma transação do evento:** uma tabela simples, e o aviso nunca fica fora de sincronia com o evento.

**Como atualizar a tela**

- **WebSockets ou polling:** imediato, mas proibido ou caro em redes móveis.
- **Consultar ao voltar o foco para a janela:** suficiente para um grupo de amigos, sem conexões abertas.

## Decisão

- Tabela `notificacoes`, gravada pelo contrato `Features/Notificacoes/Contracts/NotificacoesAtividade`, dentro da transação de quem gera o evento.
- Eventos:
  - treino novo: os revisores (`NovaAtividade`);
  - primeira reação de cada pessoa: o autor (`Reacao`). Trocar de reação não avisa de novo;
  - comentário: o autor e quem já comentou, menos quem escreveu (`Comentario`; os administradores saíram pela decisão 0042);
  - a atividade ficou `Rejeitada` ou `EmRevisao`: o autor (`SituacaoAlterada`). `Verificada` deixou de avisar (decisão 0048);
  - entrou em `EmRevisao`: os administradores, menos o autor (`Disputa`).
- Enquanto não forem lidas, reações e comentários de uma atividade viram uma linha só, com um total.
- Toda leitura cruza com o vínculo ativo: quem saiu de um grupo não vê mais nada dele.
- `GET /api/notificacoes` traz as 50 mais recentes. `GET /api/notificacoes/nao-lidas` traz o total para o sino.
- O sino e a lista atualizam quando a janela volta ao foco, sem polling.
- O serviço em segundo plano já existente apaga as notificações 90 dias depois da última atualização.
- Sem preferências por tipo, push, e-mail ou lembretes agendados.

## Impacto

- Migração 013 cria `notificacoes`.
- Nova fatia `Features/Notificacoes`, chamada por `CriarAtividade`, `RegistrarReacao`, `RevisarAtividade`, `ComentariosRevisao` e `VerificacaoAtividade`.
- Novas telas: sino no Início e no Perfil, e a página `/notificacoes`.
- **Compromisso aceito:** com a aba aberta e em foco, um aviso novo só aparece ao navegar ou trocar de janela e voltar.
