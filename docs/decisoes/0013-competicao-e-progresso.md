# 0013 — Competição, classificação e resumo

**Status:** DECIDIDA. Parcialmente substituída pela decisão 0016 (adesão tardia e SQL de contagens) e pela 0090 (ordem de desempate, sequência e posição dividida).

## Contexto

A Fase 7 precisa de classificação, histórico semanal, sequências de metas cumpridas e resumo na Início. `arquitetura.md` §8.5 define critérios de ranking; §10.3 prevê `GET /api/desafios/{id}/classificacao`. A decisão 0009 deixou para esta fase como tratar adesão tardia na classificação. O progresso da semana atual já existe em `GET /api/desafios/{id}/progresso` (Fase 4–5).

## Alternativas consideradas

**Adesão tardia**

- **Excluir semanas anteriores a `joined_at`:** mais justo para quem entra depois, mas diverge de “tratado como os demais” na 0009.
- **Contar semanas anteriores como não cumpridas:** todos competem sobre o mesmo calendário do desafio; quem adere tarde começa com zeros nas semanas passadas.
- **Ranking proporcional (% de semanas elegíveis):** não está em §8.5.

**SQL de contagens**

- **Duplicar inline em Desafios e Progresso:** risco de divergência.
- **Fragmento canônico em `Infrastructure/Database/ContagensAtividadeDesafio.cs`:** uma fonte, sem repositório genérico; ambas as fatias importam Infrastructure.
- **Mover `ObterProgresso` para Progresso:** concentra competição, mas desloca endpoint já estável da Fase 4.

**Resumo na Início (vários grupos)**

- **Só o primeiro grupo:** simples, mas ignora multi-grupo (0008).
- **Card enriquecido por grupo:** desafio em destaque, progresso pessoal da semana e mini-classificação.

## Decisão

**Adesão tardia (0009)**

- Revista pela decisão 0016: as semanas anteriores à primeira semana inteira do membro não contam e aparecem como "não participou". Antes, quem aderia tarde era tratado como os demais no calendário, com essas semanas como não cumpridas.

**Classificação (§8.5)**

Ordem de desempate (revista pela 0090, que acrescenta o tempo e as atividades verificadas e troca o nome pela posição dividida):

1. Semanas com meta cumprida (verificadas ≥ meta da semana).
2. Total de sessões verificadas no desafio.
3. Sequência atual: semanas consecutivas cumpridas, da semana mais recente para trás.
4. ~~`nome_exibicao`, depois `usuario_id`.~~ Ver 0090.

A semana corrente entra nas contagens como as demais (meta só com verificadas).

**Endpoints**

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/desafios/{id}/classificacao` | Ranking e estatísticas por participante |
| GET | `/api/desafios/{id}/historico` | Histórico semanal do usuário autenticado |
| GET | `/api/grupos/{grupoId}/resumo` | Desafio em destaque, progresso da semana do usuário, classificação (top 3 até a ADR 0066, que passou a trazer todos) |

Permissões: qualquer membro ativo do grupo (404 se não membro ou desafio/grupo inexistente, como nas rotas de desafio).

**Desafio em destaque no resumo**

- Primeiro desafio **não cancelado** na ordenação de listagem (`Ativo`, depois `Agendado`, depois `Encerrado`; início mais recente dentro de cada faixa).
- Se não houver nenhum (só cancelados ou grupo sem desafio), resposta com campos de desafio vazios.

**SQL de contagens por desafio**

Fonte canônica: `Wrexercise/Infrastructure/Database/ContagensAtividadeDesafio.cs`.

Atividades elegíveis:

- Mesmo `grupo_id` do desafio.
- Autor membro ativo (`membros_grupo.removed_at IS NULL`), em semana que começou no dia da entrada dele ou depois (decisão 0016).
- `duracao_segundos >= duracao_minima_segundos`.
- Data local (`started_at AT TIME ZONE fuso_horario`) dentro do período do desafio e, quando aplicável, do intervalo da semana.

Contagens:

- **Verificadas:** `situacao_verificacao = 'Verificada'`.
- **Pendentes (provisório):** `AguardandoFeedback`, `EmRevisao`, `Contestada`.

Índice de semana (alinhado a `CalculoSemanaDesafio`):

```text
indice_semana = ((started_at AT TIME ZONE fuso)::date - data_inicio) / 7
```

Query agregada padrão: `GROUP BY usuario_id, indice_semana`. Progresso da semana atual filtra `indice_semana` da semana corrente ou restringe datas da semana.

Os totais de desempate da 0090 (tempo e quantidade das atividades `Verificada`) usam o mesmo `FromElegiveisSql`, em `ContagensDesafio.LoadTotaisVerificadosAsync`.

Alterações neste SQL exigem atualizar este ADR e os testes de progresso/classificação.

## Impacto

- Nova fatia `Features/Progresso/` e `webapp/src/features/progresso/`.
- Contrato `Features/Desafios/Contracts/CalculoSemanaDesafio.cs` para semanas sem acoplar fatias.
- `ObterProgresso` passa a usar `ContagensAtividadeDesafio`.
- `arquitetura.md` §10 atualizado; Q033 parcialmente endereçada (visualização de progresso e classificação).
