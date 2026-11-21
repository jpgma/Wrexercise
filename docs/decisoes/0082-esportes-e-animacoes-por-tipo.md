# 0082 — Futebol, Vôlei e Luta, e o Avatar que treina do jeito do esporte

**Status:** DECIDIDA. Acrescenta tipos de atividade (arquitetura §tipos), conquistas e Enfeites (`gamificacao.md` §9.1), amplia a folha de avatar da ADR 0039 e 0057 e muda a animação do treino ao vivo da ADR 0078.

## Contexto

- Os participantes jogam futebol, vôlei e lutam. Esses treinos caíam em `Esporte`, que não diz qual esporte, ou em `Outro`.
- No treino ao vivo (ADR 0078), o Avatar só tinha arte própria para a Corrida (`correr`) e a Caminhada (`andar`). Os outros tipos usavam o mesmo `treinando`.
- Os Enfeites dos tipos (halteres, bicicleta, piscina, golzinho…) ficavam parados no chão, sem ligação com o treino.
- A praça tem uma barra de calistenia separada dos outros objetos, guardada para "um avatar poder usá-la no futuro" (`ObjetosPraca.tsx`).
- No ícone da Corrida, uma pessoa parada (`PersonStanding`) não parecia corrida. O lucide não tem uma pessoa correndo.

## Alternativas consideradas

- **Trocar `Esporte` pelos três esportes:** não dá para saber o esporte das atividades antigas. Rejeitada.
- **Futebol e Vôlei contarem para o `Boleiro`:** mais simples, mas o mantenedor pediu uma conquista e um Enfeite por tipo novo. Rejeitada.
- **Animações só com o corpo, sem objetos:** mais fácil de gerar, porém difícil de reconhecer em 48 px. Rejeitada.
- **Uma folha separada só para os treinos:** obrigaria quem importa uma folha a enviar dois arquivos. Rejeitada.
- **Esperar a arte para ampliar a folha:** o mantenedor preferiu o encanamento completo já, com linhas provisórias.

## Decisão

### Tipos

- Novos tipos: `Futebol`, `Volei` ("Vôlei") e `Luta`. `Esporte` continua, com o rótulo "Outro esporte", para quem pratica outro esporte. As atividades antigas não mudam.
- Ordem no formulário: Musculação, Corrida, Caminhada, Ciclismo, Natação, Futebol, Vôlei, Luta, Yoga, Calistenia, Outro esporte, Outro.
- Ícones: Corrida ganha uma pessoa correndo e Futebol uma bola, ambos desenhados na grade do lucide (`iconesProprios.ts`, sem dependência nova). Vôlei usa `Volleyball` e Luta `HandFist`.

### Conquistas e Enfeites

| Conquista | Tipo | Enfeite |
|---|---|---|
| `Craque` | `Futebol` | Cones de treino com bola |
| `Levantador` | `Volei` | Redinha de vôlei de praia com bola |
| `FaixaPreta` ("Faixa preta") | `Luta` | Saco de pancada vermelho num pedestal |

As conquistas valem já. O Enfeite de cada uma entra no catálogo quando a arte existir. Como os Enfeites liberados são calculados a partir das conquistas, quem já tem a conquista recebe o Enfeite nesse dia.

### Folha de avatar: 225 linhas

A folha passa de 129 para 225 linhas (432 × 10 800). As folhas de 97 e de 129 linhas continuam aceitas. Nelas, as animações dos esportes viram `treinando`.

| Primeira linha | Nome | Quadros | ms | Objeto |
|---:|---|---:|---:|---|
| 129 | `musculacao` | 6 | 120 | halteres nas mãos |
| 137 | `ciclismo` | 6 | 80 | bicicleta (a mesma do Enfeite) |
| 145 | `natacao` | 6 | 110 | anel de água na cintura |
| 153 | `yoga` | 4 | 400 | — |
| 161 | `calistenia` | 4 | 150 | — (flexões) |
| 169 | `barra` | 6 | 120 | — (barra fixa da praça; só a direção `sul` é desenhada) |
| 177 | `volei` | 6 | 100 | bola |
| 185 | `manchete` | 4 | 100 | — (a bola é da cena) |
| 193 | `futebol` | 8 | 90 | bola (embaixadinhas) |
| 201 | `chutar` | 6 | 80 | — (a bola é da cena; sai no 4º quadro) |
| 209 | `luta` | 6 | 80 | — (sombra: guarda, direto, cruzado) |
| 217 | `largada` | 6 | 120 | — (agachado no bloco; só `leste`, espelhado) |

- Enquanto não há arte, o `build.py` preenche as linhas novas com os quadros de `treinando`.
- A `Luta` é a única exceção à regra "sem poses de combate" da arte: sombra, sem adversário e sem luvas.
- Os prompts para gerar a arte no ChatGPT ficam em `pixel-art/base-masculino/prompts-chatgpt.md` e `pixel-art/enfeites/prompts-chatgpt.md`.

### O treino ao vivo na praça

- **Sem o Enfeite no chão**, o Avatar treina no lugar dele, com o objeto genérico da animação.
- **Com o Enfeite do tipo no chão da praça agora** (na vitrine e colocado na cena), o Avatar vai até ele e o usa:
  - Musculação: ao lado do suporte de halteres.
  - Corrida: agacha no bloco de largada e sai correndo.
  - Caminhada: o cachorro sai do lugar e anda ao lado.
  - Ciclismo: o Avatar pega a bicicleta, que some do chão enquanto ele pedala.
  - Natação: dentro da piscininha, com a água cobrindo as pernas.
  - Yoga: no tapete, que se abre inteiro quando a pessoa chega e fica aberto até o fim do treino.
  - Calistenia: flexões nas paralelas enquanto espera a barra.
  - Futebol: pega a bola dos cones e a conduz em zigue-zague entre eles, um cone de cada lado; no fim da fileira, faz embaixadinhas e volta. Enquanto isso, os cones ficam sem a bola (a linha `craque-sem-bola` do atlas, recortada por código).
  - Vôlei: ao lado da redinha.
  - Luta: de frente para o saco de pancada.
  - Outro esporte: chuta a bola no golzinho.
- **Voltas:** Corrida, Caminhada e Ciclismo dão voltas numa pista por fora da elipse.
- **Barra:** quem faz Calistenia ao vivo reveza na barra da praça, em turnos de 20 segundos, na ordem de quem começou primeiro. Quem espera faz flexões ao lado. Sozinho, a pessoa alterna entre a barra e o chão.
- **Juntos:** duas ou mais pessoas no Futebol trocam passes. No Vôlei, trocam manchetes, por cima da redinha se alguém a tiver no chão.
- Voltas e turnos vêm do relógio e de `startedAt`. Uma encenação (Foco, cartas, Rival) tem prioridade. Com movimento reduzido, cada Avatar fica no seu lugar, num quadro só.
- No cronômetro, o Avatar faz a animação do tipo sozinho, sem Enfeite.

## Impacto

- Migração `043`: o `CHECK` de `atividades.tipo` aceita os três tipos.
- `ValidacaoFolhaAvatar` aceita 10 800, 6192 e 4656 px de altura.
- O `pixel-art/base-masculino/build.py` e os builds das outras folhas do bundle ganham as 12 linhas.
- A arte (avatar e os 3 Enfeites, mais o cachorro andando) será gerada depois. Até lá, as linhas são provisórias e os Enfeites novos não aparecem.
- **Atualização (2026-11-21):** os 3 Enfeites, o cachorro andando e o tapete se abrindo foram gerados (`pixel-art/enfeites/`, prompts 3 a 5). O saco de pancada ficou vermelho, e não violeta. O atlas tem 24 linhas: os 22 Enfeites e 2 linhas extras (`andarilho-andando`, `zen-aberto`).
