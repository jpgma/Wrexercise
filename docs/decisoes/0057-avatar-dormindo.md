# 0057 — Avatar dormindo: 48 horas sem atividade

**Status:** DECIDIDA. Complementa `0023-gamificacao.md`, `0039-avatar-em-8-direcoes.md` e `0037-avatar-importado-e-enfeites.md`.

## Contexto

Na praça do modo Jogo (`gamificacao.md` §3), quem não treina fica `parado`, igual a quem treinou ontem. Faltava um jeito leve e engraçado de mostrar que alguém sumiu, sem cobrança: o princípio 4 de `gamificacao.md` §1.1 proíbe lembretes, e o princípio 3 proíbe prejudicar alguém.

## Alternativas consideradas

- **Cochilo em pé:** a cabeça cai e volta. Mais fácil de gerar e sem mudar a âncora, mas pouco legível: parece `parado`.
- **Deitado no chão:** o mais óbvio, mas difícil de manter consistente nas 8 direções e invade o espaço dos vizinhos na elipse.
- **Dois estágios (cochilo com 48 horas, sono profundo depois de uma semana):** mais expressivo, com o dobro da arte e mais uma regra.
- **Notificação ao colega ("cutucar"):** é lembrete, vetado pelo princípio 4.
- **Sentado no chão, cabeça caída:** escolhida. Legível, cabe na célula de 48 px e mantém os pés na âncora.

## Decisão

- **Regra:** a pose é `dormindo` quando o membro não tem atividade **neste grupo** que comece nas últimas 48 horas, sem contar as `Rejeitada` e as `Anulada` (as pendentes contam). Quem entrou no grupo há menos de 48 horas não dorme. A regra vale com ou sem desafio.
- **Prioridade:** `treinando` vence `comemorando`, que vence `dormindo`, que vence `parado`. Quem já bateu a meta da semana continua comemorando.
- **Resposta:** `GET /api/grupos/{id}/cena` traz `dormindoDesde` (a última atividade válida ou a entrada no grupo) só para quem dorme. O Foco mostra "Dormindo há N dias".
- **Arte:** 4 animações novas, no fim da folha em 8 direções, que passa de 97 para 129 linhas (432×6192): `dormindo` (repete), `adormecer`, `acordar` (susto) e `levantar`. A folha de 97 linhas continua aceita na importação, sem migração; nela, quem dorme aparece num quadro fixo de `parado`, com o "Zzz". Os prompts ficam em `pixel-art/base-masculino/prompts-gemini.md` §4, "Dormir".
- **Cena:**
  - Ao abrir a praça, quem dorme faz `adormecer` e passa a `dormindo`, com um "Zzz" e, de vez em quando, uma bolha de sonho com um halter ou um tênis (`ambiente.png`, linhas 5 e 6).
  - Tocar em quem dorme abre o Foco sem giro: ele toma um susto (`acordar`) e volta a dormir.
  - Atingido por uma carta, quem dorme faz `acordar` no lugar de `receber`. Para andar, arremessar, aceitar, recusar, provocar ou comemorar, quem dorme faz `levantar` antes e `adormecer` depois.
  - Quem dormia na última visita de quem vê e agora está acordado faz `levantar` uma vez: é uma encenação e conta no limite de 3. A lista fica no cookie comum `wx_dormindo_<grupo>`, com o começo de cada identificador.
  - O pombo prefere pousar ao lado de quem dorme. Quem dorme não conversa nem pega a bola.
  - Cada um vê também o próprio avatar dormindo, sem notificação.

## Impacto

- `ObterCena` ganha uma subconsulta por membro (o índice `atividades_usuario_started` serve).
- A validação da folha (`ValidacaoFolhaAvatar`) aceita as duas alturas. O navegador mede a altura de cada folha e escala o fundo só pela largura, que é a mesma nos dois formatos.
- Nada muda na verificação, nas metas, na classificação nem na Energia.
