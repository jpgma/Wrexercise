# 0047 — Fila de avaliação antes do formulário

**Status:** DECIDIDA. Substitui o item 4 e o retorno automático ao registro do item 5 de `0041-avaliar-antes-de-registrar.md`.

## Contexto

Pela `0041`, a tela de registro mostrava o formulário com um aviso da fila acima, e o botão de envio virava "Avaliar e enviar". O formulário ia para um rascunho em memória (`features/atividades/rascunho.ts`) e era enviado sozinho ao fim da fila. Na prática, a pessoa preenchia o formulário sem perceber que precisava avaliar antes, e o envio automático acontecia longe dos olhos dela. O rascunho, com imagens e um efeito de envio adiado, era a parte mais frágil da tela.

## Alternativas consideradas

- **Manter o formulário com o aviso**, só com mais destaque. Continua deixando a pessoa preencher primeiro e descobrir depois.
- **Ir direto para a primeira avaliação** ao tocar em "Registrar". Esconde o motivo; a pessoa cai numa tela que não pediu.
- **Manter o rascunho** só para o caso raro de chegar uma atividade com o formulário aberto. Guarda todo o código frágil por um caso de poucos segundos.

## Decisão

1. **Com a fila não vazia, a tela de registro não mostra o formulário.** Ela mostra só o aviso: os avatares de quem espera avaliação (entrando em sequência), "N treinos esperam sua avaliação", duas etapas ("Avaliar N" → "Seu treino", com cadeado) e o botão "Começar avaliação", que abre a primeira da fila com `?depois=registrar&total=N`. Vale para qualquer entrada, porque a regra está na rota.
2. **Sem rascunho nem envio automático.** `rascunho.ts` e "Avaliar e enviar" saem. Se a API responder `409 AVALIACOES_PENDENTES` com o formulário aberto, a fila é atualizada e o aviso toma o lugar do formulário; o que estava preenchido se perde.
3. **Progresso na fila:** vindo do registro, cada avaliação mostra "Avaliação X de N", uma barra por atividade e "Depois: seu treino". `total` vem da URL e nunca fica menor que o que ainda falta, caso a fila cresça.
4. **Fim da fila:** um cartão "Liberado!" (as mesmas animações de "Boa! Tá registrado."), uma vibração curta onde houver suporte e o botão "Registrar meu treino". Não há avanço automático para o formulário: a pessoa decide quando começar.
5. O avanço automático entre avaliações (`0041`, item 5) continua igual. Voltar, durante a fila, leva ao início.

## Impacto

- A tela de registro e a de avaliação ficam mais simples: sai o rascunho em memória e o efeito de envio adiado.
- Quem estava com o formulário aberto quando chegou uma atividade nova precisa preenchê-lo de novo. É raro e dura segundos.
- Nenhuma mudança no backend: a regra e o `409` da `0041` continuam valendo.
