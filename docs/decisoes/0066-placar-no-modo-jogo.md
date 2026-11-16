# 0066 — Placar no modo Jogo

**Status:** DECIDIDA. Complementa `0013-competicao-e-progresso.md` (o `resumo` passa a trazer todos os participantes) e `0023-gamificacao.md`. Não muda a ordem da Classificacao nem os princípios 1 e 7 de `gamificacao.md` §1.1.

## Contexto

- O início do modo Jogo mostra a praça e a Mão. Da Classificacao, só aparecem as medalhas 🥇🥈🥉 dos 3 primeiros. Os números de cada um ficam no Foco, uma pessoa por vez, ou na página do desafio.
- Os participantes querem ver onde estão na disputa sem sair da praça e sem rolar a tela.
- A praça e a Mão inteira precisam caber em 390×844 (`gamificacao.md` §3.5, "Altura").

## Alternativas consideradas

- **Faixa entre o cabeçalho e a praça:** encolhe a praça para caber.
- **Barras de progresso sobre cada avatar:** divertido, mas difícil de comparar e poluído em grupos cheios.
- **Cabeçalho vira placar:** perde a saudação e o progresso da semana.
- **Placar sobre o alto da praça, como num fliperama:** escolhido. O alto da praça é céu e prédios, sem avatares (a cabeça do avatar mais alto fica por volta de y = 140 de 400), então a faixa não muda a altura de nada.

## Decisão

- **Placar:** uma faixa no alto da praça, fora da câmera, com todos os participantes na ordem da Classificacao. Cada item mostra a posição, o retrato, o primeiro nome ("Você" para quem vê) e as semanas cumpridas. Os itens rolam na horizontal dentro da faixa, que abre já rolada até quem vê.
- **Toque:** tocar num item abre o Foco daquela pessoa, onde já ficam as sessões, a sequência e os totais.
- **Setas:** ▲N / ▼N comparam com a posição no início da semana, calculada sem a semana atual. Não aparecem na primeira semana nem com o desafio encerrado.
- **Quem dorme:** o item fica apagado, com 💤.
- **Desafio encerrado:** a ordem final, com o selo "FINAL".
- **Quando aparece:** com as mesmas regras das medalhas: desafio `Ativo` ou `Encerrado` e alguém com atividade verificada. O Placar some no Foco e nas encenações.
- **Disputa no cabeçalho:** com o desafio `Ativo`, o cabeçalho ganha um 3º quadro com a disputa de quem vê:
  - `Lider`: "Na liderança!", com a vantagem em treinos sobre o 2º quando empatados em semanas.
  - `Treinos`: mesmas semanas que quem está acima: "Faltam N treinos para passar Ana."
  - `Meta`: uma semana atrás, sem a meta desta semana: "Feche a semana para encostar em Ana."
  - `Semanas`: "Ana tem N semanas cumpridas a mais."
- **Sem Energia e sem pontos:** o Placar usa só a Classificacao (princípio 7, `arquitetura.md` §8.5).

## Impacto

- `GET /api/grupos/{id}/resumo`:
  - `classificacao` traz todos os participantes, não só os 3 primeiros. Cada item ganha `sequenciaSemanas` e `posicaoInicioSemana`.
  - Ganha `proximo` (`tipo`, `nomeExibicao`, `quantidade`), só com o desafio `Ativo`.
- Regras puras em `ClassificacaoDesafio` (`CalcularRankingInicioSemana`, `GetProximo`). Sem migração.
- Frontend: `PlacarJogo` na praça; `SaudacaoAlternada` com o 3º quadro; textos em `getDisputa`.
