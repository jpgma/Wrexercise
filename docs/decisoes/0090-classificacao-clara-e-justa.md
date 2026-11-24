# 0090 — Classificação clara e justa

**Status:** DECIDIDA. Substitui a ordem de desempate da `0013-competicao-e-progresso.md`. Mantém a adesão tardia da 0016 (semanas antes da entrada contam como 0).

## Contexto

O mantenedor pediu que as regras da classificação ficassem claras para os participantes e que fossem revistas onde não parecessem justas. A revisão encontrou:

- **Sequência zerada toda segunda-feira.** `GetSequenciaAtual` começava na semana em andamento. Até a pessoa bater a meta da semana, a sequência era 0, o 🔥 sumia e o 3º critério virava "quem já bateu a meta desta semana". O mesmo valia para a página do membro.
- **Texto errado.** "Regras do desafio" falava da "maior sequência de semanas", mas o critério é a sequência atual.
- **Desempate pelo nome.** O último critério era a ordem alfabética: uma posição real decidida pelo nome.
- **Tempo e quantidade de treinos não desempatavam.** O mantenedor quer que contem.
- **Regras sem explicação.** Ninguém dizia que dias além da meta contam no desempate, que um treino verificado depois do fim da semana ainda conta para a semana dele, que a semana atual conta assim que a meta é batida, nem que quem entra no meio começa atrás.
- **Unidade errada.** "Faltam N treinos para passar Ana" conta dias: dois treinos no mesmo dia contam um só.

## Alternativas consideradas

- **Percentual de semanas para quem entra no meio.** Mais justo para quem chega tarde, mas 1 de 1 semana ficaria à frente de 7 de 8. Descartada: a contagem continua absoluta e a interface diz isso.
- **Dias limitados à meta de cada semana.** Tiraria a vantagem de quem treina mais que a meta. Descartada: treinar mais continua valendo no desempate.
- **Tempo antes da sequência, ou no lugar dos dias.** Premiaria treinos longos mais que a constância, contra a ideia de manter a rotina. Descartada.
- **Quantidade de atividades antes do tempo.** Fácil de inflar dividindo um treino em dois. Descartada: as atividades só desempatam depois do tempo.
- **Quem chegou primeiro ao placar.** Mais justo que o nome, mas difícil de explicar. Descartada.
- **Manter a ordem alfabética.** Descartada.

## Decisão

Ordem da classificação:

1. Semanas com a meta cumprida.
2. Dias verificados no desafio (inclusive além da meta e em semanas não cumpridas).
3. Sequência atual de semanas cumpridas.
4. Tempo verificado: soma de `duracao_segundos` das atividades `Verificada` que valem para o desafio.
5. Atividades verificadas: quantidade dessas atividades.

Quem empata nos 5 critérios **divide a posição** (1, 2, 2, 4). O nome só dá uma ordem estável dentro do empate. As medalhas da praça seguem a posição: dois 🥈 são possíveis.

**Sequência.** A semana em andamento só entra na sequência depois de cumprida. Até lá, a sequência conta a partir da semana anterior. Depois do fim do desafio, a última semana já está fechada e conta como as outras.

**Totais.** `ContagensDesafio.LoadTotaisVerificadosAsync` usa o mesmo `FromElegiveisSql` das contagens de dias: período do desafio, membro ativo, semanas a partir da entrada. Atividades pendentes, rejeitadas, anuladas ou ao vivo não entram. A Correção da duração vale na hora, porque muda `duracao_segundos`.

**Resposta.** Cada item de `classificacao` (em `/classificacao` e `/resumo`) ganha `segundosVerificados`, `atividadesVerificadas` e `semanasDisputadas` (semanas até a atual em que a pessoa já contava, como na página do membro). `posicao` passa a vir do cálculo, com empate. `proximo` ganha o tipo `EmpateLider`.

**Interface.**

- A linha mostra os mesmos números (semanas, dias, 🔥). O tempo e as atividades só aparecem no texto do motivo, quando decidem.
- Quem entrou no meio vê "X de Y semanas".
- O bloco "Como funciona a classificação", abaixo da lista, explica os critérios e os casos acima.
- "Regras do desafio" ganha a ordem completa e a regra da adesão tardia.
- As disputas falam em "dias de treino".

## Impacto

- `CalculoSemanaDesafio.GetSequenciaAtual` recebe `semanaRecenteEmAndamento`. A página do membro também deixa de zerar a sequência na segunda-feira.
- `ClassificacaoDesafio`, `ContagensDesafio`, `ObterClassificacao`, `ObterResumoGrupo` e `Respostas.cs` mudam. Não há migração.
- Frontend: `vantagem.ts`, `motivacao.ts`, `ListaClassificacao.tsx`, `ComoFuncionaClassificacao.tsx`, `RegrasDesafio.tsx`.
- `funcionalidades.md` §3.3.2 e §9.3; versão 1.10.0.
