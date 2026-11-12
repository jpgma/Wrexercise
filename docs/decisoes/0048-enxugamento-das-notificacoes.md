# 0048 — Enxugamento das notificações

**Status:** DECIDIDA. Altera os eventos de `0020-notificacoes-no-aplicativo.md` e o aviso de Jogada concluída de `0035-cartas-e-jogadas.md`.

## Contexto

Com as notificações push a caminho (`0049-notificacoes-push.md`), cada aviso passa a poder tocar o telefone. Dois avisos só confirmam o caminho esperado e não pedem nenhuma ação:

- "Seu treino foi verificado": quase todo treino chega a `Verificada` quando a janela de 48 horas fecha.
- "Você cumpriu uma carta": quem cumpriu sabe que treinou.

## Alternativas consideradas

- **Manter tudo e escolher só o que vira push.** A lista continuaria cheia de avisos que ninguém abre, e cobriria os que importam.
- **Preferências por tipo.** A `0020` decidiu não ter preferências, e um grupo pequeno não precisa delas.
- **Apagar também `CartaAceita` e `CartaRecusada`.** O remetente quer saber o destino da carta; elas ficam na lista, mas sem push.

## Decisão

- `SituacaoAlterada` só é gerada para `Rejeitada` e `EmRevisao`. Ficar `Verificada`, pela janela ou por decisão administrativa, não avisa ninguém.
- `JogadaConcluida` só vai para o remetente, com o destinatário como ator.
- A migração 024 apaga as linhas antigas desses dois casos.

## Impacto

- O texto "Seu treino foi verificado" e o "Você cumpriu uma carta" saem do frontend.
- Testes de integração passam a verificar a ausência desses avisos.
