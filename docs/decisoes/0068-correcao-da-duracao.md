# 0068 — O administrador corrige a duração de uma atividade

**Status:** DECIDIDA. Complementa `0054-correcao-de-atividade.md` e reabre um ponto dela: a correção passa a valer também depois de a atividade ser verificada, mas só para a duração.

## Contexto

- A 0054 deixou duas saídas para um registro errado: o autor apaga nos primeiros 15 minutos, ou o administrador anula antes da decisão. Depois de `Verificada`, não havia nada.
- Em 13/11/2026, um participante registrou 3 h num treino de 1 h 41 min. A atividade foi verificada, porque as evidências eram reais e só a duração estava errada. A correção foi feita com SQL direto na produção.
- Anular não serviria: o treino aconteceu e o dia deve continuar contando. O que estava errado era um número.
- A 0054 descartou a edição por três motivos: formulário de edição, recálculo dos avisos e reações já dadas. E limitou a correção ao período antes da decisão porque a Energia só cresce enquanto `Verificada` é final.

## Alternativas consideradas

- **Continuar com SQL na produção:** sem código, mas sem registro, sem regra de quem pode e dependente do mantenedor.
- **Edição completa da atividade (tipo, início, descrição, duração):** o início muda o dia, a semana, os bônus de horário e a janela dos avisos. É a alternativa que a 0054 já recusou.
- **O autor corrige a própria duração:** quem recebeu `Kudos` por 3 h poderia registrar pouco, esperar a verificação e aumentar depois.
- **Anular e pedir novo registro:** o limite de 48 horas no passado já pode ter vencido, e a mesma foto daria `EVIDENCIA_DUPLICADA`.
- **Registrar a correção em `historico_revisao`:** a tabela guarda decisões sobre a situação, com `decisao` num CHECK. Faltariam as duas durações.
- **Notificar o autor:** pede um tipo novo de notificação, com migração e textos de push. A correção já aparece na página da atividade, com o nome de quem corrigiu. Fica fora desta decisão (ver 0048).
- **Só a duração, pelo administrador, com registro visível ao grupo:** escolhida.

## Decisão

- `PUT /api/atividades/{id}/duracao`, com `duracaoSegundos` (os mesmos limites do registro: 60 a 86400) e `justificativa` obrigatória (até 500 caracteres). Devolve a atividade atualizada.
- **Quem:** o proprietário e os administradores do grupo. Outro membro recebe `403 ACESSO_NEGADO`.
- **Nunca a própria atividade** (como em 0052 e 0054): `403 CORRECAO_PROPRIA_NAO_PERMITIDA`. O autor pede a outro administrador.
- **Quando:** em `AguardandoFeedback`, `EmRevisao`, `Contestada` e `Verificada`, sem prazo. `Rejeitada` e `Anulada` não contam para nada: `409 CORRECAO_NAO_PERMITIDA`.
- A mesma duração de antes é `400 DADOS_INVALIDOS`.
- **Só a duração muda.** A situação, as reações, os comentários, os revisores e a janela de avaliação ficam como estavam.
- **Registro:** cada correção fica em `correcoes_duracao`, com quem corrigiu, a duração de antes, a de depois, a justificativa e a data. Ninguém a edita nem apaga.
- **Transparência:** `GET /api/atividades/{id}` devolve `correcoes`, e a página da atividade mostra cada uma a todo o grupo: "Bia corrigiu a duração de 3h para 1h 41min", com a justificativa. Devolve também `correcaoPermitida`, que decide se o botão "Corrigir duração" aparece.

### O que a correção não refaz

- **Avisos (0052):** `FotoForaDoPeriodo` e `RegistroTardio` foram calculados com a duração do registro e não são recalculados. Eles descrevem o registro como foi feito.
- **Notificações já enviadas**, inclusive a de Jogada concluída, não são recolhidas nem reenviadas.

### Energia

- A Energia é calculada na leitura, então a correção vale na hora para a meta do dia, o progresso, a Classificacao, as Jogadas, o Rival e o perfil.
- A duração não entra direto na Energia, mas decide se o dia alcança a meta. Se a correção tira um dia da meta, a Energia que dependia dele (meta da semana, sequência, desafio, Jogada, Rival) sai.
- Por isso a regra "a Energia ganha só cresce" (0023, 0034) ganha uma exceção: a correção por um administrador. O saldo pode ficar negativo se a pessoa já gastou essa Energia. Nada é desfeito na Loja: a pessoa só não compra enquanto o saldo não voltar.
- É o resultado honesto: aquela Energia veio de um número errado. O caso é raro e depende de um administrador, com justificativa à vista do grupo.

## Impacto

- Migração `038-create-correcoes-duracao.sql`: a tabela `correcoes_duracao`.
- Fatia `Atividades`: `CorrigirDuracao.cs`, a regra pura `CorrecaoDuracao` e os campos `correcaoPermitida` e `correcoes` em `AtividadeResponse`.
- Frontend: `CorrigirDuracao` na página da atividade.
- Novo conceito no glossário: `Correcao`.
- `funcionalidades.md` (§7.7 e a lista do que não existe), `modelo-dados.md`, `arquitetura.md` e `gamificacao.md` §5.3 atualizados.
