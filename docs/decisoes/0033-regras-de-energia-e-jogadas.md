# 0033 — Regras de Energia e Jogadas antes da implementação

**Status:** DECIDIDA. Complementa e substitui partes de `0024-cartas-e-avatar-importado.md` (recompensas das Jogadas e notificações). Detalhes em `../gamificacao.md` §5 e §6. A `0059-fadiga.md` dá consequência à recusa e à expiração para quem recebeu.

## Contexto

Antes das fases G2 e G3, uma revisão de `gamificacao.md` encontrou lacunas que mudam o código de `CalculoEnergia` e o esquema de `cartas` e `jogadas`:

- Cada atividade pertence a um grupo (`atividades.grupo_id`), mas a Energia é única por pessoa. Quem está em dois grupos e registra o mesmo treino nos dois ganharia os bônus e a meta semanal duas vezes.
- Não estava claro se os marcos de sequência pagavam uma vez por pessoa ou uma vez por desafio.
- Com 1,5 × o preço para quem recebe, uma única Jogada concluída (cerca de 50 EN) valia metade de uma semana típica e cinco vezes um treino. E a APOSTA (+50% sobre a parte de quem recebe) rendia de 15 a 22 EN por um custo de 25, ou seja, perdia mesmo quando ganhava.
- Uma Provocação recusada ou expirada custava o preço inteiro a quem enviou, o que desestimula enviar cartas (critério de sucesso 2).
- "Jogada ativa", usada nos limites, não tinha definição.
- O alvo da VARIEDADE ("a última atividade verificada") podia mudar durante a Jogada.
- Só a carta recebida gerava `Notificacao`.

## Alternativas consideradas

**Vários grupos**

- **Aceitar a soma:** simples, mas premia estar em mais grupos.
- **Detectar treinos duplicados:** frágil e sem critério confiável.
- **Base por atividade e bônus por pessoa:** a base continua por atividade, em cada grupo. Os bônus e a meta semanal contam uma vez por pessoa.

**Recompensas**

- **1,5× e 0,5× (0024):** recompensa da Jogada maior que a do treino, e APOSTA sempre negativa.
- **1× e 0,5×, com APOSTA dobrando a parte de quem recebe:** a Jogada vale cerca de três treinos, e a APOSTA vira uma aposta de verdade (ganha de 5 a 20 EN líquidos ou perde 25).

**Carta não concluída**

- **Carta perdida:** cada recusa custa de 30 a 45 EN a quem enviou.
- **Reembolso parcial:** exige livro-razão ou um campo de crédito.
- **Carta volta para a Mão:** derivado das Jogadas, sem escrita extra.

## Decisão

- **Bônus por pessoa.** A base de 10 EN vale para cada atividade verificada, em qualquer grupo. Os qualificadores de "primeira" (da semana, num dia novo, tipo novo) consideram todas as atividades da pessoa, de todos os grupos. Os bônus de horário (antes das 06:00, entre 06:00 e 08:00, fim de semana) contam no máximo uma vez por pessoa por dia. Os 20 EN da meta semanal contam uma vez por pessoa por semana, no primeiro grupo em que a meta for cumprida.
- **Sequências e desafio concluído:** cada marco de sequência (4, 8, 12) paga uma vez por pessoa, como as conquistas. O desafio concluído (75 EN) é um desafio `Encerrado` com todas as semanas que contavam para a pessoa cumpridas, e paga uma vez por desafio.
- **Recompensas da Jogada concluída:** 1 × o preço para quem recebeu e 0,5 × para quem enviou. A APOSTA dobra a parte de quem recebeu (2 × o preço). O teste de simulação semanal de `CalculoEnergia` confere esses valores.
- **Carta volta para a Mão** quando a Jogada fica `Recusada` ou `Expirada`. Só uma Jogada `Concluida` gasta a Provocação. As Respostas são gastas quando são usadas, aconteça o que acontecer depois. A situação da carta é calculada a partir das Jogadas, sem coluna de data de uso.
- **Limite da Mão:** conta as cartas não gastas, inclusive as que estão em Jogadas abertas, para que uma carta devolvida nunca passe de 5.
- **Jogada ativa**, para os limites: `Enviada` ou `Aceita`. Uma Jogada `AguardandoVerificacao` libera a vaga, mesmo que a atividade esteja em `EmRevisao`. Se a atividade for rejeitada e a Jogada voltar a `Aceita`, os limites podem ficar acima do máximo por um tempo. Eles só são conferidos ao enviar.
- **Alvo da VARIEDADE:** o tipo da última atividade da pessoa que recebeu já `Verificada` no momento do envio. Ele é gravado na Jogada.
- **Notificações para quem enviou:** a carta aceita, recusada ou concluída gera uma `Notificacao` para quem enviou. A conclusão também avisa quem recebeu. A expiração não gera aviso, porque é calculada na leitura e não tem evento.

## Impacto

- `gamificacao.md` §5, §6 e §12 foram atualizados.
- `CalculoEnergia` recebe as atividades de todos os grupos da pessoa, não só as de um grupo.
- `jogadas` ganha uma coluna com o tipo-alvo da VARIEDADE. `cartas` não tem coluna de data de uso.
- `VerificacaoAtividade` passa a chamar um contrato de `Features/Jogo/Contracts/` na mesma transação, para gravar a notificação de Jogada concluída (decisão 0020).
- Novos tipos de `Notificacao`: carta recebida, aceita, recusada e concluída.
