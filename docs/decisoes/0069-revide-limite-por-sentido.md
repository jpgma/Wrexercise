# 0069 — Revide: o limite por par vale por sentido

**Status:** DECIDIDA. Muda um limite de `gamificacao.md` §6.3 e reabre, em parte, o item "Cadeias de contra-ataque" de §11. As regras das cartas (`0033`, `0035`, `0036`) e a FADIGA (`0059`) continuam as mesmas.

## Contexto

- O limite "1 Jogada ativa por par de pessoas" vale nos dois sentidos. Quem recebe uma Provocação não pode jogar uma carta em quem a enviou enquanto a Jogada estiver aberta, o que pode levar dias.
- A única volta possível é o DESAFIO DUPLO: custa 40 EN e devolve o mesmo objetivo, não uma carta da Mão.
- Produção em 16/11/2026, segunda semana do Jogo: 4 pessoas, 1 grupo, 5 cartas compradas e 1 Jogada. Quatro Provocações estão paradas nas Mãos, e quem foi provocado tem três cartas que não pode usar em quem o provocou. O problema de hoje é o jogo parado, não o excesso de cartas.
- "Cadeias de contra-ataque" foram descartadas por complexidade (§11). O receio era uma sequência sem fim de respostas.

## Alternativas consideradas

- **Limite por sentido:** escolhida. Uma Jogada ativa de A para B e uma de B para A. Sem carta nova, sem tabela nova e sem regra nova para explicar.
- **Revide com bônus:** marcar a segunda Jogada como "revide" e pagar um extra se os dois cumprirem. Mais regra e mais interface antes de saber se o revide é usado.
- **Nova Resposta "Revide":** aceita a Provocação e anexa uma Provocação de volta. Exige comprar mais uma carta e se sobrepõe ao DESAFIO DUPLO.
- **Não mexer:** mantém as cartas paradas.

## Decisão

- **Limite:** no máximo 1 Jogada ativa de uma pessoa para outra. A Jogada no sentido contrário não conta. O código do erro continua `JOGADA_ATIVA_COM_PESSOA`.
- **Os outros limites não mudam:** 2 recebidas ativas, 2 enviadas ativas e 1 Jogada nova para a mesma pessoa a cada 24 horas.
- **A cadeia tem fim:** cada sentido tem uma vaga. Um revide não abre vaga para outro revide. O par fica com, no máximo, 2 Jogadas vindas de envios.
- **DESAFIO DUPLO:** não muda. Continua sem conferir os limites (`0036`) e sem aceitar outro DESAFIO DUPLO. A Jogada devolvida ocupa a vaga do sentido dela para os envios seguintes.
- **FADIGA:** não muda. Quem revida pode dar FADIGA a quem o provocou, se a carta for recusada ou expirar. A proteção continua sendo o não acúmulo e os limites de envio (`0059`).

## Impacto

- Backend: `LimitesJogada.Verificar` compara remetente e destinatário no mesmo sentido. Sem migração e sem mudança na API.
- Frontend: `getBloqueioJogada` deixa de bloquear a Mão quando a carta aberta veio do alvo. A mensagem passa a ser "Você já tem uma carta aberta com …".
- `gamificacao.md` §6.3 e §11 e `funcionalidades.md` §3.3.2 e §3.3.4 descrevem o limite novo.
- Reavaliar com `metricas.bat` depois de duas semanas: Jogadas enviadas e Jogadas para inativos.
