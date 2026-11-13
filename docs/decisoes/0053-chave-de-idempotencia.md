# 0053 — Chave de idempotência no registro de atividade

**Status:** DECIDIDA. Fecha Q020 junto com 0052. Complementa `0011-atividades.md`.

## Contexto

O registro chega do celular, muitas vezes com conexão ruim. Se o servidor cria a atividade e a resposta se perde, o formulário mostra o erro e a pessoa toca de novo em "Registrar": a atividade fica duplicada. Ninguém pode apagar uma atividade, então a cópia só sai se o grupo a rejeitar.

0052 já recusa a foto repetida, mas uma atividade sem foto, ou a própria atividade, ainda duplicava.

## Alternativas consideradas

- **Detectar duplicata pelos dados** (mesmo tipo, início e duração em poucos minutos): também recusaria dois treinos parecidos e verdadeiros, e o limite seria arbitrário.
- **Desabilitar o botão durante o envio:** já existe e não resolve a resposta perdida.
- **Chave no corpo JSON:** funcionaria, mas o cabeçalho `Idempotency-Key` é o padrão das APIs HTTP e deixa o contrato da atividade igual.
- **Cabeçalho `Idempotency-Key` com um UUID do navegador:** escolhida.

## Decisão

- O navegador gera um UUID (`crypto.randomUUID()`) para os dados do formulário e o envia no cabeçalho `Idempotency-Key` de `POST /api/grupos/{grupoId}/atividades`. Uma nova tentativa com os mesmos dados repete a chave; dados diferentes geram outra.
- O servidor guarda a chave em `atividades.idempotency_key`, única por usuário (índice parcial). Se a chave já existe para quem registra, no mesmo grupo, devolve a atividade existente com `200` em vez de `201`. Nada é criado de novo, e ninguém é avisado de novo.
- A busca pela chave vem antes da fila de avaliação: se outra pessoa registrou nesse meio-tempo, a repetição não recebe `409 AVALIACOES_PENDENTES`.
- Duas requisições simultâneas com a mesma chave: `INSERT ... ON CONFLICT DO NOTHING` faz a segunda esperar a primeira, e ela devolve a atividade criada.
- A mesma chave em outro grupo: `422 CHAVE_IDEMPOTENCIA_REUTILIZADA`. Chave que não é UUID: `400 DADOS_INVALIDOS`. Sem o cabeçalho, a criação segue como antes.
- A chave não expira: é um UUID e ocupa 16 bytes por atividade.

### Evidências na repetição

A repetição reenvia as fotos. Uma foto que já está na mesma atividade recebe `409 EVIDENCIA_JA_ANEXADA`, e o formulário a conta como enviada. Essa verificação vem antes do limite de 3 evidências, para a repetição completa não receber `LIMITE_EVIDENCIAS`. Foto de outra atividade continua com `EVIDENCIA_DUPLICADA` (0052).

## Impacto

- **Migração 028:** coluna `idempotency_key` e o índice único parcial `(usuario_id, idempotency_key)`.
- **Dados alterados entre as tentativas:** geram outra chave e, portanto, outra atividade. É o comportamento certo: a pessoa mudou o registro.
- **Fora do formulário:** outras criações (comentário, reação, Jogada) não usam chave. A reação já é idempotente (`PUT`), e um comentário duplicado não muda nenhuma regra.
- Relacionadas: 0011, 0041, 0052.
