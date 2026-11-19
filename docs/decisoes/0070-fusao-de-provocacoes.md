# 0070 — Fusão de Provocações

**Status:** DECIDIDA. Acrescenta uma mecânica às cartas de `0024`, `0033`, `0035` e `0036`. A FADIGA (`0059`) e os limites de envio (`0069`) continuam os mesmos. A `0074-carta-jogada-nao-volta.md` muda o que acontece com as duas cartas e a recompensa de quem enviou.

## Contexto

- Produção em 16/11/2026, segunda semana do Jogo: 4 pessoas, 1 grupo, 5 cartas compradas (175 EN) e 1 Jogada. Uma pessoa tem ANTES DAS 8, VARIEDADE e APOSTA paradas na Mão.
- Cada Provocação pede uma coisa só, e o limite é de uma Jogada por sentido entre duas pessoas. Quem junta cartas não tem como usá-las juntas na mesma pessoa.
- O pedido do mantenedor: jogar ANTES DAS 8 e VARIEDADE ao mesmo tempo, como uma carta só, mais difícil e que valha mais.
- A renda típica é de 90 a 110 EN por semana (`gamificacao.md` §5.1). Um prêmio alto demais desequilibra a Loja; um prêmio baixo não paga a dificuldade.

## Alternativas consideradas

- **Como cumprir:**
  - **No mesmo treino:** escolhida. Uma atividade cumpre os dois objetivos. É um desafio novo, e não duas cartas empilhadas.
  - **Em treinos separados, dentro do prazo:** equivale a duas Jogadas numa vaga só.
  - **Quem envia escolhe:** mais regra e mais interface.
- **Prêmio de quem cumpre:**
  - **Soma + 20%:** escolhida. De 78 a 102 EN, perto de uma semana de renda.
  - **Só a soma:** a única vantagem seria ocupar uma vaga.
  - **Soma + 50%:** até 127 EN por Jogada, mais que uma semana de renda.
- **Cumprir só um objetivo:**
  - **Tudo ou nada:** escolhida. Simples de explicar e de calcular.
  - **Crédito parcial** (paga a carta cumprida, sem FADIGA): mais justo com quem recebe, mas pede um estado novo de Jogada e devolve só uma das cartas.
  - **Tudo ou nada, com recusa sem FADIGA:** uma exceção a mais na FADIGA.
- **Pares:** qualquer par de Provocações diferentes (escolhida, inclusive +10 MIN com +20 MIN), uma lista de pares com nome próprio, ou um par só para começar.
- **Respostas:** todas menos MUDANÇA DE PLANOS (escolhida); todas, com a APOSTA dobrando também o bônus; ou só PRAZO EXTRA.
- **Onde montar:** escolher as duas cartas na hora de jogar (escolhida, sem tabela nem arte de carta nova); fundir antes, criando uma carta permanente na Mão; ou vender a carta fundida na Loja, o que não aproveita as cartas já compradas.
- **Trava contra abuso:** uma Fusão recebida ativa por pessoa, ou Fusão só em quem treinou na semana. Nenhuma foi adotada agora.

## Decisão

- **Fusão:** duas Provocações de códigos diferentes, da Mão de quem envia, numa Jogada só. São 10 pares. Ela ocupa uma vaga de Jogada.
- **Objetivo:** um mesmo treino cumpre os dois objetivos.
  - Numa Fusão, a duração de +10 MIN e +20 MIN é a dessa atividade, sem somar o dia (uma exceção à `0040`, só dentro da Fusão).
  - +10 MIN com +20 MIN pede a duração mínima do desafio + 30 minutos.
- **Tudo ou nada:** só a Jogada `Concluida` paga. Recusar, deixar expirar ou cumprir só um objetivo dá FADIGA a quem recebeu, e as duas cartas voltam para quem enviou. Concluída, a Fusão gasta as duas cartas.
- **Recompensa:** quem cumpre ganha a soma dos preços mais 20% da soma, arredondado para baixo. Quem enviou ganha metade da soma, arredondada para baixo.
- **Prazo:** o maior entre os prazos das duas cartas.
- **Respostas:** PRAZO EXTRA vale como sempre. A APOSTA dobra a soma, não o bônus. O DESAFIO DUPLO devolve a Fusão inteira. A MUDANÇA DE PLANOS não vale.
- **Sem trava nova:** valem os limites de envio de `0069`. A FADIGA não acumula (`0059`), o que limita o dano de Fusões difíceis em sequência.
- **Interface:** a Fusão é montada no Foco, tocando numa segunda Provocação. As cartas batem uma na outra com faíscas em pixel art e ficam juntas, com o selo "FUSÃO". O botão é "Jogar fusão".

## Impacto

- **Equilíbrio:** a Fusão é a maior fonte de Energia por Jogada. Com a APOSTA, VARIEDADE + ANTES DAS 8 paga 165 EN, mais que uma semana e meia de renda. Em troca, é mais difícil e pune a falha com FADIGA.
- **Princípio 3 (`gamificacao.md` §1.1):** uma Fusão recusada ou expirada dá FADIGA, como qualquer Provocação, mas é mais fácil de falhar. Reavaliar a trava (uma Fusão recebida ativa por pessoa) e o bônus de 20% com `metricas.bat` depois de duas semanas de uso.
- **Banco:** migração `039-add-fusao-jogada.sql`, com a coluna `jogadas.carta_fusao_id`. A duração-alvo e o tipo a evitar continuam nas colunas de sempre.
- **Backend (`Features/Jogo`):** `ObjetivoCarta.Cumpre`, `PrazoJogada.Calcular`, `CalculoEnergia.GetRecompensa`, `EstadoJogo` (cartas em jogo e gastas), `JogarCarta` (`cartaFusaoId`, erro `FUSAO_INVALIDA`), `ResponderJogada` e `JogadasConcluidas`. A resposta de `GET /api/grupos/{id}/jogadas` ganha `cartaFusao` e `recompensa`.
- **Frontend:** `MaoJogo` (montagem e animação), `MesaJogadas`, `CenaGrupo` (o par voa junto) e as animações `fusao-*` em `styles/index.css`, todas desligadas com movimento reduzido.
- **Arte:** `pixel-art/fusao/build.py` gera `fusao.png`, com 6 quadros de 32×32, CC0.
- **Operação:** `deploy/metricas.sql` conta as Fusões e mostra as duas cartas nas últimas Jogadas.
- **Documentação:** `gamificacao.md` §6.5, `funcionalidades.md` §3.3.2 a §3.3.4 e o glossário de `AGENTS.md` (`Fusao`).
