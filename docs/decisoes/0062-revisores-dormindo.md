# 0062 — Quem dorme não revisa

**Status:** DECIDIDA. Complementa `0012-verificacao-comunitaria.md` e usa a regra de `0057-avatar-dormindo.md`.

## Contexto

- A lista de revisores de uma atividade é fixada no registro: todos os membros ativos do grupo, menos o autor (ADR 0012).
- A atividade só vira `Verificada` antes do fim da janela de 48 horas quando **todos** os revisores dão moral.
- Num grupo pequeno, basta um membro que sumiu para que toda atividade dos outros espere as 48 horas inteiras. Isso atrasa a Energia, as cartas e a sensação de progresso de quem está treinando.
- O aplicativo já sabe quem sumiu: na praça, o membro sem atividade válida no grupo nas últimas 48 horas está `dormindo` (ADR 0057).

## Alternativas consideradas

- **Encurtar a janela:** atrasa menos, mas também tira tempo de quem só avalia à noite.
- **Lista de revisores dinâmica (quem dorme pode reagir, mas ninguém espera por ele):** mais justa, mas a lista deixa de ser fixa, e a regra de consenso e a fila de avaliação passam a depender do relógio.
- **Excluir quem dorme no registro da atividade:** escolhida. É uma condição a mais no `INSERT` dos revisores.

## Decisão

- **Regra:** ao registrar uma atividade, só os membros acordados entram como revisores. Acordado é quem tem atividade no grupo que começou nas últimas 48 horas (sem contar `Rejeitada` e `Anulada`) ou entrou no grupo há menos de 48 horas. É a mesma regra da pose `dormindo`, num só lugar: `Infrastructure/Database/MembrosDormindo.cs`.
- **Quem dorme só vê:** não é revisor, então não reage (`NAO_REVISOR`) e a atividade não entra na fila dele. Ao voltar a treinar, ele registra sem fila para limpar antes (ADR 0041).
- **Todos os outros dormem:** a atividade fica sem revisores e é `Verificada` na hora, como num grupo de uma pessoa (sem evidência no registro, com evidência ao anexar a primeira).
- **A lista continua fixa:** quem acorda depois não entra nas atividades já registradas. As atividades pendentes antes desta decisão mantêm a lista que tinham.

## Impacto

- `CriarAtividade` filtra os revisores com `MembrosDormindo.AcordadoSql`. `ObterCena` e `PoseAvatar` usam a mesma regra.
- **Risco aceito:** quando todos os outros dormem, quem treina se autoverifica. Num grupo de amigos, isso é melhor do que travar quem está ativo. O administrador ainda pode anular um registro errado (ADR 0054).
- Nada muda no consenso (`ConsensoAtividade`), na janela de 48 horas nem no cálculo de rejeições, que usa o total de revisores da lista.
