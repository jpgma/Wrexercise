# 0079 — Desafio em destaque na página do grupo, sem Placar

**Status:** DECIDIDA. Substitui `0066-placar-no-modo-jogo.md` e `0067-placar-em-letreiro.md`. Não muda a ordem da Classificacao (`0013-competicao-e-progresso.md`).

## Contexto

- A página do grupo mostrava só a semana: uma barra com os treinos do grupo na semana e "Como está a turma", com a semana de cada um. As regras, as datas e a Classificacao do desafio ficavam em outra página, "Meta do grupo".
- Na página do desafio, a semana vinha antes da Classificacao, e as regras ficavam em quadradinhos soltos.
- O Placar da praça mostrava só as semanas cumpridas (★) e as setas desde segunda. A ordem também usa os dias verificados e a sequência: com o mesmo número de ★, ninguém via por que um estava na frente. O letreiro em movimento piorava a leitura.

## Alternativas consideradas

- **Onde fica o desafio:**
  - **Página do grupo como centro:** escolhida. O desafio inteiro (andamento, regras, Classificacao) fica na página do grupo. A página do desafio fica para detalhes e histórico.
  - **Juntar as duas páginas:** a página do grupo ficaria longa demais com histórico, edição e cancelamento.
  - **Manter as duas como estavam, só reordenadas:** o desafio continuaria a um toque de distância.
- **Como explicar a ordem:**
  - **Mesma regra, mostrada por inteiro:** escolhida. Cada linha mostra semanas e dias verificados, e uma frase diz por que a pessoa está à frente da seguinte. Não muda nada no servidor.
  - **Pontos:** um número só explicaria tudo, mas muda a regra e contraria "o aplicativo não usa pontos".
  - **Só dias verificados:** fácil de ler, mas premia volume em vez de constância.
- **Placar na praça:** pódio + "você" fixo; só um selo com a distância; ou **tirar da praça**. Escolhido: tirar. As medalhas 🥇🥈🥉 continuam na praça, e a Classificacao fica na página do grupo.
- **A semana na página do grupo:** seção recolhida; nada; ou **uma linha**. Escolhida: uma linha.

## Decisão

- **Página do grupo:** depois do cabeçalho do grupo, um cartão com o desafio em destaque: o nome, a situação, o andamento ("Semana 3 de 8 · até 29 de dez."), uma barra com uma parte por semana e as **Regras do desafio** em tópicos: período e número de semanas, meta de dias por semana, mínimo por dia (os treinos do dia somam), metas por tipo (somam em proporção), só treinos verificados contam, como se ganha e o desempate, participantes e entrada no meio, e o fuso. Um link leva a "Detalhes e seu histórico".
- **Classificacao:** abaixo do cartão, todos os participantes na ordem. Cada linha mostra a posição, o retrato, o primeiro nome, as semanas cumpridas, os dias verificados e a sequência (🔥, a partir de 2). Embaixo do nome, uma frase diz por que a pessoa está à frente da seguinte, pelo critério que decidiu: "1 semana à frente de Bia", "Empate em semanas, 3 dias a mais que você", "Empate em semanas e dias, sequência maior que Caio" ou "Empate com Duda". Com o desafio encerrado, o título é "Classificação final". Antes do primeiro dia verificado, uma frase toma o lugar da lista.
- **Semana:** uma linha abaixo da Classificacao: "Esta semana: 14/24 dias verificados do grupo · fecha em 2 dias e 5h". As barras da semana de cada pessoa ficam na página do desafio.
- **Página do desafio:** título "Desafio". Ordem: cartão com nome, descrição e regras; Classificacao; o histórico de quem vê; a semana de cada pessoa (só com o desafio ativo); cancelar.
- **Praça:** sem Placar. As medalhas ficam. O `resumo` deixa de trazer `posicaoInicioSemana`, que só servia às setas.

## Impacto

- Frontend: `GrupoPage`, `DesafioPage`, `RegrasDesafio`, `ListaClassificacao` e `explicarVantagem`; sai `PlacarJogo`.
- Backend: sai `ClassificacaoDesafio.CalcularRankingInicioSemana` e o campo `posicaoInicioSemana` do `resumo`.
- `docs/funcionalidades.md`, `docs/gamificacao.md` e o glossário do `AGENTS.md` deixam de citar o Placar.
