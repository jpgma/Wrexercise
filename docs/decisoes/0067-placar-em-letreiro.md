# 0067 — Placar em letreiro

**Status:** DECIDIDA. Complementa `0066-placar-no-modo-jogo.md`: muda só como a faixa do Placar se move. O conteúdo dos itens, a ordem e as regras de quando o Placar aparece continuam os da 0066.

## Contexto

- O Placar (ADR 0066) é uma faixa parada, que rola com o dedo e abre centrada em quem vê.
- Em grupos maiores, quem não arrasta a faixa só vê os vizinhos de posição. O resto da disputa fica escondido.
- A praça é a tela "de fliperama" do aplicativo. Um placar que corre sozinho combina com ela e mostra a disputa inteira sem pedir nenhum gesto.

## Alternativas consideradas

- **Movimento:**
  - **Volta contínua:** escolhida. Corre devagar, da direita para a esquerda, e recomeça sem corte.
  - **Passo a passo:** para em alguns itens e desliza para os próximos. Parece mais um carrossel do que um letreiro.
  - **Um por vez, virando:** perde a comparação lado a lado, que é o motivo do Placar.
- **Toque:**
  - **Pausar, arrastar e tocar:** escolhida. Mantém o atalho para o Foco e o arrasto de hoje. Exige um laço pequeno em JavaScript sobre a rolagem nativa.
  - **Só pausar e tocar (CSS puro):** mais simples, mas em grupos cheios obriga a esperar o item passar.
  - **Só exibição:** tira um atalho que já existe.
- **Grupos pequenos:** correr sempre repetiria as mesmas pessoas lado a lado. Escolhido: correr só quando os itens não cabem.
- **Conteúdo:** misturar manchetes ("Ana subiu 2") pediria textos e dados novos do `resumo`. Fica fora desta decisão.

## Decisão

- **Letreiro:** quando os itens não cabem na faixa, ela corre sozinha a 30 px por segundo, em volta contínua. Depois do último colocado vem uma marca (◆) e o 1º de novo.
- **Início:** a faixa continua abrindo centrada em quem vê, e corre a partir daí.
- **Pausa:** tocar ou arrastar pausa o letreiro, que volta a correr 3 segundos depois do último movimento. O mouse sobre a faixa e o foco do teclado também pausam.
- **Arrasto:** a faixa rola com o dedo para os dois lados e dá a volta nas duas pontas.
- **Toque:** tocar num item ainda abre o Foco daquela pessoa.
- **Parada:** a faixa fica como na 0066, sem correr, quando todos os itens cabem ou quando o sistema pede movimento reduzido (`prefers-reduced-motion`).
- **Escondida:** o letreiro não corre enquanto o Placar está oculto (Foco e encenações).
- **Leitores de tela:** a volta usa uma cópia dos itens, fora da árvore de acessibilidade e da ordem do teclado. A lista é lida uma vez só.

## Impacto

- Só frontend: `PlacarJogo` (laço com `requestAnimationFrame` sobre o `scrollLeft`, `ResizeObserver` para saber se os itens cabem). Sem dependência nova, sem mudança na API e sem migração.
- `gamificacao.md` §3.5 e `funcionalidades.md` descrevem o letreiro.
