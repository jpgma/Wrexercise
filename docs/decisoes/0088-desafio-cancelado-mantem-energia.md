# 0088 — O desafio cancelado mantém a Energia

**Status:** DECIDIDA. Muda o cálculo da Energia das semanas e das sequências (`0033`, `0034`) e as conquistas de sequência: o desafio cancelado entra até o dia do cancelamento. O desafio concluído, a classificação e o progresso oficial do desafio continuam os mesmos. Detalhes em `../gamificacao.md` §5.

## Contexto

- A Energia é calculada na leitura. `DesafiosDaPessoa.LoadAsync` ignorava os desafios cancelados (`cancelled_at IS NULL`).
- Quando o proprietário cancelava um desafio, a Energia das semanas cumpridas nele (20 EN por semana) e os marcos de sequência (25, 50 e 100 EN) sumiam do saldo de todos, junto com as conquistas de sequência. Quem treinou perdia o que já tinha ganhado por uma decisão de outra pessoa.
- O mantenedor pediu que quem cumpriu semanas num desafio cancelado não perca a Energia ganha nele.

## Alternativas consideradas

- **Só as semanas inteiras antes do cancelamento:** a semana do cancelamento, já cumprida, se perderia. Descartada.
- **Ignorar o cancelamento até o fim previsto:** a pessoa ganharia Energia de um desafio que não existe mais. Descartada.
- **Manter só a Energia das semanas, sem as sequências e as conquistas:** a sequência também foi ganha antes do cancelamento. Descartada.
- **Gravar a Energia no cancelamento:** pediria um livro-razão, que o jogo evita. Descartada: o cálculo na leitura só passa a considerar o dia do cancelamento.

## Decisão

- **Até o cancelamento:** um desafio cancelado depois de começar entra no cálculo até o dia do cancelamento, no fuso do grupo. Contam as semanas cumpridas com atividades verificadas desses dias, inclusive a semana do cancelamento, se já cumprida. As atividades depois do cancelamento não contam para ele.
- **Sequências:** os marcos de Energia e as conquistas de sequência contam as semanas do desafio cancelado.
- **Desafio concluído:** o cancelado nunca conta (`DesafioDaPessoa.IsConcluido`), nem os 75 EN, nem a conquista.
- **Cancelado antes de começar:** não entra.
- **Sem pagar duas vezes:** a semana de um desafio cancelado só paga se não cruza uma semana já paga (início a menos de 7 dias). Um desafio criado logo depois, começando em outro dia da semana, não paga os mesmos dias de novo. As semanas dos desafios não cancelados contam como antes.
- **Transição:** vale para todos os desafios já cancelados. A Energia e as conquistas que tinham sumido voltam na implantação.
- **Fora do escopo:** o Rival vencido num desafio cancelado continua sem render (`questoes-abertas.md`).

## Impacto

- **Economia:** só acrescenta Energia: nenhum saldo diminui.
- **Backend:** `DesafiosDaPessoa.LoadAsync` (SQL sem o filtro do cancelamento, com o dia do cancelamento), `DesafioDaPessoa.Cancelado`, `CalculoEnergia.CountSemanasCumpridas`. Sem migração.
- **Frontend:** nenhuma mudança.
