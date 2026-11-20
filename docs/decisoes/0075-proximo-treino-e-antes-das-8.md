# 0075 — Só o próximo treino vale, e a ANTES DAS 8 olha o fim do treino

**Status:** DECIDIDA. Muda, em `0035-cartas-e-jogadas.md`, a "atividade que conta" (qualquer treino entre o envio e o prazo) e o objetivo da ANTES DAS 8. Acrescenta a situação `Falhou` à Jogada e um gatilho à FADIGA (`0059`). A soma do dia nas cartas de duração (`0040`), a Fusão (`0070`) e a carta perdida (`0074`) continuam valendo. Detalhes em `../gamificacao.md` §5.4, §6.1, §6.4 e §6.5.

## Contexto

- Uma Provocação aceita valia para qualquer treino entre o envio e o prazo. Quem recebia podia treinar como sempre e esperar um dia que, por acaso, cumprisse o objetivo. A carta não mudava o treino.
- O mantenedor pediu duas mudanças:
  - a carta vale só se o objetivo for cumprido no treino seguinte, e não em qualquer treino dentro do prazo;
  - a ANTES DAS 8 pede um treino que termine antes das 08:00, e não um que só comece antes.
- A situação da Jogada é calculada na leitura (`SituacaoJogada`), sem tarefa agendada nem coluna de resultado.

## Alternativas consideradas

- **Próximo treino contado do envio:** quem treina antes de abrir o aplicativo perde uma carta que nem viu. Descartada: conta do aceite.
- **Falha sem FADIGA, ou com a carta devolvida a quem enviou:** a regra fica mais fraca que deixar a carta expirar. Descartada: falhar é como expirar.
- **Próximo treino pela data de início:** um treino registrado depois, mas com início anterior, mudaria a vez de uma Jogada já concluída, e a recompensa sumiria. A ordem de registro não volta atrás. A pessoa ainda pode deixar de registrar um treino, mas aí ele também não conta para a meta.
- **Cartas de duração com um treino só:** quebraria a soma do dia (`0040`), que existe porque muita gente divide o treino. Descartada: vale o dia do próximo treino.
- **ANTES DAS 8 com fim estrito (antes das 08:00):** quem termina às 08:00 em ponto perderia por um minuto. Vale terminar até as 08:00.

## Decisão

- **A vez:** decide a Jogada o primeiro treino registrado (`created_at`) do destinatário no grupo, com início entre o aceite e o prazo, que não esteja `Rejeitada` nem `Anulada`. Um treino rejeitado, anulado ou apagado sai da conta, e a vez passa ao seguinte.
  - Em +10 MIN e +20 MIN fora da Fusão, a vez é o dia (no fuso do grupo) desse treino: os treinos desse dia somam, como na `0040`.
  - Treinos entre o envio e o aceite não contam nem fazem falhar.
- **Situações:** a vez que cumpre deixa a Jogada `AguardandoVerificacao` e, verificada, `Concluida`. A vez que não cumpre deixa a Jogada `Falhou`, mesmo antes de ser verificada:
  - num treino, no instante do registro;
  - num dia das cartas de duração, quando o dia acaba sem a soma. Se o prazo acaba antes do dia, a Jogada fica `Expirada`, como antes.
- **Consequências da falha:** as mesmas da expiração: FADIGA para quem recebeu, a partir do instante da falha, e a carta perdida para quem enviou (`0074`). Sem notificação, como na expiração.
- **ANTES DAS 8:** o treino termina (início + duração) até as 08:00 do dia em que começou, no fuso do grupo. Um treino que passa da meia-noite não conta. O prazo da carta não muda.
- **Formulário de treino:** com uma carta aceita, o aviso diz quando o treino faz a carta falhar, e postar pede confirmação ("Postar e perder a carta?"). A Mesa diz "Só vale o próximo treino depois de aceitar.", e a prévia do arremesso também.
- **Transição:** as Jogadas enviadas antes de `ObjetivoCarta.InicioProximoTreino` (20/11/2026, 13:00, UTC−3) seguem a regra antiga até o fim: qualquer treino no prazo conta, e a ANTES DAS 8 olha o início. Assim, nenhuma Jogada concluída deixa de estar.

## Impacto

- **Dificuldade:** a carta passa a pedir um plano para o treino seguinte. Mais Jogadas falham, então mais FADIGA e mais cartas perdidas. Com isso, a Fusão fica mais arriscada, porque o próximo treino precisa cumprir os dois objetivos.
- **Bônus da manhã e conquista `Madrugador`:** continuam pelo início do treino. Só a carta muda.
- **Backend:** `ObjetivoCarta` (`GetVez`, `GetFalha`, ANTES DAS 8), `SituacaoJogada.Avaliar` e `Falhou`, `Fadiga.GetGatilho`, `JogadasConcluidas.ConcluiAgora` (recebe também as pendentes, para saber a vez) e o SQL da janela (`Jogadas.InicioJanelaSql`). `GET /api/grupos/{id}/jogadas` ganha `respondidaEm` e a situação `Falhou`. Sem migração.
- **Frontend:** o selo "Não cumprida", o aviso e a confirmação no formulário de treino (`useCartasQueFalham`), o texto da ANTES DAS 8 ("Um treino que termine até as 8h") e a prévia do arremesso. A prévia do formulário usa a regra nova também para as Jogadas antigas, que somem em até 3 dias.
- Reavaliar com `metricas.bat` depois de duas semanas: Jogadas concluídas, falhas e expiradas. Se quase todas falharem, rever primeiro as cartas de duração.
