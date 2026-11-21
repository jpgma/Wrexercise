# 0083 — Fotos e vídeos depois do envio e nos comentários

**Status:** DECIDIDA. Altera o upload em atividade `RegistroManual` da `0011-atividades.md` (deixa de ser `409 COMPROVACAO_NAO_APLICAVEL`), o comentário só de texto da `0042-comentarios-sempre-abertos.md` e o que o token do app pode fazer na `0081-treino-ao-vivo-pelo-movr.md`.

## Contexto

- O autor só anexava evidências enquanto a atividade estava `EmAndamento` ou `AguardandoFeedback` (ou `Verificada` sem revisores). Depois disso, e em qualquer `RegistroManual`, a foto era recusada com `ATIVIDADE_ENCERRADA` ou `COMPROVACAO_NAO_APLICAVEL`. A página da atividade nem tinha onde mandar.
- Caso real: o Movr fechou no fim de um treino ao vivo, antes de mandar a imagem do resumo. A pessoa terminou aqui sem fotos (`RegistroManual`) e não tinha mais como pôr o print.
- Comentários eram só texto. Uma foto do pós-treino, do equipamento ou do print que faltou não tinha lugar na conversa.

## Alternativas consideradas

- **Só comentários com mídia:** resolve a conversa, mas o print que faltou ficaria fora do comprovante, longe de onde o grupo o procura.
- **Só evidência depois do prazo:** resolve o print, mas só o autor manda, e a conversa continua só de texto.
- **Evidência depois do prazo que volta a valer para a verificação:** reabre atividades já decididas e cria um jeito de "consertar" uma rejeição. Rejeitada.
- **O app manda o resumo que faltou como comentário:** daria ao token o poder de escrever comentários, que a ADR 0081 negou. Rejeitada: o resumo é evidência, como no fim normal.
- **As duas coisas, sem efeito na verificação:** escolhida.

## Decisão

### Evidência adicionada depois

- O autor manda fotos e vídeos em qualquer situação, menos `Anulada` (`409 ATIVIDADE_ENCERRADA`), também numa atividade `RegistroManual`.
- No prazo continua igual: `EmAndamento`, `AguardandoFeedback`, ou `Verificada` sem revisores, e a comprovação não é `RegistroManual`. Sem revisores, a primeira evidência no prazo verifica a atividade.
- Fora do prazo, a evidência entra com `adicionada_depois = true` (migração 044): aparece no comprovante com o selo "Adicionada depois", mas não verifica, não muda a situação e não muda o tipo de comprovação. O limite de 10, as recusas por foto repetida e os avisos continuam os mesmos.
- A regra fica em `PrazoEvidencia.Classificar`, pura e testada sem banco.
- `AtividadeResponse` ganha `AnexoPermitido` (o autor, fora de `Anulada`) e `EvidenciaResponse` ganha `AdicionadaDepois`. A página da atividade mostra "Adicionar foto ou vídeo" ao autor, fora do treino ao vivo, com vaga.

### Comentário com foto ou vídeo

- Um comentário tem texto, uma foto ou um vídeo, ou os dois. `comentarios.texto` passa a aceitar nulo, com a regra de que texto ou mídia existe.
- `POST /api/atividades/{id}/comentarios/midia` (multipart: `arquivo`, `miniatura`, `texto` opcional), com os limites de arquivo das evidências e o limite de 20 comentários por minuto. A mídia fica no mesmo armazenamento das evidências e entra no mesmo backup.
- A mídia do comentário não é evidência: sem avisos, sem checagem de foto repetida e sem efeito na verificação. Qualquer membro ativo manda, como o texto (ADR 0042). Continua sem edição e sem remoção.
- Os arquivos saem com a atividade: a exclusão e a expiração do treino ao vivo apagam também os da conversa.
- `LimiteUploadFilter` sai da fatia Atividades para `Infrastructure/Files`, porque Reacoes também o usa.

### O resumo que o app não mandou

- `PUT /api/integracoes/treinos/{externalId}/resumo`: depois que a pessoa terminou aqui, o app manda a imagem do resumo, uma vez só, como evidência `Integracao` adicionada depois. Repetir devolve `200` sem duplicar. Com o treino ainda aberto, `409 TREINO_EM_ANDAMENTO` (o resumo vai pelo fim).
- O token continua sem poder ler ou escrever comentários, terminar ou postar.

## Impacto

- **Migração 044:** `evidencias_atividade.adicionada_depois` e as colunas de mídia de `comentarios`.
- O código `COMPROVACAO_NAO_APLICAVEL` deixa de existir.
- O Movr ganha "Mandar o resumo para o Wrexercise" no histórico, para o treino ao vivo terminado sem a imagem.
- **Risco aceito:** um print mandado depois pode ser de outro dia. Ele não muda nada na verificação, e os avisos de data continuam aparecendo para o grupo.
- Relacionadas: 0011, 0042, 0052, 0058, 0065, 0078, 0081.
