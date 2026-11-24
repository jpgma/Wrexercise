# 0087 — Meia devolução da carta não cumprida

**Status:** DECIDIDA. Muda, em `0074-carta-jogada-nao-volta.md`, o custo da carta não cumprida: quem enviou recebe metade do preço de volta. Reabre a alternativa "reembolso parcial" que a `0074` descartou, pelo motivo abaixo. A carta jogada continua não voltando para a Mão, e a recompensa de 1 × na carta cumprida, a FADIGA (`0059`) e os limites de envio (`0069`) continuam os mesmos. Detalhes em `../gamificacao.md` §5.1, §6.2 e §6.4. Desde `0089-fadiga-visivel-e-recusa-com-peso.md`, a carta **recusada** devolve o preço inteiro (a Revanche); a falha e a expiração continuam com metade.

## Contexto

- Pela `0074`, a Provocação recusada, não cumprida ou expirada custa o preço inteiro a quem enviou: de 30 a 45 EN, ou até 85 EN numa Fusão.
- O resultado depende só de quem recebe: recusar, deixar vencer ou falhar. Quem enviou não tem como agir, e perde tudo do mesmo jeito.
- A `0074` descartou o reembolso parcial para não enfraquecer o custo. Com a janela da carta (`0086`), menos cartas falham por acaso, e o custo que sobra vem das escolhas de quem recebe. O mantenedor pediu que quem enviou receba metade do valor quando a carta é recusada ou não cumprida.
- A Energia é calculada na leitura (`EstadoJogo`, `CalculoEnergia`): a devolução não precisa de coluna nem de lançamento.

## Alternativas consideradas

- **Só na recusa e na falha, sem a expiração:** a carta que vence sem resposta costuma ser uma carta que ninguém viu, e o custo para quem recebe é o mesmo da falha. Descartada: vale para as três.
- **Devolver a carta para a Mão:** volta ao jogo sem risco que a `0074` corrigiu. Descartada.
- **Devolução maior na APOSTA, quando quem recebe aposta e perde:** quem enviou lucraria com a aposta perdida do colega, o que vai contra "ninguém prejudica ninguém" (`../gamificacao.md` §1). Descartada: a APOSTA não muda a devolução.
- **Devolver também na Jogada do DESAFIO DUPLO:** quem a devolveu pagou uma Resposta, não a Provocação. Descartada: só quem comprou a Provocação recebe a metade.
- **Notificar só na recusa:** a falha e a expiração passariam em silêncio. Descartada: um serviço periódico avisa as duas.

## Decisão

- **Devolução:** quando a Jogada fica `Recusada`, `Falhou` ou `Expirada`, quem enviou recebe metade do preço em Energia, arredondada para baixo (`CalculoEnergia.GetDevolucao`). Na Fusão, metade da soma das duas Provocações.
  - +10 MIN 30 → 15; +20 MIN 45 → 22; VARIEDADE 35 → 17; FIM DE SEMANA 35 → 17; ANTES DAS 8 40 → 20.
  - `Concluida`: continua 1 × o preço.
  - APOSTA: metade, como nas outras.
  - DESAFIO DUPLO: a Jogada devolvida não devolve nada.
- **Energia:** nova origem `Devolucoes` ("Cartas não cumpridas (metade de volta)"). `GET /api/grupos/{id}/jogadas` ganha `devolucao`, só para quem enviou.
- **Interface:** a Mesa mostra "+N EN de volta"; a recusa diz "Recusando, quem enviou recebe metade de volta: +N EN."; a prévia do arremesso termina com "Se não, você recebe +N EN de volta."; a Loja, a Mão e "Como jogar" explicam a regra.
- **Avisos (só na lista, sem push):**
  - `CartaRecusada` mostra "+N EN de volta" (a coluna nova `energia` guarda o valor). O texto antigo, "Ela voltou para a sua Mão", estava errado desde a `0074`.
  - `CartaNaoCumprida`, novo: "{nome} não cumpriu a sua carta" ou "A sua carta para {nome} venceu", com "+N EN de volta". O `CartasNaoCumpridasBackgroundService` procura, a cada 5 minutos, as Jogadas que falharam ou expiraram nos últimos 7 dias e grava um aviso por Jogada (índice único).
- **Transição:** a devolução vale para todas as Jogadas desde `EstadoJogo.InicioCartaPermanente` (19/11/2026, 10:00, UTC−3). Antes disso, a carta voltava para a Mão e não há devolução. Os avisos `CartaNaoCumprida` só saem para os resultados a partir de `CartasNaoCumpridas.InicioAviso` (22/11/2026, 00:00, UTC−3).

## Impacto

- **Economia:** a carta não cumprida custa metade do preço a quem enviou (de 15 a 23 EN; até 43 EN numa Fusão). Com metade das Jogadas concluídas, a perda média por carta cai de 0,5 × para 0,25 × o preço. A devolução retroativa sobe os saldos uma vez, na implantação.
- **Backend:** `JogadaCarregada.Devolucao` e `VoltouPelaRegraAntiga` (saiu de `EstadoJogo`), `EnergiaGanha.Devolucoes`, `ResponderJogada` (energia no aviso da recusa), `CartasNaoCumpridas` e o serviço periódico, `NotificacoesJogada.NotificarNaoCumpridaAsync`. Migração `046-add-devolucao-carta.sql`: coluna `notificacoes.energia`, tipo `CartaNaoCumprida` e o índice único.
- **Frontend:** `getDevolucao`, o selo da Mesa, a linha do saldo, os textos dos avisos.
- Reavaliar com `metricas.bat` depois de duas semanas: Jogadas enviadas por semana e Energia devolvida.
