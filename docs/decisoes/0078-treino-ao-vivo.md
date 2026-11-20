# 0078 — Treino ao vivo: "Começar treino" e "Terminar treino"

**Status:** DECIDIDA. Acrescenta ao registro de atividade (`0011`) um segundo jeito de registrar: começar agora e terminar depois. O registro de um treino já feito continua igual. Muda o limite de evidências de `0011` e `0058` (de 3 para 10).

## Contexto

- Até aqui, o treino só era registrado depois de feito. O grupo só via o colega quando ele postava, às vezes horas depois.
- O mantenedor pediu um "check-in/check-out": ao começar, o Avatar treina na praça e o grupo vê que a pessoa está treinando agora. Enquanto o treino corre, a pessoa vê o cronômetro e o botão de terminar, em qualquer tela e aparelho. O registro posterior precisa continuar funcionando como hoje.
- Pediu também ideias para deixar isso divertido e mais fotos por treino, para ir mandando durante o exercício.
- Não há WebSocket nem SSE (`docs/arquitetura.md`): o "ao vivo" do grupo vem de consultas periódicas e de push.

## Alternativas consideradas

- **Tabela separada (`treinos_em_andamento`), no modelo do `Rascunho` (`0065`):** nenhuma consulta de atividades precisaria saber dela. Descartada pelo mantenedor: preferiu uma atividade com uma situação nova, que já tem evidências, feed e página.
- **Criar a atividade só no fim, com o início guardado no aparelho:** não aparece para o grupo nem em outro aparelho. Descartada.
- **WebSocket ou SSE para a praça:** proibidos sem decisão. Descartada: a praça consulta a cena a cada 30 segundos enquanto está visível.
- **Pausar o cronômetro:** mais estado e mais regras (duração líquida, várias pausas). Descartada: ao terminar, a pessoa só pode diminuir a duração.
- **Push a cada Torcida:** viraria ruído. Descartada: a Torcida aparece no cronômetro e na lista do app.
- **Limite próprio para vídeos (ex.: 3 dos 10):** descartado pelo mantenedor. O tamanho de cada arquivo continua limitado (5 MB por foto, 25 MB por vídeo).

## Decisão

- **Situação `EmAndamento`.** "Começar treino" (`POST /api/grupos/{id}/atividades/em-andamento`, só o tipo) cria a atividade com `situacao_verificacao = 'EmAndamento'`, `ao_vivo = true`, início agora, duração provisória de 60 segundos e comprovação provisória `Fotografia` (para aceitar fotos). Ela não tem revisores e não conta para nada: progresso, classificação, Energia, cartas, fila de avaliação. As consultas que excluíam `Rejeitada` e `Anulada` também excluem `EmAndamento`. Quem treina ao vivo está acordado (`MembrosDormindo`).
- **Um treino ao vivo por pessoa**, em todos os grupos (índice único parcial). Com ele aberto, o registro de um treino já feito responde `409 TREINO_EM_ANDAMENTO`. Repetir "Começar" no mesmo grupo devolve o mesmo treino com `200`.
- **Mesmas travas do registro:** fila de avaliação (`AVALIACOES_PENDENTES`) e carta sem resposta (`CARTA_SEM_RESPOSTA`, `0076`).
- **"Terminar treino"** (`POST /api/atividades/{id}/terminar`): o início fica travado e a duração vai de 60 segundos até o tempo decorrido; com evidência, a comprovação não pode ser `RegistroManual`. A atividade passa a `AguardandoFeedback` e o `submitted_at` passa a ser agora. Daí em diante vale tudo do registro, sem regra nova: RegistroTardio, revisores, `NovaAtividade`, verificação automática sem revisores, janela de 48 horas e os 15 minutos para apagar. Os avisos de data das evidências são refeitos com a duração final. O código do fim do registro ficou em `RegistroAtividade.FinalizarAsync`, usado pelos dois caminhos.
- **"Cancelar treino"**: o autor apaga o treino ao vivo a qualquer momento, sem penalidade.
- **Esquecido:** depois de 3 horas, a notificação `TreinoLongo` (com push). Ao terminar depois de 4 horas, a pessoa confirma o tempo ou diminui. Depois de 24 horas (a duração máxima), o treino expira e sai, com as evidências. Um serviço periódico faz as duas coisas a cada 5 minutos.
- **Até 10 evidências** por atividade, fotos ou vídeos, em qualquer mistura. Durante o treino, cada uma sobe na hora e a localização não sai do aparelho. **Atualização (2026-11-20):** a ADR 0080 deixa o treino guardar onde começou, se a pessoa escolher; as fotos e os vídeos continuam sem local.
- **No aplicativo:** o formulário começa em "Começar agora" (ou "Já treinei"). O cronômetro (`/grupos/{id}/treino`) mostra o tempo, o Avatar treinando e as Torcidas. Ao abrir o app com treino aberto, ele abre o cronômetro. Nas outras telas, um selo "AO VIVO" leva até ele. A Chegada de uma carta espera o fim do treino.
- **Para o grupo:** a cena traz `treinoAoVivo` de cada membro; o Avatar treina (correr na Corrida, andar na Caminhada), com a bolha "AO VIVO" e os minutos. O Placar marca "AO VIVO". Quem treina ao vivo fica lado a lado na praça (Treino junto). A notificação `ComecouTreinar` (com push) vai aos colegas acordados, no máximo uma por pessoa e grupo a cada 12 horas. O feed mostra o treino ao vivo com "Torcer".
- **Torcida:** `POST /api/atividades/{id}/torcida`, uma por colega a cada 10 minutos por treino, nunca no próprio. Tabela `torcidas`. Notificação `Torcida`, só na lista.
- **Energia:** bônus Ao vivo (+3) para o treino começado ao vivo e Parceria (+3) quando dois treinos ao vivo de pessoas diferentes do mesmo grupo, ambos verificados, se sobrepõem por 15 minutos ou mais. Os dois ficam dentro do teto de bônus por atividade (10). Como nada mais pode ser registrado com um treino ao vivo aberto, a ordem de registro (`created_at`, decisão 0034) continua valendo: o treino ao vivo não toma o bônus de uma atividade posterior.
- Uma Provocação aceita conta o próximo treino com início depois do envio da carta (`0075`, `0076`). Como "Começar" é recusado com carta sem resposta, um treino ao vivo que já estava aberto quando a carta chegou não vale para ela.

## Impacto

- Migração `040-add-treino-ao-vivo.sql`: situação `EmAndamento`, coluna `ao_vivo`, índice único parcial, tabela `torcidas`, tipos de notificação `ComecouTreinar`, `Torcida` e `TreinoLongo`, e `ordem` das evidências até 10.
- Novas operações em `Features/Atividades` (`ComecarTreino`, `TerminarTreino`, `ObterTreinoAtual`, `ExpiracaoTreinos`) e em `Features/Reacoes` (`Torcer`).
- A praça passa a consultar a cena a cada 30 segundos, e o cronômetro consulta o treino atual a cada 15 segundos, só com a tela visível. É o único polling novo.
- Nenhuma arte nova: o Avatar usa as linhas `treinando`, `correr`, `andar` e `comemorar` que já existem.
