# 0036 — Cartas de Resposta

**Status:** DECIDIDA. Complementa `0033-regras-de-energia-e-jogadas.md` e `0035-cartas-e-jogadas.md`. Fecha a questão 1 de `../gamificacao.md` §12. Detalhes em `../gamificacao.md` §6.

## Contexto

A G4 libera as quatro Respostas: DESAFIO DUPLO, APOSTA, PRAZO EXTRA e MUDANÇA DE PLANOS. Ao implementar, ficaram em aberto:

- A tabela de trocas da MUDANÇA DE PLANOS (§12, item 1).
- O prazo depois da troca. Um +10 MIN enviado na segunda vale 48 horas; trocado por FIM DE SEMANA, esse prazo não inclui o sábado.
- Os parâmetros da Jogada devolvida pelo DESAFIO DUPLO: a duração-alvo, o tipo a evitar da VARIEDADE e o prazo.
- Se o DESAFIO DUPLO passa pelos limites de envio. A Jogada original continua `Aceita`, então o limite de "uma Jogada ativa por par" barraria sempre a devolução.
- Qual carta a Jogada devolvida aponta. `jogadas.carta_id` é obrigatório, e as recompensas usam o preço da Provocação original.

## Alternativas consideradas

- **Trocas:** qualquer outra Provocação; uma troca fixa por carta; ou a mesma faixa de preço, com escolha. Qualquer outra deixa trocar +20 MIN por FIM DE SEMANA. A troca fixa tira a escolha, que é o que a carta oferece.
- **Prazo da troca:** manter o original (o objetivo novo pode ficar impossível); recalcular pela carta nova (pode encurtar); ou o maior dos dois.
- **Parâmetros do DESAFIO DUPLO:** copiar tudo da original, ou recalcular para quem vai cumprir. Copiar o tipo a evitar usaria a última atividade de outra pessoa.
- **Carta da Jogada devolvida:** a própria DESAFIO DUPLO (o preço seria 40, não o da Provocação) ou a Provocação original.

## Decisão

1. **MUDANÇA DE PLANOS** troca dentro da mesma faixa de preço, e quem aceita escolhe: `+10 MIN ↔ VARIEDADE ↔ FIM DE SEMANA` e `+20 MIN ↔ ANTES DAS 8`. O objetivo novo fica em `jogadas.codigo_objetivo`. A duração-alvo e o tipo a evitar do objetivo novo são fixados no aceite, para quem vai cumprir. Trocar para +10 MIN exige um desafio `Ativo`. O prazo passa a ser **o maior** entre o atual e o prazo da carta nova calculado a partir do envio: nunca encurta e nunca fica impossível. As recompensas continuam com o preço pago pela Provocação.
2. **DESAFIO DUPLO** cria uma Jogada nova, de quem aceitou para quem enviou, com `origem_id` apontando para a original e `carta_id` igual à **Provocação original**. Assim as recompensas usam o preço original. Essa Provocação continua sendo de quem a comprou: a Jogada devolvida não a gasta nem a deixa "em jogo". A duração-alvo de +10 MIN e +20 MIN é copiada; o tipo a evitar da VARIEDADE é o da última atividade verificada de quem vai cumprir; o prazo começa no aceite. A devolução **não confere os limites de envio**, porque é uma resposta, mas exige que quem enviou esteja no modo Jogo e ainda no grupo. Uma Jogada devolvida não aceita outro DESAFIO DUPLO, e um índice único em `origem_id` garante uma devolução por Jogada.
3. **PRAZO EXTRA** soma 24 horas a `expira_em`, no horário local do grupo. Não há coluna nova.
4. **APOSTA** só registra a carta. Na leitura, a recompensa de quem recebeu passa a 2 × o preço se a Jogada ficar `Concluida`.
5. Toda Resposta é gasta ao ser usada (`carta_resposta_id`), aconteça o que acontecer depois. Recusar continua grátis e não aceita carta.
6. `GET /api/grupos/{id}/jogadas` devolve, na Jogada recebida que espera resposta, os objetivos que a MUDANÇA DE PLANOS pode escolher. Assim a tabela fica só no servidor.

## Impacto

- Migração `017-add-objetivo-jogada.sql`: coluna `jogadas.codigo_objetivo` e índice único em `jogadas.origem_id`.
- A Loja vende as nove cartas. `PUT /api/jogadas/{id}/resposta` aceita `cartaRespostaId` e `objetivo`.
- `EstadoJogo` passa a contar as Respostas usadas como gastas, e `CalculoEnergia` dobra a parte de quem recebeu com APOSTA.
- `gamificacao.md` §6 e §12 e `funcionalidades.md` foram atualizados.
