# 0089 — FADIGA visível ao grupo e recusa com mais peso

**Status:** DECIDIDA. Complementa `0059-fadiga.md` (cargas, acúmulo, redenção) e `0087-meia-devolucao-da-carta.md` (a recusa devolve o preço inteiro). Muda o limite de 1 carta por par a cada 24 horas (`gamificacao.md` §6.3) com a Revanche.

## Contexto

A FADIGA só aparecia para a própria pessoa (`GET /api/jogo/energia`). O grupo não sabia quem tinha recusado uma carta, então a consequência era invisível e a recusa continuava "segura". O mantenedor pediu:

- que o grupo veja a FADIGA dos colegas, de um jeito divertido e sem arte ou animação nova;
- que a recusa tenha mais consequências, justas e sem desmotivar o treino.

Valem as mesmas restrições da 0059: não tirar Energia já ganha, não dificultar o treino nem mexer no progresso oficial, e sair da punição só treinando.

## Alternativas consideradas

- **Marca social "Arregou" 🐔** na Mesa: só social, e pode magoar num grupo pequeno. Descartada; o tom fica brincalhão ("Sem gás").
- **Loja mais cara com FADIGA:** pune o comércio, não a recusa. Descartada.
- **FADIGA também nos bônus:** pesa demais no treino. Descartada.
- **Aviso ao grupo a cada recusa:** mais ruído nas notificações. A praça e o Foco bastam.
- **Escolhidas:** a bateria na praça, o bloqueio de Provocações, cargas por motivo, acúmulo só da recusa, redenção e Revanche.

## Decisão

**Visível ao grupo (só no modo Jogo):**

- `GET /api/grupos/{id}/cena` devolve, para cada membro no modo Jogo, `fadiga`: `{ restantes, motivo, carta, remetenteNome }`, ou `null`. A FADIGA vale em todos os grupos da pessoa; a carta e quem a mandou só aparecem se a Jogada for do grupo da cena.
- Na praça: a bateria fraca (o mesmo `fadiga.png` da 0059) e as cargas ao lado do nome; o avatar sem cor e mais lento (só CSS, fora do treino ao vivo e das encenações); uma gota 💧 de vez em quando. A bateria pisca quando uma carga some, e um ⚡ aparece quando ela zera. Com movimento reduzido, nada pisca nem cai.
- No Foco: "Sem gás · N treinos pra recarregar" e o motivo ("Recusou +20 MIN de Ana", "Não cumpriu …", "Deixou … vencer").

**Regras, para eventos a partir de `Fadiga.InicioRegras2` (24/11/2026, -03):**

- **Cargas por motivo:** recusa = 3; carta aceita e não cumprida (`Falhou`, ou `Expirada` depois do aceite) = 2; expirada sem resposta = 3. Tentar pesa menos que recusar.
- **Acúmulo:** só a recusa com FADIGA ativa soma 1 carga, até `Fadiga.Teto` = 5. Falha e expiração com FADIGA ativa não mudam nada.
- **Redenção:** cumprir uma Provocação recebida tira 1 carga, no `verified_at` mais cedo entre os treinos que contam para a Jogada. Sem FADIGA, a carta cumprida não guarda crédito. No mesmo instante, a ordem é: dia, redenção, gatilho.
- **Sem gás para provocar:** com FADIGA, `POST /api/grupos/{id}/jogadas` devolve 409 `FADIGA_ATIVA` ("Quem não aguenta, não provoca: …"). Vale para a Provocação e a Fusão; as Respostas, o DESAFIO DUPLO e a Loja continuam liberados.
- **Revanche:** a carta recusada devolve o preço inteiro a quem a mandou (soma na Fusão; a Jogada do DESAFIO DUPLO continua sem devolução). Se a última carta do remetente para essa pessoa foi recusada, ele pode mandar outra sem esperar as 24 horas. Vale uma vez: a carta da Revanche passa a ser a última. Os outros limites continuam.

## Casos de borda aceitos

- **A Energia já ganha não diminui**, com as mesmas exceções da 0059. O acúmulo só afeta dias verificados depois da recusa; a redenção, as cargas menores da falha e a devolução inteira só aumentam a Energia. Uma recusa entre `InicioRegras2` e a implantação pode reduzir um dia verificado nesse intervalo; o intervalo é de horas.
- **Jogada concluída e depois anulada** (`0054`): a redenção some, e a carga volta, como a recompensa da carta.
- **A Revanche no aparelho** não confere a data da recusa: uma recusa antiga, de menos de 24 horas, pode mostrar a Revanche, e o servidor devolve `JOGADA_RECENTE`.
- **Custo da cena:** para cada membro no modo Jogo com algum gatilho, a cena lê as Jogadas e os dias verificados dele. Os grupos são pequenos, e a praça se atualiza a cada 30 segundos.

## Impacto

- Sem migração. `Fadiga.Calcular` recebe os gatilhos com motivo e as cartas cumpridas; `JogadaCarregada` ganha `ConcluidaEm` (do `verified_at`) e `IsRecusaComRevanche`; `LimitesJogada.TemRevanche`; `ErrosJogo.FadigaAtiva`; `FadigaMembroResponse` na cena.
- Interface: `CenaGrupo` (bateria, `sprite-cansado`, gota, ⚡), `PainelFoco`, `MaoJogo` (bloqueio e Revanche), `RespostaCarta` (texto do botão e da devolução), `FadigaExplicada`, "Como jogar" e versão 1.9.0.
- Testes: `FadigaTests` (cargas, acúmulo, redenção, regra antiga, Revanche) e `FadigaRoutesTests` (cena, `FADIGA_ATIVA`, Revanche, redenção).
