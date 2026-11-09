# Modelo de dados — Wrexercise

Este documento descreve o esquema em uso e cresce junto com as migrações. O modelo planejado está em `arquitetura.md`, seções 7 e 9. A fonte da verdade são os arquivos em `Wrexercise/Infrastructure/Database/Migrations/`.

## `usuarios`

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `nome_exibicao` | `text` | Obrigatória |
| `email` | `text` | Único, sem diferenciar maiúsculas de minúsculas |
| `password_hash` | `text` | Obrigatória |
| `created_at` | `timestamptz` | Padrão `now()` |

## `sessoes_autenticacao`

Uma linha por sessão: cada dispositivo tem a sua. Ver `decisoes/0006-autenticacao-por-sessao.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `usuario_id` | `uuid` | Referencia `usuarios`, apagada junto com o usuário. Indexada |
| `token` | `text` | Único. Legível, para os administradores verem o link |
| `created_at` | `timestamptz` | Padrão `now()` |
| `expires_at` | `timestamptz` | Obrigatória. Renovada com o uso |
| `revoked_at` | `timestamptz` | Preenchida ao sair |

## `redefinicoes_senha`

Links de uso único criados pelo operador com o comando `reset-senha`. Ver `decisoes/0019-conta-e-redefinicao-de-senha.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `usuario_id` | `uuid` | Referencia `usuarios`, com `ON DELETE CASCADE` |
| `token_hash` | `bytea` | SHA-256 do token, único |
| `created_at` | `timestamptz` | Padrão `now()` |
| `expires_at` | `timestamptz` | 1 hora depois da criação |
| `used_at` | `timestamptz` | Preenchida ao usar, ou quando outro link é gerado |

## `grupos`

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `nome` | `text` | Obrigatória |
| `criador_id` | `uuid` | Referencia `usuarios`. Não muda com a transferência da propriedade |
| `fuso_horario` | `text` | Nome IANA, validado contra `pg_timezone_names` |
| `created_at` | `timestamptz` | Padrão `now()` |

## `membros_grupo`

Uma linha por usuário e grupo. Sair do grupo preenche `removed_at`, e voltar reaproveita a linha, mantendo o `joined_at` original. Ver `decisoes/0008-grupos-e-convites.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `grupo_id` | `uuid` | Parte da chave primária. Referencia `grupos` |
| `usuario_id` | `uuid` | Parte da chave primária. Referencia `usuarios`. Indexada entre os membros ativos |
| `papel` | `text` | `Proprietario`, `Administrador` ou `Membro`. Um único `Proprietario` ativo por grupo, garantido por índice |
| `joined_at` | `timestamptz` | Padrão `now()`. Renovada ao voltar |
| `removed_at` | `timestamptz` | Preenchida ao sair ou ser removido |

## `convites_grupo`

Um link reutilizável por grupo. Só vale enquanto `revoked_at` estiver vazia e `expires_at` estiver no futuro. Ver `decisoes/0017-convite-reutilizavel.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `grupo_id` | `uuid` | Referencia `grupos`. Indexada |
| `criador_id` | `uuid` | Referencia `usuarios` |
| `token_hash` | `bytea` | SHA-256 do token, único. O token em si nunca é gravado |
| `created_at` | `timestamptz` | Padrão `now()` |
| `expires_at` | `timestamptz` | Obrigatória. 7 dias depois da criação |
| `revoked_at` | `timestamptz` | Preenchida ao cancelar, ao gerar outro link, quando quem criou deixa o grupo ou quando alguém é removido. No máximo uma linha do grupo fica sem ela |

## `desafios`

As datas são dias do calendário no fuso do grupo. Só semanas inteiras, garantidas por `CHECK`. A situação não é gravada: vem das datas e de `cancelled_at`. Ver `decisoes/0009-desafios.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `grupo_id` | `uuid` | Referencia `grupos`. Indexada com `data_inicio` |
| `criador_id` | `uuid` | Referencia `usuarios` |
| `nome` | `text` | Obrigatória |
| `descricao` | `text` | Opcional |
| `data_inicio` | `date` | Sempre uma segunda-feira |
| `data_fim` | `date` | Sempre um domingo, posterior ao início. Incluída no período |
| `duracao_minima_segundos` | `integer` | Positiva. Mínimo por dia: as atividades do dia se somam (`decisoes/0040-meta-diaria.md`) |
| `sessoes_por_semana` | `integer` | Positiva. Dias por semana que alcançam o mínimo; no máximo 1 por dia |
| `created_at` | `timestamptz` | Padrão `now()` |
| `cancelled_at` | `timestamptz` | Preenchida ao cancelar |

## `atividades`

Ver `decisoes/0011-atividades.md` e migração `006-create-atividades.sql`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `grupo_id` | `uuid` | Referencia `grupos` |
| `usuario_id` | `uuid` | Autor; referencia `usuarios` |
| `tipo` | `text` | Tipo de exercício (§7.5) |
| `duracao_segundos` | `integer` | Positiva |
| `started_at` | `timestamptz` | Define a semana no fuso do grupo |
| `descricao` | `text` | Opcional |
| `tipo_comprovacao` | `text` | `RegistroManual`, `Fotografia`, `CapturaDeTela` |
| `situacao_verificacao` | `text` | `AguardandoFeedback`, `Verificada`, `EmRevisao`, `Contestada`, `Rejeitada` |
| `submitted_at` | `timestamptz` | Início da janela de reações (48 h) |
| `created_at` | `timestamptz` | Padrão `now()` |

## `evidencias_atividade`

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `atividade_id` | `uuid` | Referencia `atividades` |
| `caminho_relativo` | `text` | Sob o volume de evidências |
| `tipo_mime` | `text` | Validado no upload |
| `tamanho_bytes` | `integer` | Positivo |
| `sha256` | `text` | Hash do arquivo |
| `ordem` | `smallint` | 1–3 por atividade |
| `caminho_miniatura` | `text` | Opcional. JPEG gerado no navegador, sob o mesmo volume |
| `created_at` | `timestamptz` | Padrão `now()` |

## `revisores_atividade`

Snapshot na criação da atividade. Ver `decisoes/0012-verificacao-comunitaria.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `atividade_id` | `uuid` | Parte da chave primária. Referencia `atividades` |
| `usuario_id` | `uuid` | Parte da chave primária. Referencia `usuarios` |

## `reacoes_atividade`

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `atividade_id` | `uuid` | Referencia `atividades` |
| `usuario_id` | `uuid` | Referencia `usuarios` |
| `tipo` | `text` | `Kudos`, `Suspeito`, `Rejeitar` |
| `created_at` | `timestamptz` | Padrão `now()` |
| `updated_at` | `timestamptz` | Atualizada a cada alteração |

Restrição: uma reação por usuário e atividade.

## `historico_revisao`

Auditoria de decisões administrativas.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `atividade_id` | `uuid` | Referencia `atividades` |
| `responsavel_id` | `uuid` | Referencia `usuarios` |
| `decisao` | `text` | `Verificada` ou `Rejeitada` |
| `justificativa` | `text` | Obrigatória |
| `created_at` | `timestamptz` | Padrão `now()` |

## `comentarios_revisao`

Conversa sobre uma atividade em revisão. Membros ativos escrevem apenas durante `EmRevisao`, e os comentários não podem ser editados nem removidos.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `atividade_id` | `uuid` | Referencia `atividades` |
| `usuario_id` | `uuid` | Referencia `usuarios` |
| `texto` | `text` | De 1 a 500 caracteres |
| `created_at` | `timestamptz` | Padrão `now()` |

## `notificacoes`

Avisos dentro do aplicativo. Reações e comentários não lidos de uma atividade ficam em uma linha só. Toda leitura cruza com o vínculo ativo em `membros_grupo`. Apagadas 90 dias depois da última atualização. Ver `decisoes/0020-notificacoes-no-aplicativo.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `usuario_id` | `uuid` | Quem recebe. Referencia `usuarios` |
| `grupo_id` | `uuid` | Referencia `grupos` |
| `atividade_id` | `uuid` | Referencia `atividades` |
| `tipo` | `text` | `NovaAtividade`, `Reacao`, `Comentario`, `SituacaoAlterada`, `Disputa` |
| `ator_id` | `uuid` | Opcional. Quem causou, ou a última pessoa, nas agrupadas |
| `situacao` | `text` | Só em `SituacaoAlterada` |
| `total` | `integer` | Eventos agrupados na linha |
| `created_at` | `timestamptz` | Padrão `now()` |
| `updated_at` | `timestamptz` | Padrão `now()` |
| `read_at` | `timestamptz` | Vazia enquanto não lida |

## `schema_migrations`

Tabela de controle do executor de migrações. Ver `decisoes/0001-migracoes-sql.md`.
