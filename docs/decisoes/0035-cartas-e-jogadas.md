# 0035 — Detalhes das cartas e Jogadas

**Status:** DECIDIDA. Complementa `0033-regras-de-energia-e-jogadas.md`. Detalhes em `../gamificacao.md` §6.

## Contexto

Ao implementar a G3, alguns pontos de `gamificacao.md` §6 ficaram em aberto ou não cabiam no banco atual:

- A recompensa de 0,5 × o preço não é inteira para cartas de preço ímpar (+20 MIN, VARIEDADE, FIM DE SEMANA).
- As cartas +10 MIN e +20 MIN dependem da "duração mínima do desafio", que pode mudar enquanto a Jogada está aberta.
- "Até o fim do próximo domingo" e "até as 08:00 da segunda manhã depois do envio" não diziam o que acontece num envio no domingo ou de madrugada.
- `notificacoes.atividade_id` era obrigatório, e um aviso de carta não tem atividade.
- §6.3 dizia que o aviso de conclusão é gravado por `VerificacaoAtividade`, mas uma atividade fica `Verificada` em quatro lugares: registro manual sem revisores, evidência sem revisores, reações e decisão administrativa.
- A compra trava a linha do remetente, mas o limite de 2 Jogadas recebidas depende do destinatário. Dois colegas enviando ao mesmo tempo podiam passar do limite.

## Alternativas consideradas

- **Arredondamento:** para cima, para baixo ou guardar meios pontos. Para cima premia mais quem envia do que a regra pede; meios pontos exigem um tipo decimal só para isso.
- **Duração-alvo:** ler a duração do desafio ativo na hora da verificação, ou fixá-la no envio. Ler na hora muda o objetivo depois de aceito.
- **Notificações:** uma tabela própria para avisos do Jogo, ou abrir a tabela atual para Jogadas. Uma tabela nova duplicaria a lista, o sino e a limpeza de 90 dias.
- **Limite de recebidas:** aceitar a corrida rara ou travar também o destinatário.

## Decisão

1. A parte de quem enviou é **0,5 × o preço, arredondada para baixo**: 15, 22, 17, 17 e 20 EN para +10 MIN, +20 MIN, VARIEDADE, FIM DE SEMANA e ANTES DAS 8.
2. **Fixado no envio:** +10 MIN e +20 MIN guardam a duração-alvo (`duracao_alvo_segundos`), que é a duração mínima do desafio `Ativo` mais 10 ou 20 minutos. A VARIEDADE guarda o tipo a evitar (`tipo_evitar`): o tipo da última atividade `Verificada` do destinatário **no grupo da Jogada**. Sem atividade verificada, qualquer tipo vale.
3. **Prazos**, no horário local do grupo:
   - padrão: 48 horas depois do envio;
   - FIM DE SEMANA: até o fim do primeiro domingo depois do dia do envio. Enviada num domingo, vale até o domingo seguinte;
   - ANTES DAS 8: até as 08:00 do segundo dia depois do envio. Enviada às 06:00 de segunda, vale até as 08:00 de quarta.
4. **Notificações:** `notificacoes.atividade_id` passa a ser opcional, e a nova coluna `jogada_id` aponta para a Jogada. Cada aviso tem exatamente uma das duas. Os tipos novos são `CartaRecebida`, `CartaAceita`, `CartaRecusada` e `JogadaConcluida`. O aviso de conclusão é gravado pelo contrato `Features/Jogo/Contracts/JogadasConcluidas.cs`, chamado nos quatro lugares que deixam uma atividade `Verificada`, na mesma transação. Ele só avisa quando a atividade é a primeira verificada que cumpre o objetivo. Desde a `0048`, só o remetente recebe esse aviso.
5. **Travas:** a compra trava a linha do usuário. Jogar uma carta trava as linhas do remetente e do destinatário, sempre na mesma ordem, para evitar deadlock.
6. **Encenações vistas:** só a chegada da carta (`vista_em`, para o destinatário) e a resposta (`resultado_visto_em`, para o remetente) são registradas. Jogadas concluídas ou expiradas aparecem só no estado final, com o carimbo, sem encenação própria.
7. **Comprar não exige o modo Jogo.** Todos acumulam Energia; a Loja só aparece no modo Jogo, mas a API não precisa barrar a compra. Jogar uma carta exige que remetente e destinatário estejam no modo Jogo.

## Impacto

- Migração `016-create-cartas-e-jogadas.sql`: tabelas `cartas` e `jogadas` e as mudanças em `notificacoes`.
- `gamificacao.md` §6.3 e §6.4 foram atualizados.
- `CalculoEnergia` ganha a origem `jogadas`, e `GET /api/jogo/energia` devolve `detalhamento.jogadas`.
