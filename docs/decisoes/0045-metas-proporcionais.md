# 0045 — Metas por tipo proporcionais

**Status:** DECIDIDA. Substitui os itens 1, 2, 4 e 6 de `0043-metas-por-tipo.md`; os itens 3, 5 e 7 continuam valendo, com o texto ajustado abaixo.

## Contexto

Pela decisão 0043, uma meta por tipo é uma alternativa ao mínimo do dia (OU), e o mínimo dela precisa ser menor que o do desafio. Isso resolve o treino mais puxado (20 min de corrida em vez de 60 min de qualquer treino), mas não o caso contrário: quem vai ao trabalho de bicicleta pedala 1 h todo dia e cumpre a meta de 60 min sem treinar de propósito. O grupo quer um desafio de 60 min no geral, 90 min de ciclismo e 30 min de outro grupo de tipos.

Só permitir um mínimo maior não basta: com a regra OU, os 60 min de bicicleta ainda alcançam os 60 min do total.

## Alternativas consideradas

- **Manter o OU e só permitir mínimos maiores:** não resolve, pela razão acima.
- **Grupos exclusivos:** os tipos de uma meta só contam para ela, e o total geral soma só os tipos sem meta. Resolve, mas é duro com dias mistos: 60 min de bicicleta + 20 min de corrida não contam nada.
- **Metas por pessoa:** resolveria o caso do ciclista sem mexer nos outros, mas complica a classificação (já adiada pela 0043).
- **Proporcional:** cada atividade preenche uma parte do dia, os segundos dela divididos pelo mínimo do tipo; o dia conta quando as partes somam 100%.

## Decisão

1. **Regra proporcional.** O mínimo de um tipo é o da meta por tipo que o inclui; sem meta, é a duração mínima do desafio. Cada atividade preenche `duracao_segundos / mínimo do tipo` do dia, e o dia conta como uma sessão quando a soma chega a 1. Exemplo, com 60 min no geral, 90 min de Ciclismo e 30 min de Corrida: 60 min de bicicleta preenchem 67% e não contam; somados a 10 min de corrida (33%), contam.
2. **Com mínimos menores, é mais generosa que a 0043:** 10 min de corrida (metade de 20) + 30 min de musculação (metade de 60) agora contam. É o que se espera de quem treinou "meio" de cada coisa.
3. **Validação:** o mínimo de uma meta vai de 60 s até 24 h, como o do desafio, e não pode ser igual ao dele (não mudaria nada). Um tipo fica em no máximo uma meta, para cada minuto ter um único valor. Continua o limite de 4 metas.
4. **Arredondamento:** a soma é arredondada a 9 casas antes de comparar com 1 (`ContagensAtividadeDesafio.CompletaODiaSql`; o frontend faz o mesmo), para que, por exemplo, três partes de 1/3 não percam o dia por resíduo da divisão. O que se ignora é menos de um milissegundo.
5. **Rival:** cada dia conta no `verified_at` da primeira atividade verificada que levou a soma das partes do dia a 100%.
6. **Qual meta aparece no dia:** `metaPorTipoId` passa a ser a meta cujos tipos preencheram a maior parte do dia; nulo quando os tipos sem meta preencheram mais (empate favorece o geral e, depois, a ordem de criação), ou quando o dia não conta.
7. **Vale para todos os desafios.** As metas por tipo existiam havia dois dias, sem uso real, então a regra muda também para os desafios já criados, sem coluna de versão. Dados criados antes, com um tipo em duas metas, usam a primeira meta na ordem de criação.
8. **Interface:** a seção da criação passa a se chamar "Mínimo por tipo", os tipos já usados em outra meta ficam desabilitados, e as descrições trocam o "ou" por "60 min por dia (Pedal: 1h30, Corrida: 30 min)". O formulário de registro mostra quanto falta do tipo escolhido e, se houver, um caminho mais curto: "Faltam 30 min de Pedal (ou 10 min de Corrida)".

## Impacto

- `ContagensAtividadeDesafio.BuildDiasPorCondicao` soma partes em vez de avaliar condições; `JoinMetaDoTipoSql`, `ParteDoDiaSql` e `CompletaODiaSql` são os fragmentos compartilhados, usados também por `Rivais`.
- Sem migração: a tabela `metas_tipo_desafio` não muda.
- Energia, Conquistas, o total de treinos do Perfil e as Jogadas +10 MIN e +20 MIN continuam como na 0043, sem metas por tipo.
