# 0034 — Ordem e interpretação dos bônus de Energia

**Status:** DECIDIDA. Complementa `0033-regras-de-energia-e-jogadas.md`. Detalhes em `../gamificacao.md` §5.2 e §5.3.

## Contexto

Ao implementar a G2, três pontos de §5.2 e §5.3 de `gamificacao.md` ficaram ambíguos ou contradiziam a garantia de que a Energia ganha só cresce:

- Os qualificadores de "primeira" (da semana, num dia novo, tipo novo) eram atribuídos em ordem de `started_at`. Como uma atividade pode ser registrada com data passada (`ValidacaoAtividade.IsValidStartedAt` só barra o futuro), uma atividade retroativa podia tomar o bônus de outra já `Verificada`. A Energia ganha diminuiria, e na G3 o saldo poderia ficar negativo depois de uma compra.
- "Os bônus de horário contam no máximo uma vez por pessoa por dia" não dizia se o limite valia para cada qualificador ou para todos juntos.
- "Primeiro tipo de atividade novo na semana" não dizia se a primeira atividade da semana já conta como tipo novo.

## Alternativas consideradas

**Ordem dos qualificadores**

- **`started_at`:** é a ordem intuitiva, mas quebra a garantia de crescimento e exige tratar saldo negativo.
- **Proibir registro retroativo:** muda uma regra das atividades por causa de um jogo opcional.
- **Ordem de registro (`created_at`, `id`):** uma atividade nova sempre entra no fim. O bônus pode cair numa atividade diferente da que a pessoa esperaria, mas o total fica parecido.

**Bônus de horário**

- **Cada qualificador uma vez por dia:** um treino às 05:30 e outro às 07:00 renderiam 5 + 3.
- **Todos juntos, uma vez por dia:** só a primeira atividade do dia ganharia algum bônus de horário.
- **Por grupo de bônus:** o bônus de madrugada (5 ou 3) paga uma vez por dia, e o de fim de semana outra vez por dia.

**Tipo novo**

- **Todo tipo inédito, inclusive o primeiro:** a primeira atividade da semana quase sempre bateria o teto.
- **Só a partir do 2º tipo:** premia a variedade de fato.

## Decisão

- Os qualificadores de "primeira" e os bônus de horário são atribuídos na **ordem de registro** (`created_at`, `id`), entre as atividades não rejeitadas. Uma atividade pendente já ocupa a sua posição. Se for rejeitada, a seguinte pode ganhar o bônus.
- O **bônus de madrugada** (5 antes das 06:00, 3 entre 06:00 e 08:00) paga uma vez por pessoa por dia. O **bônus de fim de semana** (3) também paga uma vez por pessoa por dia, de forma independente.
- O **tipo novo** (+5) vale a partir do 2º tipo da semana. O tipo da primeira atividade da semana só é registrado.

## Impacto

- `gamificacao.md` §5.2 e §5.3 foram atualizados.
- `Features/Jogo/CalculoEnergia.cs` recebe as atividades já em ordem de registro, e `ObterEnergia.cs` ordena por `created_at, id`.
- Com essas regras, uma semana típica (meta de 3, um treino cedo, dois tipos, um no fim de semana) rende 73 EN das fontes da G2. Missões, Rival e Jogadas completam o alvo de 90 a 110 EN.
