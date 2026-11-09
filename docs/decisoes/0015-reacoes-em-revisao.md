# 0015 — Reações e comentários durante a revisão

**Status:** DECIDIDA. Altera a decisão 0012. A parte "Comentários" foi alterada pela decisão 0042.

## Contexto

Pela decisão 0012, a primeira reação negativa levava a atividade a `EmRevisao`, e nesse estado nenhuma reação era mais aceita. Com dois ou mais revisores, o limite de rejeições nunca podia ser atingido: a primeira rejeição fechava a votação antes da segunda. Na prática, só o administrador conseguia rejeitar, e a fórmula de Q012 só valia com um revisor.

## Alternativas consideradas

- **Uma negativa fecha a votação, e o administrador decide sempre:** simples, mas descarta a fórmula de rejeições e deixa atividades paradas quando o administrador não aparece.
- **Só `Suspeito` leva a `EmRevisao`, e `Rejeitar` apenas soma:** o limite funciona, mas uma rejeição isolada ficaria invisível para o administrador.
- **Manter as reações abertas em `EmRevisao` até o fim do prazo:** o limite funciona, o administrador vê a disputa desde a primeira negativa e pode decidir antes.

## Decisão

- As reações são aceitas em `AguardandoFeedback` e `EmRevisao` enquanto a janela de 48 horas estiver aberta.
- A situação é recalculada a partir das contagens a cada reação, inclusive em `EmRevisao`:
  - rejeições iguais ou acima do limite: `Rejeitada`;
  - alguma reação negativa abaixo do limite: `EmRevisao`;
  - nenhuma negativa, com todos os revisores tendo reagido: `Verificada`;
  - nenhuma negativa, com reações faltando: `AguardandoFeedback`.
- Uma reação pode ser trocada até o fim da janela, mas não removida. Trocar a única negativa encerra a revisão pelas regras acima.
- `Verificada` e `Rejeitada` são finais: novas reações recebem `REACAO_ENCERRADA`.
- Depois da janela, `EmRevisao` só muda por revisão administrativa. O administrador pode decidir a qualquer momento, mesmo com a janela aberta.
- `ReacoesResponse` passa a informar `RejeicoesNecessarias`, para a interface mostrar o limite real.

**Comentários** (substituído por `0042-comentarios-sempre-abertos.md`: comentários ficam sempre abertos)

- Uma disputa precisa de contexto: quem marcou como suspeito pode explicar, e o autor pode responder.
- Membros ativos do grupo, inclusive o autor, comentam apenas enquanto a atividade está em `EmRevisao`. Fora disso, recebem `COMENTARIOS_ENCERRADOS`.
- Depois da revisão, a conversa fica só para leitura. Comentários não são editados nem removidos, porque fazem parte da história da decisão.
- Até 500 caracteres, em `GET` e `POST /api/atividades/{id}/comentarios`, na fatia `Reacoes`. Quem não é membro recebe `ATIVIDADE_NAO_ENCONTRADA`.

## Impacto

- `ConsensoAtividade.DeterminarSituacao` e a condição de `RegistrarReacao` mudam, sem alteração de esquema.
- A migração 008 cria `comentarios_revisao`.
- A página da atividade mostra "Trocar reação" e o limite de rejeições durante a revisão.
- Uma atividade pode sair de `EmRevisao` sem decisão de um administrador, o que também a retira da fila dele.
