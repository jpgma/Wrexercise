# Prompts do PixelLab — personagem `jpgma` e animações que faltam

> As exportações de `fonte/` já têm `rotations`, `Walking`, `Running`, `Throw_Object`, `Cheering` e `Taking_Punch` (usado como `receber`, a reação de quem é atingido pela carta) nas 8 direções. Este arquivo traz o prompt do personagem e lista as animações que o Jogo usa e que ainda não existem (`docs/gamificacao.md` §3.2 e §3.3), com um prompt para o fluxo *animate character* do PixelLab.

## Personagem

Use este prompt para criar (ou recriar) o `jpgma` no PixelLab, por exemplo ao importar as `rotations` como referência para gerar animações novas. Ele descreve o personagem como ele está em `fonte/`.

```text
Young adult man in casual workout clothes, chibi proportions with a large head.
Messy, wavy dark brown hair, medium length, with a small tuft sticking up on top
and loose strands over the ears. Short full dark beard and mustache. Warm light tan skin,
dark eyes with light grey eye whites. Plain black crew-neck t-shirt with short sleeves,
long charcoal black pants, white sneakers with dark soles. Relaxed standing pose,
arms at the sides. Dark near-black outline, simple soft shading, muted colors.
```

**Configuração**, para ficar igual às exportações de `fonte/`:

- Tela de 64×64, com o personagem de cerca de 18×33 px, os pés perto da linha 45 e centrado na horizontal.
- Vista de cima e um pouco de lado (*low top-down*), 8 direções.
- Contorno de uma cor só, quase preto (`#0e071b`); sombreamento simples; detalhe médio.
- Se der para anexar referências, use as 8 imagens de `rotations` (por exemplo, recortadas de `saida/jpgma.png`, linha 0, ampliadas sem suavização) e a paleta de `PALETA` em `build.py`.
- Confira se a nova `rotations` sai igual à de `fonte/`. Se mudar, o build para (a tag repetida precisa ser idêntica nas fontes). Nesse caso, dê outro nome a essa tag em `FONTES` (por exemplo, `rotations_c`): os quadros continuam contados, mas não entram na folha.

## Como gerar

- **Personagem:** o mesmo `jpgma` (as `rotations` de `fonte/`). Mesma tela de 64×64, mesmo tamanho e mesma posição dos pés, para que o recorte fixo de `build.py` continue valendo.
- **Direções:** as 8, na ordem `south`, `south-east`, `east`, `north-east`, `north`, `north-west`, `west`, `south-west`.
- **Saída:** fundo transparente, sem antisserrilhado, sem sombra no chão, 200 ms por quadro (o padrão do PixelLab).
- **Paleta:** se o PixelLab aceitar uma paleta, use as 18 cores de `PALETA` em `build.py`. Senão, o build leva as cores novas para a mais próxima, então evite cores que não existem no personagem.
- **Quantidade de quadros:** a da tabela, que vem do formato atual (`docs/avatar-formato.md` §3). Se o PixelLab gerar outra quantidade, tudo bem: o build lê a quantidade de `FONTES`.
- **Exportar:** um `.aseprite` com uma camada `Idle` e tags `Acao-direcao` (por exemplo, `Idle_Breathing-south`), como os arquivos de `fonte/`. Salve como `fonte/jpgma-c.aseprite` (e assim por diante). Depois, acrescente o arquivo em `FONTES` e a linha em `ANIMACOES`, em `build.py`.

## Animações

| Nome na folha | Uso no Jogo | Quadros | Repetição |
|---|---|---:|---|
| `parado` | pose padrão | 4 | sim |
| `treinando` | atividade aguardando verificação | 6 | sim |
| `alongar` | variação de `parado` | 4 | uma vez |
| `olhar` | variação de `parado` | 4 | uma vez |
| `aceitar` | aceitar a carta | 4 | uma vez |
| `recusar` | recusar a carta | 3 | uma vez |
| `provocar` | encontro dos rivais | 4 | uma vez |

Hoje, `treinando` pode usar `correr` e `comemorando` pode usar `comemorar`. Gerar `treinando` só vale a pena se a corrida "no lugar" ficar estranha na cena.

O `receber` vem do `Taking_Punch`, que começa e termina com os punhos levantados, não na pose parada. Se o salto entre as poses incomodar na cena, gere de novo com este prompt (6 quadros, o impacto no terceiro):

```text
Gets hit in the chest by a small card thrown at them: starts from the relaxed idle pose,
the upper body jolts back on frame 3, the head tilts, then recovers and ends in the same
relaxed idle pose. Comic, not violent. Feet do not move. 6 frames. Do not draw the card.
```

### `parado` — Idle_Breathing

```text
Idle breathing loop, standing still in place. Relaxed stance, arms at the sides.
Subtle chest rise and fall, shoulders move 1 pixel. Feet do not move. Seamless loop, 4 frames.
```

### `treinando` — Jogging_In_Place

```text
Light jogging in place, warming up. Knees lift a little, arms swing bent at the elbows.
The body bounces 1 pixel. Feet stay in the same spot, no forward travel. Seamless loop, 6 frames.
```

### `alongar` — Stretching

```text
Quick stretch while standing: one arm reaches straight up over the head, the body leans
slightly to the side, then returns to the relaxed idle pose. Feet do not move. 4 frames, ends in idle pose.
```

### `olhar` — Looking_Around

```text
Looks around while standing: the head turns to one side, then to the other side, then back to the front.
The body stays still. 4 frames, ends in idle pose.
```

### `aceitar` — Fist_Pump

```text
Accepts a challenge with confidence: a short crouch, then a fist pump with one arm raised
above the head. Feet do not move. 4 frames, ends with the fist up.
```

### `recusar` — Shrug

```text
Shrugs to decline: both shoulders and hands lift, palms up, the head tilts a little to the side.
Feet do not move. 3 frames, ends at the top of the shrug.
```

### `provocar` — Taunting

```text
Playful taunt to a rival in front of them: points forward with one arm extended, a small
lean forward, a quick bounce, then back to the point. Friendly, not aggressive. Feet do not move. 4 frames.
```

## Retrato

O `retrato` (o `Avatar` fora da cena) não precisa de geração: ele pode sair de um recorte da cabeça e dos ombros de `rotations-south`. O tamanho e o recorte ficam para a decisão do formato (`docs/decisoes/0039-avatar-em-8-direcoes.md`).
