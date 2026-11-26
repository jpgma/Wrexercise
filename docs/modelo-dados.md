# Modelo de dados — Wrexercise

Este documento descreve o esquema em uso e cresce junto com as migrações. Cobre todas as tabelas das migrações 001 a 046. O modelo planejado está em `arquitetura.md`, seções 7 e 9. A fonte da verdade são os arquivos em `Wrexercise/Infrastructure/Database/Migrations/`.

## `usuarios`

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `nome_exibicao` | `text` | Obrigatória |
| `email` | `text` | Único, sem diferenciar maiúsculas de minúsculas |
| `password_hash` | `text` | Obrigatória |
| `created_at` | `timestamptz` | Padrão `now()` |
| `modo_inicio` | `text` | `App` (padrão) ou `Jogo`. Tela inicial de cada conta |

## `sessoes_autenticacao`

Uma linha por sessão: cada dispositivo tem a sua. Ver `decisoes/0006-autenticacao-por-sessao.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `usuario_id` | `uuid` | Referencia `usuarios`, apagada junto com o usuário. Indexada |
| `token_hash` | `bytea` | SHA-256 do token, único. O token em si nunca é gravado |
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
| `token` | `text` | Único. Legível, para os administradores verem o link. O convite antigo, com hash, foi apagado (migração 010) |
| `created_at` | `timestamptz` | Padrão `now()` |
| `expires_at` | `timestamptz` | Obrigatória. 7 dias depois da criação |
| `revoked_at` | `timestamptz` | Preenchida ao cancelar, ao gerar outro link, quando quem criou deixa o grupo ou quando alguém é removido. No máximo uma linha do grupo fica sem ela (índice único parcial) |

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

## `metas_tipo_desafio`

Um mínimo próprio para estes tipos, menor ou maior que a duração mínima do desafio. Um tipo fica em no máximo uma meta. Cada atividade preenche os segundos dela divididos pelo mínimo do tipo, e o dia conta quando as partes somam 100%. Não mudam depois da criação. Ver `decisoes/0043-metas-por-tipo.md` e `decisoes/0045-metas-proporcionais.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `desafio_id` | `uuid` | Referencia `desafios` |
| `ordem` | `smallint` | Posição no formulário, a partir de 0. Única por desafio; desempata qual meta fez o dia contar |
| `nome` | `text` | Opcional, até 20 caracteres |
| `tipos` | `text[]` | Tipos de atividade (§7.5), pelo menos um, sem repetição |
| `duracao_minima_segundos` | `integer` | Positiva e menor que a do desafio |
| `created_at` | `timestamptz` | Padrão `now()` |

## `atividades`

Ver `decisoes/0011-atividades.md` e migração `006-create-atividades.sql`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `grupo_id` | `uuid` | Referencia `grupos` |
| `usuario_id` | `uuid` | Autor; referencia `usuarios` |
| `tipo` | `text` | Tipo de exercício (§7.5): `Musculacao`, `Corrida`, `Caminhada`, `Ciclismo`, `Natacao`, `Futebol`, `Volei`, `Luta`, `Esporte`, `Yoga`, `Calistenia`, `Outro` |
| `duracao_segundos` | `integer` | Positiva |
| `started_at` | `timestamptz` | Define a semana no fuso do grupo |
| `descricao` | `text` | Opcional |
| `tipo_comprovacao` | `text` | `RegistroManual`, `Fotografia`, `CapturaDeTela` |
| `situacao_verificacao` | `text` | `EmAndamento` (treino ao vivo, ADR 0078), `AguardandoFeedback`, `Verificada`, `EmRevisao`, `Contestada`, `Rejeitada`, `Anulada` (ADR 0054). Só uma `EmAndamento` por pessoa (índice único) |
| `submitted_at` | `timestamptz` | Início da janela de reações (48 h) |
| `created_at` | `timestamptz` | Padrão `now()` |
| `verified_at` | `timestamptz` | Quando ficou `Verificada`. Obrigatória nessa situação. Decide quem cumpriu a meta primeiro no Rival |
| `idempotency_key` | `uuid` | Opcional. Com `usuario_id`, é única: repetir o registro devolve a atividade já criada (ADR 0053) |
| `ao_vivo` | `boolean` | `true` quando começou pelo "Começar treino" (ADR 0078). Padrão `false` |
| `latitude`, `longitude` | `double precision` | Opcionais, sempre juntas. Local do início, só quando a pessoa escolhe mostrar ao grupo (ADR 0080) |
| `app`, `app_external_id`, `app_duracao_segundos`, `app_ended_at` | | Vínculo com um app conectado (ADR 0081). Opcional. Único por (`usuario_id`, `app`, `app_external_id`) |

## `evidencias_atividade`

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `atividade_id` | `uuid` | Referencia `atividades` |
| `caminho_relativo` | `text` | Sob o volume de evidências |
| `tipo_mime` | `text` | Validado no upload |
| `tamanho_bytes` | `integer` | Positivo |
| `sha256` | `text` | Hash do arquivo |
| `ordem` | `smallint` | 1–10 por atividade (era 1–3; migração 040). Única por atividade |
| `caminho_miniatura` | `text` | Opcional. JPEG gerado no navegador, sob o mesmo volume |
| `origem_data_captura` | `text` | `Exif`, `Arquivo`, `Camera`, `Print`, `Integracao` (só o servidor, ADR 0065) ou `Percurso` (só o servidor, uma por atividade, ADR 0084) |
| `capturada_em` | `timestamptz` | Data da captura, quando a origem é conhecida. Nula junto com `origem_data_captura` |
| `sha256_original` | `text` | Hash do arquivo antes da compressão. Opcional |
| `dhash` | `bigint` | Impressão visual da imagem (ou do quadro do meio, no vídeo), para achar fotos parecidas |
| `latitude`, `longitude` | `double precision` | Opcionais, sempre juntas. Limpas no ADR 0061, quando a localização era guardada sem escolha |
| `duracao_ms` | `int` | Só em vídeo, entre 2 e 61 segundos |
| `print` | `boolean` | `true` quando é uma captura de tela (ADR 0060). Padrão `false` |
| `adicionada_depois` | `boolean` | `true` quando foi enviada depois do prazo da verificação (ADR 0083). Não verifica nem muda a comprovação. Padrão `false` |
| `created_at` | `timestamptz` | Padrão `now()` |

## `percursos`

O Percurso do treino ao vivo (ADR 0084, migração `045-create-percursos.sql`). A linha existe só quando a pessoa escolheu gravar; o resumo é refeito a partir dos pontos a cada lote, ao terminar e ao corrigir a duração.

| Coluna | Tipo | Observações |
|---|---|---|
| `atividade_id` | `uuid` | Chave primária. Referencia `atividades`; sai junto com ela |
| `distancia_metros` | `integer` | Dentro do período do treino, sem as lacunas |
| `velocidade_maxima` | `double precision` | m/s, em janelas de 10 s ou mais; nula sem janela |
| `ganho_elevacao_metros` | `integer` | Aproximado; nulo quando o GPS não mandou altitude |
| `total_pontos` | `integer` | Até 20.000 |
| `updated_at` | `timestamptz` | Último recálculo |

## `pontos_percurso`

Os pontos crus, como o aparelho os mandou. O filtro (precisão, saltos, lacunas, tremor) é feito na leitura, em `PercursoAtividade.cs`. Chave primária `(atividade_id, seq)`: repetir um lote não duplica nada.

| Coluna | Tipo | Observações |
|---|---|---|
| `atividade_id` | `uuid` | Referencia `percursos` |
| `seq` | `integer` | Ordem do ponto no aparelho, de 0 a 100.000 |
| `recorded_at` | `timestamptz` | Quando o GPS mediu |
| `latitude`, `longitude` | `double precision` | Nunca vão para o log |
| `precisao_metros` | `real` | Até 30 |
| `altitude_metros` | `real` | Opcional |
| `segmento` | `smallint` | Muda a cada reinício da gravação no aparelho |

## `codigos_integracao`

Código de uso único da conexão de um app (ADR 0065). Ver migração `037-create-integracoes.sql`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `usuario_id` | `uuid` | Quem autorizou; referencia `usuarios` |
| `app` | `text` | `Movr` |
| `code_hash` | `bytea` | SHA-256 do código; único |
| `code_challenge` | `text` | Desafio PKCE S256 |
| `redirect_uri` | `text` | Endereço de retorno da lista fechada |
| `created_at` | `timestamptz` | Padrão `now()` |
| `expires_at` | `timestamptz` | 5 minutos depois |
| `used_at` | `timestamptz` | Vazia até a troca pelo token |

## `apps_conectados`

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `usuario_id` | `uuid` | Referencia `usuarios` |
| `app` | `text` | `Movr` |
| `token_hash` | `bytea` | SHA-256 do token; único |
| `created_at` | `timestamptz` | Padrão `now()` |
| `last_used_at` | `timestamptz` | Último uso do token |
| `revoked_at` | `timestamptz` | Vazia enquanto ativo |

## `rascunhos_atividade`

Treino preparado por um app conectado. Único por (`usuario_id`, `app`, `external_id`).

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `usuario_id` | `uuid` | Referencia `usuarios` |
| `app` | `text` | `Movr` |
| `external_id` | `text` | Identificador do treino no app |
| `tipo` | `text` | Tipo de atividade |
| `duracao_segundos` | `integer` | 60 a 86400 |
| `started_at`, `ended_at` | `timestamptz` | Início e fim do treino |
| `caminho_relativo`, `tipo_mime`, `tamanho_bytes`, `sha256` | | Imagem do resumo, no volume de evidências |
| `caminho_miniatura` | `text` | JPEG |
| `created_at`, `updated_at` | `timestamptz` | Padrão `now()` |
| `expires_at` | `timestamptz` | `started_at` + 48 horas |
| `atividade_id` | `uuid` | A atividade postada; `ON DELETE SET NULL` |

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
| `decisao` | `text` | `Verificada`, `Rejeitada` ou `Anulada` (ADR 0054) |
| `justificativa` | `text` | Obrigatória |
| `created_at` | `timestamptz` | Padrão `now()` |

## `correcoes_duracao`

Correções da duração de uma atividade feitas pelo proprietário ou por um administrador. Todo o grupo as vê na atividade. Ver `decisoes/0068-correcao-da-duracao.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `atividade_id` | `uuid` | Referencia `atividades`; sai junto com a atividade |
| `responsavel_id` | `uuid` | Referencia `usuarios` |
| `duracao_anterior_segundos` | `integer` | Maior que zero |
| `duracao_nova_segundos` | `integer` | Maior que zero e diferente da anterior |
| `justificativa` | `text` | Obrigatória, até 500 caracteres |
| `created_at` | `timestamptz` | Padrão `now()` |

## `comentarios`

Conversa sobre uma atividade. Membros ativos do grupo escrevem a qualquer momento, e os comentários não podem ser editados nem removidos. Ver `decisoes/0042-comentarios-sempre-abertos.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `atividade_id` | `uuid` | Referencia `atividades` |
| `usuario_id` | `uuid` | Referencia `usuarios` |
| `texto` | `text` | Opcional quando há foto ou vídeo. De 1 a 500 caracteres |
| `caminho_relativo`, `tipo_mime`, `tamanho_bytes`, `caminho_miniatura`, `duracao_ms` | | Foto ou vídeo opcional do comentário, sempre juntos (ADR 0083). Não é evidência |
| `created_at` | `timestamptz` | Padrão `now()` |

## `notificacoes`

Avisos dentro do aplicativo. Reações e comentários não lidos de uma atividade ficam em uma linha só. Toda leitura cruza com o vínculo ativo em `membros_grupo`. Apagadas 90 dias depois da última atualização. Ver `decisoes/0020-notificacoes-no-aplicativo.md`.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `usuario_id` | `uuid` | Quem recebe. Referencia `usuarios` |
| `grupo_id` | `uuid` | Referencia `grupos` |
| `atividade_id` | `uuid` | Opcional. Referencia `atividades` |
| `jogada_id` | `uuid` | Opcional. Referencia `jogadas`, com `ON DELETE CASCADE` |
| `rival_id` | `uuid` | Opcional. Referencia `rivais`, com `ON DELETE CASCADE` |
| `cutucada_id` | `uuid` | Opcional. Referencia `cutucadas`, com `ON DELETE CASCADE` |
| `tipo` | `text` | `NovaAtividade`, `Reacao`, `Comentario`, `SituacaoAlterada`, `Disputa`, `CartaRecebida`, `CartaAceita`, `CartaRecusada`, `JogadaConcluida`, `RivalEscolhido`, `Cutucada`, `ComecouTreinar`, `Torcida`, `TreinoLongo`, `TreinoAppConcluido`, `CartaNaoCumprida` |
| `ator_id` | `uuid` | Opcional. Quem causou, ou a última pessoa, nas agrupadas |
| `situacao` | `text` | Só em `SituacaoAlterada`, e em `CartaNaoCumprida` (`Falhou` ou `Expirada`) |
| `energia` | `integer` | Energia devolvida, para o texto "+N EN de volta" (ADR 0087). Positiva |
| `total` | `integer` | Eventos agrupados na linha |
| `created_at` | `timestamptz` | Padrão `now()` |
| `updated_at` | `timestamptz` | Padrão `now()` |
| `read_at` | `timestamptz` | Vazia enquanto não lida |
| `push_enviado_em` | `timestamptz` | Vazia enquanto o aviso não foi ao telefone (ADR 0049) |

Exatamente uma das referências (`atividade_id`, `jogada_id`, `rival_id`, `cutucada_id`) está preenchida. Um aviso `CartaNaoCumprida` existe no máximo um por Jogada.

## `avisos_atividade`

Avisos automáticos sobre a evidência, lidos no navegador. Só informam: não bloqueiam nem provam nada (ADR 0052, 0058, 0060).

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `atividade_id` | `uuid` | Referencia `atividades` |
| `evidencia_id` | `uuid` | Opcional. Referencia `evidencias_atividade` |
| `tipo` | `text` | `FotoForaDoPeriodo`, `FotoSemData`, `FotoParecida`, `RegistroTardio`, `VideoParado`. Os nomes `Foto*` valem também para vídeos |
| `atividade_relacionada_id` | `uuid` | Só em `FotoParecida`: a atividade da foto parecida, que pode ser de outro grupo |
| `created_at` | `timestamptz` | Padrão `now()` |

## `push_subscriptions`

Um aparelho com o push ligado (ADR 0049).

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `usuario_id` | `uuid` | Referencia `usuarios`, com `ON DELETE CASCADE`. Indexada |
| `endpoint` | `text` | Único |
| `p256dh`, `auth` | `text` | Chaves da inscrição |
| `created_at`, `updated_at` | `timestamptz` | Padrão `now()` |

## `cartas`

Cartas compradas na Loja. O preço pago fica na linha: mudar o catálogo não altera cartas já compradas. Ver `docs/gamificacao.md` §6.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `usuario_id` | `uuid` | Referencia `usuarios`. Indexada |
| `codigo` | `text` | `Mais10Min`, `Mais20Min`, `Variedade`, `FimDeSemana`, `AntesDas8`, `DesafioDuplo`, `Aposta`, `PrazoExtra`, `MudancaDePlanos` |
| `preco` | `integer` | Positivo |
| `created_at` | `timestamptz` | Padrão `now()` |

## `jogadas`

Uma Provocação jogada para um colega. A situação é calculada na leitura (`docs/gamificacao.md` §6.4).

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `grupo_id` | `uuid` | Referencia `grupos` |
| `remetente_id`, `destinatario_id` | `uuid` | Referencia `usuarios`. Diferentes entre si |
| `carta_id` | `uuid` | Referencia `cartas`. Indexada |
| `carta_fusao_id` | `uuid` | Segunda carta da Fusão (ADR 0070). Nula fora da fusão. Diferente de `carta_id`; não existe com `codigo_objetivo` |
| `enviada_em`, `expira_em` | `timestamptz` | `expira_em` posterior a `enviada_em` |
| `resposta` | `text` | `Aceita` ou `Recusada`. Vazia até a resposta |
| `respondida_em` | `timestamptz` | Preenchida junto com `resposta` |
| `carta_resposta_id` | `uuid` | Única. A Resposta usada, gasta ao ser usada. Referencia `cartas` |
| `origem_id` | `uuid` | Única. Jogada original do DESAFIO DUPLO. Referencia `jogadas` |
| `codigo_objetivo` | `text` | Objetivo trocado pela MUDANÇA DE PLANOS: `Mais10Min`, `Mais20Min`, `Variedade`, `FimDeSemana`, `AntesDas8`. Nulo: vale o objetivo da carta |
| `duracao_alvo_segundos` | `integer` | Positiva. Fixada no envio para +10 MIN e +20 MIN |
| `tipo_evitar` | `text` | Fixado no envio para a VARIEDADE |
| `vista_em` | `timestamptz` | Momento em que a Jogada foi vista (`docs/gamificacao.md` §6.4) |
| `resultado_visto_em` | `timestamptz` | Momento em que o resultado foi visto (`docs/gamificacao.md` §6.4) |

## `avatares`

O avatar importado e a vitrine de Enfeites (`docs/gamificacao.md` §4.3 e §9.2). Uma linha por usuário, criada só quando a pessoa muda algo. Sem linha, vale o avatar padrão.

| Coluna | Tipo | Observações |
|---|---|---|
| `usuario_id` | `uuid` | Chave primária. Referencia `usuarios`, com `ON DELETE CASCADE` |
| `arquivo` | `text` | Nome aleatório de 32 caracteres hexadecimais e `.png`. Nulo: avatar padrão |
| `vitrine` | `text[]` | Até 3 códigos de Enfeites, na ordem em que aparecem. Padrão vazio |
| `enfeites_vistos` | `text[]` | Enfeites já vistos. Padrão vazio |
| `updated_at` | `timestamptz` | Padrão `now()` |

## `rivais`

Um Rival escolhido para uma semana do desafio em destaque (ADR 0038). O resultado é calculado na leitura.

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `grupo_id` | `uuid` | Referencia `grupos` |
| `desafio_id` | `uuid` | Referencia `desafios` |
| `semana_inicio` | `date` | Segunda-feira da semana do desafio, no fuso do grupo |
| `escolhedor_id`, `rival_id` | `uuid` | Referencia `usuarios`. Diferentes entre si |
| `created_at` | `timestamptz` | Padrão `now()` |
| `vista_em` | `timestamptz` | Quando o Rival viu a provocação |
| `resultado_visto_em` | `timestamptz` | Quando quem escolheu viu o resultado |

Única por (`grupo_id`, `escolhedor_id`, `semana_inicio`).

## `cutucadas`

Um membro chama um colega que dorme no grupo (ADR 0063, 0064).

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `grupo_id` | `uuid` | Referencia `grupos`. Indexada com o destinatário |
| `autor_id`, `destinatario_id` | `uuid` | Referencia `usuarios`. Diferentes entre si. `autor_id` indexada com a data |
| `tipo` | `text` | `Sofa`, `Frango`, `Meta`, `Halter`, `Despertador`, `Fantasma` ou `Livre` |
| `texto` | `text` | Só em `Livre` (obrigatório nesse tipo), de 1 a 80 caracteres (ADR 0064) |
| `created_at` | `timestamptz` | Padrão `now()` |

## `torcidas`

Um membro torce por quem está treinando ao vivo (ADR 0078).

| Coluna | Tipo | Observações |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `atividade_id` | `uuid` | Referencia `atividades`, com `ON DELETE CASCADE`. Indexada com a data |
| `usuario_id` | `uuid` | Referencia `usuarios` |
| `created_at` | `timestamptz` | Padrão `now()` |

## `schema_migrations`

Tabela de controle do executor de migrações. Ver `decisoes/0001-migracoes-sql.md`.
