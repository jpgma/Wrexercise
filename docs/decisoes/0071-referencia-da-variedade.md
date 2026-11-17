# 0071 — Treino de referência da VARIEDADE

**Status:** DECIDIDA. Muda o treino de referência da VARIEDADE definido em `0033` e no item 2 de `0035`. O tipo a evitar continua fixado no envio. As Respostas de `0036` e a Fusão de `0070` seguem a mesma regra.

## Contexto

- A VARIEDADE pede "um treino de outro tipo". O tipo a evitar era o da última atividade `Verificada` de quem recebe, no grupo da Jogada.
- Uma atividade só fica `Verificada` depois que os revisores reagem. Enquanto isso, quem joga vê o treino novo do colega no feed, dá moral nele e arremessa a VARIEDADE pensando nesse treino. A carta, porém, mirava num treino mais antigo.
- Exemplo: a pessoa pedalou ontem (verificada) e correu hoje (ainda aguardando). Quem deu moral na corrida e jogou a VARIEDADE esperava "um treino que não seja Corrida", e a carta dizia "que não seja Ciclismo".
- O pedido do mantenedor: se quem joga aprovou o último treino do rival e ele ainda não foi aprovado por todos, a carta deve valer contra esse treino.

## Alternativas consideradas

- **Só a última `Verificada`** (como era): simples, mas contraria o que quem joga acabou de ver.
- **Qualquer treino pendente mais recente**, com ou sem moral de quem joga: a carta poderia mirar num treino que quem joga nem viu, ou num registro errado que o grupo ainda vai rejeitar.
- **O treino com moral pendente sempre vence**, mesmo com uma `Verificada` mais nova: a carta poderia mirar num treino antigo, parado em revisão, em vez do último.
- **Só `AguardandoFeedback`**, sem `EmRevisao` e `Contestada`: mais uma exceção para explicar. Quem deu moral já disse que aceita o treino.
- **A mais recente entre a `Verificada` e a que quem joga aprovou** (escolhida).

## Decisão

1. O treino de referência é o que **começou por último** (`started_at`), no grupo da Jogada, entre os treinos de quem vai cumprir que:
   - estão `Verificada`; ou
   - receberam `Kudos` de quem joga a carta e não estão `Rejeitada` nem `Anulada` (valem `AguardandoFeedback`, `EmRevisao` e `Contestada`).
2. O tipo desse treino é gravado em `jogadas.tipo_evitar` **no envio**, como antes. Se o treino for rejeitado ou anulado depois, o tipo gravado não muda.
3. Sem treino de referência, `tipo_evitar` fica nulo e qualquer tipo vale.
4. **Fusão:** a mesma regra, com quem joga a Fusão.
5. **DESAFIO DUPLO:** quem devolve é quem joga. Vale o `Kudos` dessa pessoa nos treinos de quem enviou a carta original.
6. **MUDANÇA DE PLANOS** para VARIEDADE: quem responde é, ao mesmo tempo, quem escolhe e quem vai cumprir. A pessoa conhece os próprios treinos, então vale o último treino dela que não esteja `Rejeitada` nem `Anulada`.

## Impacto

- `Features/Jogo/Jogadas.cs`: `LoadTipoEvitarAsync` substitui `LoadUltimoTipoVerificadoAsync` e recebe o observador (quem joga). `FixarObjetivoAsync` repassa o observador. A consulta lê `reacoes_atividade` com SQL explícito; não há contrato novo entre fatias.
- `JogarCarta` e `ResponderJogada` informam o observador.
- Sem migração: `jogadas.tipo_evitar` já existe, e as Jogadas abertas mantêm o tipo gravado.
- `gamificacao.md` §6.1 e `funcionalidades.md` §3.3.4 foram atualizados.
- Testes de integração em `JogadasRoutesTests` cobrem cada caso da decisão.
