# 0074 — A carta jogada não volta

**Status:** DECIDIDA. Substitui, em `0033-regras-de-energia-e-jogadas.md`, "a carta volta para a Mão" e a recompensa de 0,5 × de quem enviou (detalhada em `0035-cartas-e-jogadas.md`), e muda o destino das cartas da Fusão (`0070`). A FADIGA (`0059`), as Respostas (`0036`) e os limites de envio (`0069`) continuam os mesmos. Detalhes em `../gamificacao.md` §5.1, §6.2, §6.4 e §6.5.

## Contexto

- Uma Provocação recusada, expirada ou não cumprida voltava para a Mão de quem enviou (`0033`). Jogar uma carta não tinha risco.
- Quem recebia pagava sozinho: FADIGA ao recusar, ignorar ou falhar (`0059`). Dava para mirar alguém com cartas que a pessoa não consegue cumprir, sem custo nenhum para quem joga.
- A `0033` descartou a "carta perdida" porque cada recusa custaria de 30 a 45 EN a quem enviou e desestimularia o envio. Esse receio continua válido e pede uma compensação.
- A situação da carta e a Energia são calculadas na leitura (`EstadoJogo`, `CalculoEnergia`): não há coluna de uso nem livro-razão.

## Alternativas consideradas

- **Não mexer:** mantém o custo só de um lado.
- **Recusa sem FADIGA:** quem não consegue cumprir diria "não" sem custo. Descartada pelo mantenedor: a FADIGA fica como está, e os dois lados passam a ter algo a perder.
- **Reembolso parcial:** a carta perdida devolveria parte do preço. Mais uma regra para explicar, e enfraquece o custo que se quer criar.
- **Perder a carta só em parte dos casos** (só na recusa, ou só no aceite sem cumprir): duas regras no lugar de uma, e a carta teria de continuar "em jogo" na Mão.
- **Manter a recompensa de 0,5 × para quem enviou:** com metade das Jogadas concluídas, a perda média por carta jogada iria de cerca de 0,25 × para 0,75 × o preço. O jogo, que já tinha cartas paradas (`0069`), tenderia a parar.
- **Recompensa de 0,75 ×:** meio-termo sem vantagem clara sobre 1 ×, que é mais fácil de explicar ("cumpriu, a carta saiu de graça").

## Decisão

- **A Provocação é gasta ao ser jogada**, seja qual for o resultado: `Concluida`, `Recusada` ou `Expirada` (sem resposta, ou aceita e não cumprida). Ela sai da Mão na hora e libera a vaga. Não existe mais carta "em jogo" na Mão: a Jogada aberta aparece só na Mesa.
- **Quem enviou recebe 1 × o preço** quando a Jogada fica `Concluida` (antes, 0,5 ×). Cumprida, a carta sai de graça; não cumprida, custa o preço inteiro.
- **Fusão:** as duas cartas são gastas no arremesso. Concluída, quem enviou recebe 1 × a soma dos preços; o bônus de 20% continua só de quem cumpre.
- **DESAFIO DUPLO:** a Jogada devolvida segue a mesma regra de recompensa: quem usou a Resposta recebe 1 × o preço da Provocação se o outro cumprir. A Provocação original já foi gasta pela Jogada original.
- **FADIGA:** não muda.
- **Limites de envio:** não mudam.
- **Atividade rejeitada ou anulada depois da conclusão:** a Jogada deixa de estar `Concluida`, e a recompensa de quem enviou some, como já acontecia. A carta não volta. Sem caso especial.
- **Aviso da expiração:** não há notificação nova nem serviço periódico. A Mesa mostra a Jogada encerrada com "Carta perdida: ela não volta para a Mão.". A recusa continua com a notificação `CartaRecusada`.
- **Transição:**
  - As Jogadas abertas na implantação seguem a regra nova.
  - A Provocação de uma Jogada recusada ou expirada antes de `EstadoJogo.InicioCartaPermanente` (19/11/2026, 10:00, UTC−3) já tinha voltado para a Mão e continua lá.
  - A recompensa de 1 × vale para todas as Jogadas concluídas, inclusive as antigas: quem já teve uma Jogada concluída recebe a outra metade na implantação.

## Impacto

- **Economia:** quem joga empata quando o colega cumpre e perde de 30 a 45 EN quando não cumpre (de 65 a 85 EN numa Fusão). Com metade das Jogadas concluídas, a perda média por carta é de 0,5 × o preço (antes, 0,25 ×). Cada Jogada concluída cria 2 × o preço em Energia (antes, 1,5 ×). A recompensa retroativa sobe os saldos uma vez, na implantação.
- **Mão:** a vaga abre no arremesso. Quem tem Energia pode comprar outra carta com 2 Jogadas abertas; o limite de 2 enviadas ativas continua segurando o ritmo.
- **Backend:** `EstadoJogo.IsGasta` e `CalculoEnergia.GetRecompensa`. Saem `EstadoJogo.IsEmJogo`, o erro `CARTA_EM_JOGO` e o campo `emJogo` de `GET /api/jogo/loja`; a carta já jogada responde `CARTA_NAO_ENCONTRADA`. Sem migração.
- **Frontend:** a Mão não mostra mais a vaga reservada; a prévia do arremesso termina com "Se não, você perde a carta."; o passeio "Como jogar" explica a regra; a confirmação da recusa diz "Quem enviou perde a carta."; a Loja explica a regra.
- `gamificacao.md` §5.1, §5.3, §6.1, §6.2, §6.4 e §6.5, `funcionalidades.md` §3.3.2 a §3.3.4 e o comentário de `deploy/metricas.sql` descrevem a regra nova.
- Reavaliar com `metricas.bat` depois de duas semanas: Jogadas enviadas, recusadas ou expiradas, e Energia gasta por semana. Se os envios caírem, rever primeiro a FADIGA na recusa explícita.
