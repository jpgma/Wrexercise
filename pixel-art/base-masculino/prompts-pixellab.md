# Prompts do PixelLab — personagem base masculino

> Prompts para gerar no PixelLab um personagem masculino **neutro**, sem traços marcantes, que serve de modelo para as folhas padrão em 8 direções (`docs/decisoes/0039-avatar-em-8-direcoes.md`, ponto "Folhas padrão"). Os personagens padrão saem dele por edição (cabelo, pele, roupa, acessórios), e todos mantêm o mesmo corpo, o mesmo tamanho e as mesmas animações. O formato segue o do `jpgma` (`../jpgma/prompts-pixellab.md`), para que o mesmo recorte de 48×48 e a mesma âncora valham.

## 1. Identidade visual

O Jogo é um **fliperama urbano onde a turma se encontra** (`docs/gamificacao.md` §1.2 e §1.3): pixel art contemporânea, estilo de vida urbano, roupa de academia e streetwear, numa quadra de asfalto vista de cima. O tom é leve e bem-humorado; as cartas são bilhetes de provocação entre amigos.

O que isso exige do personagem:

- **Pessoa comum, de hoje.** Nada de fantasia, medieval, armadura, capa, arma, magia ou pose de luta. Toda reação é cômica, nunca violenta.
- **Leitura sobre asfalto escuro.** O chão da cena é Asphalt `#222633`, com uma rampa de cinzas-azulados. Roupa preta ou azul-marinho some no chão: o base usa uma camiseta clara e uma calça cinza média, com contraste claro contra o contorno.
- **Cores contidas.** Os acentos da marca (Coral `#FF684A`, Electric Violet `#7957E8`, Signal Yellow `#FFD34E`, Lime `#A8D978`) ficam para os personagens derivados, um ou dois por personagem (`docs/avatar-kit.md` §2). O base não usa nenhum.
- **Simetria.** Nada de um lado só (franja de lado, fone, relógio, garrafa numa mão). Assim as 8 direções ficam coerentes e o base serve de ponto de partida para qualquer variação.
- **Regiões de cor separadas.** Pele, cabelo, camiseta, calça e tênis com rampas próprias e sem estampa. Isso facilita trocar uma região por outra cor ao derivar os personagens.

Cole este bloco no campo de estilo (ou no fim da descrição) em toda geração, inclusive nas animações:

```text
Style: contemporary urban pixel art for a cozy, playful fitness game. Everyday modern people
in gym clothes and streetwear, seen in low top-down view on a dark blue-grey asphalt court.
Clean readable silhouette, single-color near-black outline, simple soft shading with 3 tones
per material, medium detail, muted natural colors with good contrast against a dark floor.
No fantasy, no medieval, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

## 2. Personagem

### Prompt

```text
Base male character for a fitness game, a neutral template with no distinguishing features.
Young adult man of average height and average build, slightly athletic but not muscular.
Chibi proportions: large head about one third of the body height, short torso, short legs,
compact rounded hands, simple shoes. Symmetrical design, nothing on one side only.

Face: friendly neutral expression, small dark eyes with light grey whites, short simple
eyebrows, tiny nose shown by shading only, mouth closed or a 1-pixel neutral line.
Clean-shaven, no beard, no mustache, no glasses, no freckles, no scars, no makeup.

Hair: short, neat, close-cropped dark brown hair, even length all around, natural hairline,
no fringe, no part, no spikes, no tuft. The hair hugs the head shape so other hairstyles
can be drawn over it later.

Skin: medium warm skin tone, a mid value between light and dark, with one shadow tone and
one highlight tone.

Clothes: plain light heather grey crew-neck t-shirt with short sleeves, fitted but not tight,
no logo, no print, no stripes. Plain mid slate grey jogger pants, straight and tapered at the
ankle, no stripes, no pockets drawn. Plain white low-top sneakers with light grey soles,
no colored details. No accessories: no cap, no headphones, no watch, no bag, no bottle.

Pose: relaxed neutral standing pose, facing the viewer, weight on both feet, feet slightly
apart under the hips, arms relaxed at the sides with a small gap from the torso so the
silhouette reads clearly.
```

Depois do prompt, acrescente o bloco de estilo de §1.

### Configuração

Os valores abaixo reproduzem as exportações do `jpgma`, para que o build use o mesmo recorte (x 8..55 e y 1..48 do quadro de 64) e a mesma âncora (24, 44) na célula de 48×48:

| Opção | Valor |
|---|---|
| Tela | 64×64 |
| Tamanho do personagem | cerca de 18×33 px parado, centrado na horizontal, pés perto da linha 45 |
| Vista | *low top-down* (de cima e um pouco de lado) |
| Direções | 8 |
| Proporções, se houver a opção | *chibi* |
| Contorno | *single color black outline* (quase preto, como o `#0e071b` do `jpgma`) |
| Sombreamento | *basic shading* |
| Detalhe | *medium detail* |
| Fundo | transparente, sem sombra no chão, sem antisserrilhado |

### Paleta sugerida

Se o PixelLab aceitar uma paleta ou cores de referência, use estas (até 16, como nas exportações). Os valores são um ponto de partida; o que importa é a separação das regiões e o contraste com o chão.

| Região | Sombra | Base | Luz |
|---|---|---|---|
| Contorno | `#0e071b` | | |
| Pele | `#8a523e` | `#c26c4d` | `#e0855a` |
| Cabelo | `#1c1215` | `#392627` | `#493334` |
| Camiseta | `#8e93a3` | `#b9bdc8` | `#dcdfe6` |
| Calça | `#363c52` | `#4c5470` | `#646d8c` |
| Tênis | `#b9bdc8` (sola) | `#f5f1e8` | |
| Olhos | `#0e071b` (pupila) | `#dcd1d9` (branco) | |

A pele e o cabelo vêm das rampas do `jpgma`. A camiseta e a calça seguem a rampa de cinzas-azulados do chão, mas de 2 a 3 tons acima de Asphalt `#222633`. O branco do tênis é o Chalk `#F5F1E8` das linhas da quadra.

### `rotations` prontas

`build.py` recorta as 8 direções de `fonte/base.png` (uma imagem gerada, em escala grande) e as reduz para o tamanho do `jpgma`: 33 px de altura, os pés na linha 45 e centrado num quadro de 64×64, com contorno de 1 px e a paleta fixa `PALETA` do script. Os acertos à mão de olhos e sobrancelhas ficam em `RETOQUES`, no próprio script. Use `saida/rotations/<direcao>.png` (nome sem hífen, por exemplo `northeast.png`) como referência no PixelLab, ou importe as 8 como as rotações do personagem.

### Conferência da `rotations`

Antes de gerar as animações, confira as 8 direções da `rotations`:

- O personagem tem a mesma altura e os pés na mesma linha em todas as direções.
- Norte mostra só cabelo e nuca, sem rosto; sul mostra o rosto inteiro.
- Leste e oeste são o espelho um do outro (o personagem é simétrico).
- Nenhum detalhe extra apareceu (logo, listra, barba, acessório).
- A camiseta e a calça se distinguem do chão: veja a imagem sobre `#222633`.

Se algo falhar, gere de novo antes de seguir. Toda animação parte desta `rotations`.

## 3. Como gerar as animações

- **Fluxo:** *animate character*, a partir da `rotations` aprovada, nas 8 direções e na ordem `south`, `south-east`, `east`, `north-east`, `north`, `north-west`, `west`, `south-west`.
- **Modelo do PixelLab ou texto:** quando houver um modelo pronto equivalente (andar, correr, respirar), prefira o modelo, porque ele mantém o corpo mais estável. Nos outros casos, ou se o modelo sair estranho, use o prompt desta seção como descrição da ação.
- **Ponto de partida comum:** toda animação que toca uma vez começa e termina na pose parada de `rotations`, salvo quando o prompt diz outra coisa. Isso evita o salto de pose que o `Taking_Punch` do `jpgma` tem.
- **Pés:** só `andar` e `correr` deslocam os pés. Nas outras, os pés ficam no lugar, para que a âncora valha.
- **Nada de objetos:** a carta voa em CSS na cena (`docs/gamificacao.md` §3.3). Não desenhe carta, bola, peso ou garrafa.
- **Saída:** 200 ms por quadro (o padrão do PixelLab), fundo transparente, sem sombra no chão.
- **Paleta:** a mesma de §2. Cores novas vão para a mais próxima no build; evite cores que o personagem não tem.
- **Quadros:** os da tabela. Se o PixelLab gerar outra quantidade, tudo bem: o build lê a quantidade das tags.
- **Exportar:** `.aseprite` com uma camada e tags `Acao-direcao` (por exemplo, `Idle_Breathing-south`), como em `../jpgma/fonte/`. Salve em `fonte/base-masculino-a.aseprite`, `-b` e assim por diante.

Acrescente o bloco de estilo de §1 em toda animação.

## 4. Animações

| Nome na folha | Tag sugerida | Uso no Jogo | Quadros | Repetição |
|---|---|---|---:|---|
| `rotacao` | `rotations` | pose parada em cada direção | 1 | estático |
| `parado` | `Idle_Breathing` | pose padrão | 4 | sim |
| `andar` | `Walking` | ir ao centro e voltar | 8 | sim |
| `correr` | `Running` | deslocamento rápido | 8 | sim |
| `treinando` | `Jogging_In_Place` | atividade aguardando verificação | 6 | sim |
| `alongar` | `Stretching` | variação de `parado` | 4 | uma vez |
| `olhar` | `Looking_Around` | variação de `parado` | 4 | uma vez |
| `arremessar` | `Throw_Card` | jogar uma carta | 6 | uma vez; a carta sai no 4º quadro |
| `receber` | `Hit_By_Card` | ser atingido pela carta | 6 | uma vez; impacto no 3º quadro |
| `aceitar` | `Fist_Pump` | aceitar a carta | 4 | uma vez |
| `recusar` | `Shrug` | recusar a carta | 3 | uma vez |
| `provocar` | `Taunting` | encontro dos rivais | 4 | uma vez |
| `comemorar` | `Cheering` | meta cumprida, Jogada concluída, Missão, Enfeite | 8 | sim |

### `parado` — Idle_Breathing

```text
Idle breathing loop, standing still in place in the relaxed neutral pose. Arms at the sides.
The chest and shoulders rise 1 pixel and fall back, the head follows by 1 pixel.
Feet do not move. Calm, subtle, seamless loop. 4 frames.
```

### `andar` — Walking

```text
Casual relaxed walk at a normal pace. Arms swing gently opposite to the legs, small steps,
the body bobs 1 pixel on each step. Upright posture, friendly and unhurried.
Seamless loop, the first frame matches the standing pose as closely as possible. 8 frames.
```

### `correr` — Running

```text
Light athletic run, like a morning jog in the park, not a sprint. Arms bent at the elbows
swing forward and back, knees lift moderately, the torso leans forward slightly,
the body bounces 1 to 2 pixels. Seamless loop. 8 frames.
```

### `treinando` — Jogging_In_Place

```text
Light jogging in place, warming up before a workout. Knees lift a little, one at a time,
arms swing bent at the elbows. The body bounces 1 pixel. The feet stay over the same spot,
no forward travel at all. Seamless loop. 6 frames.
```

### `alongar` — Stretching

```text
Quick casual stretch while standing: both arms reach straight up over the head with the
hands together, the body leans a little to one side, then the arms come down and the
character returns to the relaxed neutral pose. Feet do not move.
4 frames, starts and ends in the idle pose.
```

### `olhar` — Looking_Around

```text
Looks around while standing, a little curious: the head turns to one side, then to the
other side, then back to the front. The body and arms stay still, feet do not move.
4 frames, starts and ends in the idle pose.
```

### `arremessar` — Throw_Card

```text
Tosses a small playing card overhand toward someone in front, playful and confident,
like flicking a note to a friend. Starts from the relaxed idle pose, frames 2 and 3 wind up
with the throwing arm back and the weight shifting to the back foot, frame 4 snaps
the arm forward and releases, frames 5 and 6 follow through and return to the idle pose.
Feet stay in place. Do not draw the card. 6 frames.
```

### `receber` — Hit_By_Card

```text
Gets hit in the chest by a small card thrown at them, comic surprise, not pain. Starts from
the relaxed idle pose, frame 2 notices it coming, frame 3 is the impact: the upper body jolts
back, the head tilts back, the arms flick outward. Frames 4 to 6 recover with a small shake
of the head and end in the same relaxed idle pose. Feet do not move. Do not draw the card. 6 frames.
```

### `aceitar` — Fist_Pump

```text
Accepts a challenge with confidence: starts from the relaxed idle pose, a short crouch on
frame 2, then a fist pump with one arm raised straight above the head and a small nod.
Feet do not move. 4 frames, ends with the fist up.
```

### `recusar` — Shrug

```text
Shrugs to decline, relaxed and good-humored, no hard feelings: starts from the relaxed idle
pose, both shoulders lift, the forearms come up with open palms facing up, the head tilts
a little to the side. Feet do not move. 3 frames, ends at the top of the shrug.
```

### `provocar` — Taunting

```text
Friendly taunt to a rival standing in front of them, like trash talk between friends before
a game: points forward with one arm extended, leans forward a little, a quick bounce on the
spot, then points again with a confident grin. Not aggressive, no fists, no fighting stance.
Feet do not move. 4 frames.
```

### `comemorar` — Cheering

```text
Celebrates a goal reached: jumps up a little with both fists raised above the head, happy
open-mouth smile, lands and bounces, both arms pump in the air again. Joyful, like finishing
a workout streak. The feet return to the same spot on every landing. Seamless loop. 8 frames.
```

## 5. Derivar personagens

Os personagens padrão saem da `rotations` do base, com a edição de personagem do PixelLab (ou uma geração com a `rotations` como referência), trocando só uma ou duas regiões por vez. Depois, as animações são geradas de novo para cada personagem, com os mesmos prompts de §4. Modelo de prompt:

```text
Same character, same body, same size, same pose and same proportions as the reference.
Change only: <short curly black hair> and <a coral #FF684A tank top>. Keep everything else
identical: face, skin, pants, sneakers, outline and shading style.
```

Regras para os derivados, vindas de `docs/gamificacao.md` §1.3 e de `docs/avatar-kit.md` §2:

- Um ou dois acentos da marca por personagem, na roupa ou num acessório (boné, fone, munhequeira).
- Variedade de tons de pele, de cabelos e de corpos entre os personagens padrão.
- Acessórios de um lado só são permitidos nos derivados, porque a folha não usa espelho.
- As folhas padrão são dedicadas ao domínio público (CC0), então nada de marca, logo ou personagem de terceiros.

## 6. Retrato

O `retrato` (o `Avatar` fora da cena) não precisa de geração: ele sai de um recorte da cabeça e dos ombros de `rotations-south`, como no `jpgma` (ADR 0039, item 6).
