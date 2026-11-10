# 0018 — Perfil, página do membro e conquistas

**Status:** DECIDIDA. Completa a Q033 e `arquitetura.md` §8.5.

## Contexto

O novo mockup (decisão 0014) tem um Perfil com estatísticas, gráfico e conquistas, e uma página por membro. A Q033 deixava o Perfil fora do escopo inicial. As ações de administração ficavam em botões dentro da lista de membros do Grupo.

## Alternativas consideradas

**Alcance dos dados**

- **Perfil único, igual para todos:** simples, mas revelaria atividade de um grupo a pessoas de outro.
- **Página do membro limitada ao grupo, conquistas só no próprio Perfil:** nada atravessa grupos para quem não é o dono dos dados.

**Conquistas**

- **Tabela com conquistas concedidas:** guarda a data, mas exige gravar em cada mudança e desfazer quando uma atividade é rejeitada.
- **Calculadas na leitura:** sem esquema novo, e sempre coerentes com as atividades.

## Decisão

- `GET /api/usuarios/eu/perfil` devolve totais de treinos verificados, tempo total, melhor sequência, as últimas 8 semanas (de segunda a domingo) e as conquistas, somando todos os grupos. A semana atual vem do fuso dos grupos ativos da pessoa; com fusos diferentes, vale o primeiro a virar a semana, e sem grupo vale o UTC. O gráfico não tem linha de meta, porque os grupos podem ter metas diferentes.
- `GET /api/grupos/{id}/membros/{usuarioId}/perfil` exige ser membro ativo do grupo e só traz dados dele: nome, papel, totais no grupo e o desafio em destaque. Nunca e-mail nem conquistas.
- As conquistas ficam em `ConquistasUsuario`, uma regra pura:
  - sequência de 4, 8 e 12 semanas, usando a melhor sequência em um único desafio, sem somar desafios;
  - 10, 50 e 100 treinos verificados;
  - Madrugador: 10 treinos verificados iniciados antes das 7h no fuso do grupo.
- Conquistas não têm efeito na classificação. Sair de um grupo não remove o que a pessoa conquistou nele.
- Atualização: o Perfil também mostra as conquistas ainda bloqueadas, com o progresso (`atual` de `alvo`). Nos níveis, só aparece o próximo. O cálculo continua na leitura, sem tabela nova, e só a própria pessoa vê.
- Papel, transferência e remoção saem da lista do Grupo e vão para a página do membro. Sair do grupo continua na página do Grupo.
- A lista de atividades do grupo aceita `?autorId=`, para a página do membro.
- O desafio em destaque passa a ser o que está em andamento, e depois o próximo. Antes, com desafios em sequência, o agendado passaria na frente do atual.

## Impacto

- Nova fatia `Features/Usuarios`, sem migração.
- `CalculoSemanaDesafio` ganha o cálculo das sequências, usado também pela classificação.
- `Conquista` entra no glossário.
