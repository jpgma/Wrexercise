# 0043 — Metas por tipo

**Status:** DECIDIDA. Complementa os itens 1, 2 e 6 de `0040-meta-diaria.md`: um dia passa a contar por mais de uma condição. Itens 1, 2, 4 e 6 substituídos por `0045-metas-proporcionais.md`: as metas por tipo passaram a valer em proporção, e o mínimo delas pode ser maior que o do desafio.

## Contexto

Pela decisão 0040, um dia conta quando a soma das atividades alcança `duracao_minima_segundos`. Num grupo com pessoas em níveis de condicionamento diferentes, e com esportes diferentes, um único número de minutos não serve para todos: 20 min de corrida puxada valem tanto quanto 60 min de caminhada, mas só a caminhada cumpre o mínimo.

O grupo quer manter o mínimo geral e acrescentar, no mesmo desafio, mínimos menores para tipos de atividade específicos, configurados no celular como blocos.

## Alternativas consideradas

- **Todas as condições no mesmo dia (E):** o dia só conta com 60 min no total **e** 20 min de corrida **e** 30 min de calistenia. Simples, mas obriga todo mundo a fazer todos os tipos todos os dias, o contrário do que se quer.
- **Metas semanais por tipo:** "corrida em 2 dias da semana", ao lado da meta geral. Força variedade, mas cria uma segunda contagem semanal e não resolve a diferença de condicionamento.
- **Alternativas (OU):** o dia conta com 60 min de qualquer treino **ou** com 20 min de corrida **ou** com 30 min de calistenia.
- **Metas por pessoa (níveis):** resolve a diferença de condicionamento, mas complica a classificação. Fica para depois, se as alternativas não bastarem.

## Decisão

1. **Alternativas (OU).** Um desafio tem de 0 a 4 metas por tipo (`MetaPorTipo`). Cada uma tem um ou mais tipos de atividade, uma duração mínima e um nome opcional (até 20 caracteres, por exemplo "Cardio"). Um dia conta como **uma** sessão quando a soma do dia alcança `duracao_minima_segundos` **ou** quando a soma das atividades dos tipos de uma meta alcança o mínimo dela. Nunca mais de uma sessão por dia.
2. **Validação:** os tipos de uma meta são válidos e não se repetem; o mínimo vai de 60 s até menos que a duração mínima do desafio, senão a meta nunca faria um dia contar; duas metas não podem ter o mesmo conjunto de tipos.
3. **Definitivo e provisório por condição.** O dia é definitivo quando alguma condição é cumprida só com atividades `Verificada`; provisório quando alguma só é cumprida somando as pendentes. O SQL canônico continua em `Infrastructure/Database/ContagensAtividadeDesafio.cs` (`BuildDiasPorCondicao`), e `DesafiosDaPessoa` passou a usá-lo em vez de repetir a regra.
4. **Rival:** cada dia conta no `verified_at` da primeira atividade verificada que levou alguma condição do dia ao mínimo dela.
5. **Imutável:** como o resto da meta, as metas por tipo não mudam depois da criação. Desafios antigos não têm metas por tipo e não mudam.
6. **Qual condição fez o dia contar:** o progresso devolve, em `seusDias`, o `metaPorTipoId` de cada dia: nulo quando a duração mínima do desafio foi alcançada (ela tem prioridade); senão, a primeira meta cumprida na ordem de criação (`metas_tipo_desafio.ordem`). A faixa da semana na tela inicial mostra o ícone da meta no dia e uma legenda.
7. **Interface:** a criação do desafio ganha a seção "Ou o dia conta com…", com blocos de tipos e minutos. O formulário de registro considera o tipo escolhido e mostra a condição mais próxima: "Faltam 5 min de Corrida (ou 25 min no total)". `seusSegundosPorDia` ganha `segundosPorTipo`.

## Impacto

- Nova tabela `metas_tipo_desafio` (migração 021). O tipo de atividade virou contrato de Atividades (`Features/Atividades/Contracts/TipoAtividade.cs`) para Desafios validar os tipos.
- As consultas de contagem agregam por tipo antes de agregar por dia. O volume é pequeno (um grupo, uma semana ou um desafio), e o único índice novo é o `UNIQUE (desafio_id, ordem)`.
- Energia, Conquistas e o total de treinos do Perfil não usam a duração mínima e não mudam. As Jogadas +10 MIN e +20 MIN continuam partindo só da duração mínima do desafio, sem as metas por tipo: são um desafio extra de volume.
- `ProgressoResponse.seusDias` deixou de ser uma lista de datas e virou uma lista de `{ data, metaPorTipoId }`.
