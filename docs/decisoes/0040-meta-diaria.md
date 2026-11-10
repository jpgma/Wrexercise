# 0040 — Meta diária

**Status:** DECIDIDA. Substitui a regra "uma atividade elegível = uma sessão" de `0011-atividades.md` e `0013-competicao-e-progresso.md`, a base de Energia por atividade de `0033-regras-de-energia-e-jogadas.md` e o objetivo de uma atividade só em +10 MIN e +20 MIN de `0035-cartas-e-jogadas.md`. Ajusta o critério de `0038-rival-da-semana.md`. Complementada por `0043-metas-por-tipo.md` (metas por tipo como alternativas ao mínimo do dia).

## Contexto

A meta de um desafio era "N sessões por semana, cada **atividade** com pelo menos X minutos". Quem treinava 30 minutos de manhã e 30 à noite, com mínimo de 60, não pontuava, embora tivesse feito a hora do dia. O grupo decidiu que o mínimo é **por dia**.

Isso afeta o progresso, a classificação, as sequências e o Jogo: a Energia pagava 10 EN por atividade verificada (dividir um treino em seis passaria a render seis vezes a base), as cartas +10 MIN e +20 MIN pediam uma atividade só, e o Rival decidia pelo `verified_at` da N-ésima atividade que contava.

## Alternativas consideradas

- **Quantas sessões um dia rende:** no máximo uma; ou `floor(total / mínimo)`, o que deixaria cumprir a semana num dia só.
- **Quais desafios:** todos, inclusive os passados, porque o progresso é calculado na leitura; ou só os novos, com uma coluna dizendo a regra de cada desafio e dois caminhos de SQL.
- **Energia:** base por dia com atividade verificada; limite de atividades com Energia por dia; ou manter por atividade.
- **+10 MIN e +20 MIN:** soma do dia; ou continuar exigindo uma atividade só.

## Decisão

1. **As atividades de um dia se somam.** O dia é a data de início de cada atividade no fuso do grupo; uma atividade que passa da meia-noite conta só no dia em que começou. Um dia conta como **uma** sessão quando a soma alcança `duracao_minima_segundos`, nunca mais de uma. `sessoes_por_semana` passa a significar dias por semana. Sem migração e sem renomear colunas.
2. **Definitivo e provisório por dia.** O dia é definitivo quando a soma das atividades `Verificada` alcança o mínimo; provisório quando só alcança somando as pendentes (`AguardandoFeedback`, `EmRevisao`, `Contestada`). As contagens `verificadas` e `pendentes` continuam com esses nomes, mas contam dias. O SQL canônico fica em `Infrastructure/Database/ContagensAtividadeDesafio.cs`.
3. **Vale para todos os desafios**, inclusive os encerrados. Como tudo é calculado na leitura, semanas passadas só podem ganhar cumprimentos.
4. **Energia:** a base de 10 EN é por dia com atividade verificada em cada grupo. Para que a Energia ganha nunca diminua, as atividades que começaram antes de 9/11/2026 (`CalculoEnergia.InicioEnergiaPorDia`) continuam pagando 10 EN cada. Os bônus não mudam.
5. **+10 MIN e +20 MIN:** somam-se as atividades de um mesmo dia que começaram dentro do prazo da Jogada. A Jogada fica `Concluida` quando a soma das verificadas de um dia alcança a duração-alvo. O aviso `JogadaConcluida` sai para a atividade que completou a soma.
6. **Rival:** cada dia conta no `verified_at` da atividade verificada que levou a soma do dia ao mínimo; a meta, no N-ésimo dia a contar.
7. **Medalhas `Treinos10/50/100`:** contam dias com atividade verificada em cada grupo, não atividades. Com o mesmo corte da Energia (`ConquistasUsuario.InicioTreinosPorDia`), as atividades anteriores contam uma cada, para que ninguém perca uma medalha já na Vitrine. O total de treinos do Perfil continua contando atividades.
8. **Interface:** o formulário de registro soma a duração às outras atividades do mesmo dia na semana atual (`seusSegundosPorDia` no progresso) e diz quanto falta no dia. O feed deixa de marcar atividades "abaixo do mínimo", porque uma atividade curta pode completar o dia. Os textos falam em "dias" em vez de "sessões".

## Impacto

- Pessoas com treinos divididos podem ganhar semanas cumpridas, sequências, conquistas de sequência e Energia de metas nos desafios já encerrados.
- As conquistas por tipo (`RatoDeAcademia` etc.), `Madrugador`, `Noturno` e `FimDeSemana` continuam contando atividades. Elas não afetam a classificação.
- O dia do formulário é o do aparelho, que costuma coincidir com o fuso do grupo.
