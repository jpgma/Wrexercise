# 0063 — Cutucada

**Status:** DECIDIDA. Revê em parte `0057-avatar-dormindo.md` (a alternativa "cutucar", vetada lá) e o princípio 4 de `gamificacao.md` §1.1. Complementa `0020-notificacoes-no-aplicativo.md`, `0049-notificacoes-push.md` e `0033-regras-de-energia-e-jogadas.md`.

## Contexto

- Num grupo pequeno, um membro que para de treinar esfria o grupo inteiro. As Provocações que ele recebe expiram, e por isso ninguém ganha a recompensa. A praça mostra o avatar dele dormindo, mas não dá a ninguém um jeito de chamá-lo.
- A ADR 0057 descartou a "cutucada" porque o princípio 4 proíbe lembretes. Esse princípio protege contra o aplicativo que cobra: o aviso automático, agendado, que ninguém mandou.
- Os grupos são de amigos próximos. Uma zoeira de um amigo, mandada por ele, de propósito, não é o aplicativo cobrando. É o grupo chamando.

## Alternativas consideradas

- **Lembrete automático para quem dorme:** continua vetado pelo princípio 4.
- **Cutucada com texto livre:** mais expressiva, mas exige moderação e abre espaço para mensagens pesadas.
- **Cutucada a qualquer membro:** vira ruído entre quem já está ativo.
- **Cutucada paga com Energia:** pune quem está tentando ajudar.
- **Cutucada grátis, só para quem dorme, com mensagens prontas, e um bônus para quem acorda alguém:** escolhida.

## Decisão

- **Quem cutuca e quem recebe:** um membro cutuca um colega do mesmo grupo que está `dormindo` ali (regra da 0057, em `MembrosDormindo`). Ninguém cutuca a si mesmo, e cutucar não custa nada.
- **Limite:** uma cutucada por par (quem cutuca e quem recebe, no mesmo grupo) a cada 24 horas: `409 CUTUCADA_RECENTE`. Quem está acordado não pode ser cutucado: `409 MEMBRO_ACORDADO`.
- **Mensagens prontas** (`TiposCutucada`), sem texto livre:

  | Código | Emoji | Texto |
  |---|---|---|
  | `Sofa` | 🛋️ | Levanta desse sofá! |
  | `Frango` | 🐔 | Tá com medo do treino? |
  | `Meta` | 📉 | Tua meta tá chorando. |
  | `Halter` | 🏋️ | O halter sente sua falta. |
  | `Despertador` | ⏰ | Acorda, dorminhoco! |
  | `Fantasma` | 👻 | Sumiu, foi? |

- **Notificação:** `Cutucada`, com push (0049). Texto: "Ana te cutucou 👉 🛋️ Levanta desse sofá!". O toque leva à praça do grupo. Chega ao colega no modo App ou Jogo.
- **Onde se cutuca:** no Foco de quem dorme (modo Jogo), pelo botão "👉 Cutucar Nome", que abre as 6 mensagens. Tocar numa envia. Depois do envio, quem dorme faz `acordar` (o susto) e volta a dormir, e o painel mostra "Você já cutucou Nome hoje." até passarem 24 horas.
- **Na praça:** quem dorme e recebeu cutucadas nas últimas 24 horas mostra uma bolha com o emoji da última e "×N" quando há mais de uma. Todos veem quem está sendo chamado.
- **Bônus Despertador:** quem cutucou ganha **5 de Energia** quando quem foi cutucado volta com uma atividade que cumpre três condições:
  - é do mesmo grupo e está `Verificada`;
  - foi registrada depois da cutucada;
  - começou até 24 horas depois dela.

  Cada cutucada paga pela primeira atividade, em ordem de registro, que cumpre essas condições. Quem cutucou várias vezes ganha uma vez por atividade, e cada colega que cutucou ganha a sua parte. O bônus é calculado na leitura (`gamificacao.md` §5.3) e só cresce, porque `Verificada` é final.

## Impacto

- **Migração 035:**
  - Cria a tabela `cutucadas`.
  - Acrescenta `notificacoes.cutucada_id`.
  - Acrescenta o tipo `Cutucada` às restrições de `notificacoes`.
- `POST /api/grupos/{grupoId}/cutucadas` na fatia `Jogo`. `GET /api/grupos/{id}/cena` ganha, por membro, `cutucadas` (total e último tipo nas últimas 24 horas) e `cutucadoPorMimEm`.
- `EnergiaGanha` e o detalhamento da Energia ganham `Despertador`.
- O princípio 4 de `gamificacao.md` passa a distinguir lembrete automático (proibido) de cutucada entre amigos (permitida).
- **Risco aceito:** dois amigos podem combinar "dormir, cutucar, treinar" para render 5 de Energia. O ganho é pequeno, e o membro precisa ficar 48 horas sem treinar no grupo.
