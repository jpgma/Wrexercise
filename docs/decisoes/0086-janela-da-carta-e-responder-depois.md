# 0086 — A janela da carta e "Responder depois do treino"

**Status:** DECIDIDA. Muda, em `0075-proximo-treino-e-antes-das-8.md`, qual treino é o próximo treino: só o que está na janela da carta. Muda, em `0076-chegada-da-carta-e-treino-antes-do-aceite.md`, o bloqueio do treino ao vivo com carta sem resposta e acrescenta à Chegada o botão "Responder depois do treino". A FADIGA (`0059`), a Fusão (`0070`) e a soma do dia (`0040`) continuam valendo. Detalhes em `../gamificacao.md` §3.3 e §6.4.

## Contexto

- Pela `0075`, o primeiro treino que começa depois do aceite decide a carta. Um treino que nunca poderia cumpri-la também decidia: o treino da noite fazia falhar a ANTES DAS 8, e o treino de terça fazia falhar a FIM DE SEMANA.
- Quem treinava era punido com FADIGA e perdia a carta. O jogo existe para recompensar a constância (`../gamificacao.md` §1), e a regra fazia o contrário: para não perder, a pessoa deixava de registrar o treino, ou de treinar.
- Pela `0076`, a Chegada não tinha "depois", e o servidor recusava o treino ao vivo com carta sem resposta. Quem abria o aplicativo a caminho da academia precisava aceitar antes de treinar, e o treino seguinte já era o próximo treino.
- O mantenedor pediu uma regra justa sem deixar a carta lenta nem tirar a competição.

## Alternativas consideradas

- **Voltar à regra antiga (qualquer treino no prazo, sem `Falhou`):** a `0075` a descartou porque a carta não mudava o treino. Descartada.
- **Carência de algumas horas depois do aceite:** arbitrária; um treino da noite, 4 horas depois, ainda faria falhar a ANTES DAS 8. Descartada.
- **Falhar sem FADIGA:** a punição por treinar ficava menor, mas continuava. Descartada.
- **Janela também para VARIEDADE, +10 MIN e +20 MIN:** essas cartas são escolhas que qualquer treino pode cumprir. Sem falha, viram cartas de esperar um dia de sorte. Descartada: continuam falhando.
- **Prazo de resposta separado, ou "depois" sem limite:** pediria colunas e regras novas. Descartada: o botão só suspende a Chegada no aparelho, e o prazo da carta não muda.

## Decisão

- **Janela da carta:** só um treino na janela da carta pode ser o próximo treino. Fora dela, o treino não conta e não faz falhar (`ObjetivoCarta.IsNaJanela`).
  - ANTES DAS 8: treinos que começam antes das 08:00, no fuso do grupo. O da janela ainda precisa terminar até as 08:00 do dia em que começou.
  - FIM DE SEMANA: treinos no sábado ou no domingo. Assim, ela só é cumprida ou expira, nunca falha.
  - +10 MIN, +20 MIN e VARIEDADE: qualquer treino, como antes.
  - Fusão: o treino precisa estar na janela das duas cartas.
- **"Responder depois do treino":** a Chegada ganha esse botão antes da resposta. Ele suspende a Chegada das cartas pendentes neste aparelho (cookie comum `wx_chegada_adiada`, até 6 horas). Uma carta nova traz a Chegada de volta, e ela também volta quando o treino ao vivo acaba ou quando a pessoa vai responder.
  - O servidor deixa começar o treino ao vivo com carta sem resposta. O registro comum continua recusado (`CARTA_SEM_RESPOSTA`), com o botão "Responder a carta".
  - O treino ao vivo começou antes do aceite: pela `0076`, conta se cumpre e nunca faz falhar.
  - O prazo da carta não muda.
- **Transição:** vale para todas as Jogadas da regra do próximo treino (`ObjetivoCarta.InicioProximoTreino`). Uma Jogada `Falhou` pode voltar a `Aceita`, passar a `Expirada` ou ser cumprida, e o início da FADIGA pode mudar.

## Impacto

- **Justiça:** treinar nunca faz falhar uma carta que esse treino não poderia cumprir. A carta continua pedindo um plano: a próxima manhã cedo, o próximo fim de semana, outro tipo, mais minutos.
- **Ritmo:** o prazo e a resposta obrigatória continuam iguais. A carta não fica mais lenta.
- **Backend:** `ObjetivoCarta.IsNaJanela` e o filtro em `GetProximoTreino`; `ComecarTreino` sem a trava da carta. Sem migração.
- **Frontend:** `getAvisoCarta` (aviso "não conta pra carta… A carta continua valendo"), `getVez` (o texto da vez na Mesa, na Chegada e na prévia), `chegadaAdiada.ts`, o botão no `PainelChegada` e a pausa no `PortaoChegada`.
- Reavaliar com `metricas.bat` depois de duas semanas: Jogadas `Falhou` e `Expirada` por carta.
