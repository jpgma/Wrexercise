# 0042 — Comentários sempre abertos

**Status:** DECIDIDA. Altera a parte "Comentários" de `0015-reacoes-em-revisao.md` e os destinatários de `Comentario` em `0020-notificacoes-no-aplicativo.md`.

## Contexto

Pela decisão 0015, só se comentava uma atividade em `EmRevisao`. A conversa existia apenas para explicar uma disputa. No dia a dia, o grupo quer comentar qualquer treino: dar parabéns, perguntar como foi, combinar o próximo. Sem isso, a única forma de interagir era a reação, e 💪 `Kudos` não diz nada além de "valeu".

## Alternativas consideradas

- **Manter os comentários só na revisão:** simples, mas não atende o pedido.
- **Comentar só enquanto a janela de 48 horas está aberta:** limita a conversa sem motivo claro, e acrescenta outra regra a explicar.
- **Comentários sempre abertos, para qualquer membro ativo do grupo:** a regra mais simples, e a conversa da revisão continua no mesmo lugar.

## Decisão

- Membros ativos do grupo, inclusive o autor, comentam qualquer atividade, em qualquer situação de verificação e a qualquer momento. `COMENTARIOS_ENCERRADOS` deixa de existir.
- Comentários continuam sem edição e sem remoção, porque os de uma disputa fazem parte da história da decisão.
- Continua o limite de 500 caracteres. `POST /api/atividades/{id}/comentarios` passa a ter limite de 20 envios por minuto por IP (política `comentarios`), porque comentar fica bem mais frequente.
- A notificação `Comentario` vai para o autor e para quem já comentou, menos quem escreveu. Os administradores deixam de recebê-la só pelo papel: com conversa em toda atividade, o aviso viraria ruído. Eles continuam recebendo `Disputa`.
- A tabela `comentarios_revisao` vira `comentarios` (migração 020), e o código troca `ComentariosRevisao` por `Comentarios`.
- O resumo da atividade (`AtividadeResumoResponse`) ganha `Comentarios`, o total de comentários, e o feed mostra 💬 com o total quando ele é maior que zero.

## Impacto

- Sem mudança de colunas; a migração 020 só renomeia a tabela e o índice.
- A consulta do resumo ganha mais uma subconsulta de contagem, coberta pelo índice `comentarios_atividade_id`.
- Sem a checagem de situação, `CriarAsync` não precisa mais de `FOR UPDATE` na atividade.
- A seção da página da atividade se chama "Comentários" e sempre mostra o campo de texto.
