# Prompts do Gemini — personagem base masculino

> Prompts para gerar no Gemini (Nano Banana) o personagem base masculino **neutro** em 8 direções, a mesma peça descrita em `prompts-pixellab.md` (identidade, paleta e regras de §1 e §2 de lá continuam valendo). A imagem gerada é só a **fonte**: ela vai para `fonte/base.png`, e o `build.py` separa as 8 direções, as reduz para 33 px de altura e grava `saida/rotations/<direcao>.png`. Nunca copie a imagem gerada direto para `saida/` ou `webapp/public/`.
>
> **Divisão de trabalho:** o Gemini faz bem uma folha de poses paradas e mal uma animação quadro a quadro. Use o Gemini para a `rotations` (§3). Para as animações, prefira o PixelLab (`prompts-pixellab.md` §3 e §4), partindo da `rotations` que o `build.py` gerou; §4 traz um prompt completo do Gemini para cada animação, sempre a partir da imagem parada de uma direção, para quando o PixelLab não der conta.

## 1. Como usar o Gemini

- **Onde:** de preferência o Google AI Studio, com o modelo de imagem do Gemini. O app do Gemini pode pôr uma marca visível num canto da imagem, por cima de um personagem.
- **Proporção:** 3:2, deitada (o `build.py` foi feito para 1536×1024). A grade tem 4 colunas e 2 linhas.
- **Fundo:** magenta chapado `#FF00FF`. O Gemini não gera transparência, e o magenta não existe no personagem. **Atenção:** o `build.py` atual separa o personagem de um fundo escuro com brilho (o da `fonte/base.png` de hoje). Para uma fonte em magenta, ele precisa tratar o magenta como fundo antes de procurar o contorno.
- **Referência:** anexe `saida/referencia-gemini.png` (as 8 direções de `fonte/<direcao>.png` na grade e na ordem de §3, sobre magenta, ampliadas 8×; gere de novo com `python tira.py`) como referência de proporção e de vista. Diga que ela mostra o corpo e o ângulo, e não o rosto, o cabelo ou a roupa. Não anexe arte de terceiros: as folhas padrão são CC0.
- **Iteração:** corrija uma direção por vez na mesma conversa ("keep the other seven identical, only redraw the north-east one: the head must turn further away"). Gerar do zero muda o personagem.
- **Salvar:** baixe o PNG em resolução cheia e salve em `fonte/base.png`.

## 2. Bloco de estilo

Cole este bloco no fim de toda geração e de toda edição:

```text
Style: contemporary urban pixel art for a cozy, playful fitness game. Everyday modern people in
gym clothes and streetwear, seen in low top-down view (from above and a little from the side).
Drawn on a small pixel grid and enlarged with nearest-neighbor: every pixel is a crisp square
block, all blocks the same size across the whole image. Clean readable silhouette, single-color
near-black outline (#0e071b), simple flat shading with 3 tones per material, light from the
top left. No anti-aliasing, no blur, no gradients, no glow, no rim light, no cast shadow on the
floor, no text, no watermark. No fantasy, no medieval, no armor, no weapons, no magic, no combat
poses. Friendly and comic tone.
```

## 3. `rotations` — 8 direções

### Prompt

```text
Character turnaround sprite sheet of the same character in 8 directions, arranged in a grid of
4 columns and 2 rows of equal cells, read left to right, top to bottom, in this exact order:
1 facing the viewer (south), 2 facing down-right (south-east), 3 facing right (east),
4 facing up-right (north-east), 5 facing away from the viewer (north), 6 facing up-left
(north-west), 7 facing left (west), 8 facing down-left (south-west).
Solid flat magenta background (#FF00FF) everywhere, with no grid lines, no cell borders and no
floor. Every figure has exactly the same height, the same scale and the feet on the same
horizontal line inside its cell, centered horizontally, with a wide magenta margin around it.
Each figure is about 33 pixels tall on the pixel grid.

The character: a base male character for a fitness game, a neutral template with no
distinguishing features. Young adult man of average height and average build, slightly athletic
but not muscular. Chibi proportions: large head about one third of the body height, short torso,
short legs, compact rounded hands, simple shoes. Symmetrical design, nothing on one side only.

Face: friendly neutral expression, small dark eyes with light grey whites, short simple eyebrows,
tiny nose shown by shading only, mouth closed or a 1-pixel neutral line. Clean-shaven, no beard,
no glasses, no freckles, no makeup.

Hair: short, neat, close-cropped dark brown hair, even length all around, no fringe, no part,
no spikes, hugging the head shape.

Skin: medium warm skin tone, with one shadow tone and one highlight tone.

Clothes: plain light heather grey crew-neck t-shirt with short sleeves, no logo, no print.
Plain mid slate grey jogger pants tapered at the ankle, no stripes. Plain white low-top sneakers
with light grey soles. No accessories: no cap, no headphones, no watch, no bag, no bottle.

Pose in every direction: the same relaxed neutral standing pose, weight on both feet, feet
slightly apart under the hips, arms relaxed at the sides with a small gap from the torso.
The north view shows only hair and the back of the neck, no face. East and west are exact
mirror images. The diagonal views are true three-quarter views, not copies of the side views.
```

Depois do prompt, acrescente o bloco de estilo de §2.

### Paleta

Se o Gemini fugir das cores, anexe uma imagem com as amostras da paleta de `prompts-pixellab.md` §2 ou cite os valores no prompt. O `build.py` leva cada pixel para a cor mais próxima de `PALETA`; se a geração nova mudar os tons, atualize `PALETA` no script, nunca o PNG.

### Conferência

Os mesmos itens de `prompts-pixellab.md` §2 ("Conferência da `rotations`"), e mais:

- As 8 figuras estão na ordem de §3: o `build.py` lê as células nessa ordem (`DIRECOES`).
- O fundo é só magenta, sem chão, sombra ou brilho em volta do personagem.
- A grade de pixels é uniforme e igual nas 8 figuras.
- Depois do `build.py`, confira `preview/rotacao-6x.png` e acerte olhos e sobrancelhas em `RETOQUES`, como hoje.

## 4. Animações

O Gemini só mantém o tamanho e a proporção do personagem quando parte de uma imagem dele. Por isso, toda animação é gerada **uma direção por vez**, a partir da imagem daquela direção, e cada prompt abaixo é completo: grade, âncora, quadros e estilo num bloco só, pronto para colar.

### Como gerar

1. Rode `python tira.py`. Ele grava `saida/referencia-gemini/<direcao>.png`: a pose parada de cada direção, ampliada 8× sobre magenta (384×512).
2. Abra uma conversa nova para cada animação e escolha a proporção indicada na seção dela. Anexe **só** a imagem de uma direção (nada de grade com as 8, porque o Gemini copia a grade e erra a quantidade de quadros) e cole o prompt sem mudar nada. A direção vem da imagem anexada, então o prompt é o mesmo nas 8 direções.
3. Na mesma conversa, siga com as outras direções na ordem `south`, `southeast`, `east`, `northeast`, `north`, `northwest`, `west`, `southwest`: anexe a próxima imagem e cole o mesmo prompt de novo.
4. Baixe em PNG, em resolução cheia. JPG deixa manchas que viram pixels soltos na redução. Salve em `fonte/gemini/<animacao>-<direcao>.png` (por exemplo `fonte/gemini/treinando-south.png`).

### Grade por quantidade de quadros

O personagem tem cerca de 0,6 de largura para 1 de altura. Uma linha só com 6 ou 8 quadros deixa as células estreitas demais, mesmo em 21:9, e o Gemini então espreme o personagem. Por isso:

| Quadros | Grade | Proporção |
|---:|---|---|
| 3 | 3 colunas × 1 linha | 21:9 |
| 4 | 2 colunas × 2 linhas | 1:1 |
| 6 | 3 colunas × 2 linhas | 3:2 |
| 8 | 4 colunas × 2 linhas | 3:2 |

### Conferência

- A quantidade de quadros e a grade são as do prompt.
- Quadros descritos como "exact copy of the reference" são iguais à imagem anexada.
- Os quadros intermediários mostram o movimento de forma clara, e não a pose parada com um tremor de 1 pixel.
- Depois da redução para 33 px (mesma escala da `rotations`), os pés ficam na linha 45, salvo nos quadros que levantam um pé ou saltam.

### Redução

`python gemini.py` lê todas as folhas de `fonte/gemini/` e grava `saida/gemini/<animacao>-<direcao>.png`: os quadros lado a lado, 64×64 cada, na escala da `rotations` (a altura do 1º quadro vira a altura de `fonte/<direcao>.png`), com os pés do 1º quadro na linha 45, a paleta fixa `PALETA` do `build.py` e o contorno de 1 px refeito. Confira `preview/gemini-<animacao>-<direcao>-6x.png`, que mostra a pose parada ao lado dos quadros.

Depois, importe a tira no Aseprite (pelo `pixel-plugin`), acerte o que faltar e exporte o `.aseprite` com as tags `Acao-direcao`, como em `prompts-pixellab.md` §3.

### `parado` — Idle_Breathing

4 quadros, grade 2×2, proporção 1:1.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 2 columns and 2 rows of equal cells, exactly 4 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background (#FF00FF)
everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell. All movements below are
measured in those pixel blocks. Do not draw any card, ball, weight or bottle.

ACTION: calm idle breathing, a seamless loop. Only the upper body moves, by exactly 1 block.
Frame 1: an exact copy of the reference image.
Frame 2: the head, the shoulders and the chest are 1 block higher than in the reference; the
arms follow the shoulders. The legs and the feet are exactly as in the reference.
Frame 3: an exact copy of frame 2.
Frame 4: an exact copy of the reference image.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `andar` — Walking

8 quadros, grade 4×2, proporção 3:2.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background (#FF00FF)
everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, as tall as in the reference, with the soles
of the feet on the same horizontal ground line in every cell. All movements below are
measured in those pixel blocks. Do not draw any card, ball, weight or bottle.

ACTION: casual relaxed walk at a normal pace, walking in place (the character does not travel
across the cell), a seamless loop. Small steps: a foot moves at most 3 blocks from where it is
in the reference. Each arm swings at most 2 blocks, always opposite to the leg on the same side.
The steps must be clearly visible, not a 1-block tremor.
Frame 1: an exact copy of the reference image: legs together, arms at the sides.
Frame 2: the right leg moves forward, the left arm swings forward, the right arm swings back.
The whole body is 1 block higher.
Frame 3: widest step: the right foot is forward, the left foot is back, both soles on the
ground line. The arms are at the end of their swing. The body is at the reference height.
Frame 4: the weight is on the right foot, the legs start to come together, the arms move back
toward the sides. The whole body is 1 block lower than in the reference.
Frame 5: legs together and arms at the sides, the same as frame 1.
Frame 6: the same as frame 2, mirrored: the left leg forward, the right arm forward, the left
arm back. The whole body is 1 block higher.
Frame 7: the same as frame 3, mirrored: the left foot forward, the right foot back.
Frame 8: the same as frame 4, mirrored: the weight on the left foot, the body 1 block lower.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `correr` — Running

8 quadros, grade 4×2, proporção 3:2.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background (#FF00FF)
everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, as tall as in the reference, with the soles
of the feet on the same horizontal ground line in every cell, except where a frame below
lifts the feet. All movements below are measured in those pixel blocks. Do not draw any card,
ball, weight or bottle.

ACTION: light athletic run in place, like a morning jog in the park, not a sprint, a seamless
loop. The character does not travel across the cell. In every frame both elbows are bent at a
right angle with the fists at the height of the lower chest, close to the body, and the torso
leans 1 block forward. A knee lifts at most 3 blocks; in the air the feet are at most 2 blocks
above the ground line. The strides must be clearly visible, not a 1-block tremor.
Frame 1: the reference pose with the elbows bent and the torso leaning 1 block forward, both
soles on the ground line.
Frame 2: the right knee lifts forward, the left leg pushes back, the left fist swings forward,
the right fist swings back. The whole body is 1 block higher.
Frame 3: both feet off the ground, 2 blocks above the ground line, widest stride. The whole
body is 2 blocks higher than in the reference.
Frame 4: the right sole lands on the ground line, the left leg starts to come forward. The
whole body is 1 block lower than in the reference.
Frame 5: the same as frame 1.
Frame 6: the same as frame 2, mirrored: the left knee forward, the right fist forward, the left
fist back.
Frame 7: the same as frame 3, mirrored: the left leg forward.
Frame 8: the same as frame 4, mirrored: landing on the left sole.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `treinando` — Jogging_In_Place

6 quadros, grade 3×2, proporção 3:2.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 3 columns and 2 rows of equal cells, exactly 6 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background (#FF00FF)
everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, as tall as in the reference, with the soles
of the feet on the same horizontal ground line in every cell, except the one foot that a frame
below lifts. The standing foot never goes below the ground line. All movements below are
measured in those pixel blocks. Do not draw any card, ball, weight or bottle.

ACTION: light jogging in place, warming up before a workout, a seamless loop. The feet stay
over the same spot, no travel at all. In every frame both elbows are bent at a right angle,
with the fists at the height of the lower chest, close to the body, like a jogger. The knee
lifts must be clearly visible, not a 1-block tremor. The face, the eyes and the head shape stay
exactly as in the reference; the head only moves up and down with the body.
Frame 1: the reference pose, but with both elbows bent as described. Both soles on the ground
line.
Frame 2: the right knee starts to lift: the right sole is 1 block above the ground line. The
left fist moves 1 block forward. The whole upper body is 1 block higher.
Frame 3: the right knee is at its highest: the right sole is 3 blocks above the ground line and
the right thigh points forward. The left fist is 2 blocks forward. The upper body stays 1 block
higher. The left sole stays on the ground line.
Frame 4: an exact copy of frame 1.
Frame 5: the same as frame 2, mirrored: the left knee starts to lift, the right fist moves
1 block forward.
Frame 6: the same as frame 3, mirrored: the left sole is 3 blocks above the ground line, the
right fist is 2 blocks forward, the right sole stays on the ground line.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `alongar` — Stretching

4 quadros, grade 2×2, proporção 1:1.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 2 columns and 2 rows of equal cells, exactly 4 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background (#FF00FF)
everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, with the soles of both feet on the same
horizontal ground line in every cell. Leave enough room above the head for the raised arms.
All movements below are measured in those pixel blocks. Do not draw any card, ball, weight
or bottle.

ACTION: a quick casual stretch while standing, played once. The feet do not move. The legs
stay exactly as in the reference.
Frame 1: both arms are raised straight out in front at shoulder height. The rest is the
reference pose.
Frame 2: both arms reach straight up above the head, with the hands together. The arms are
two separate thin vertical shapes, 2 blocks wide each, one on each side of the head, from
the shoulders up past the top of the hair; the hands meet about 6 blocks above the top of
the hair. The whole head, the hair and the face stay fully visible between the arms, with
the same size and shape as in the reference. The arms never cover or wrap around the head.
The body stretches 1 block taller.
Frame 3: the arms stay up as in frame 2. The upper body leans to one side by shifting, not
by rotating: the hips stay in place, the chest moves 1 block to the side and the head and
the raised arms move 2 blocks to the same side, keeping their upright shape. The head, the
face and the hair are not tilted or distorted. The legs stay vertical, as in the reference.
Frame 4: the upper body is straight again and the arms come down to shoulder height.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `olhar` — Looking_Around

4 quadros, grade 2×2, proporção 1:1.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 2 columns and 2 rows of equal cells, exactly 4 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background (#FF00FF)
everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction for the body, the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel of the reference
is one square block; every frame uses blocks of exactly that size. The character is centered
horizontally in each cell, as tall as in the reference, with the soles of both feet on the
same horizontal ground line in every cell. All movements below are measured in those pixel
blocks. Do not draw any card, ball, weight or bottle.

ACTION: looks around while standing, a little curious, played once. Only the head turns. The
body, the arms, the legs and the feet stay exactly as in the reference, and the head keeps its
size and its outline.
Frame 1: an exact copy of the reference image.
Frame 2: the head turns to the character's right: the eyes, the eyebrows and the nose move
1 block to that side, and the hair shifts to match.
Frame 3: the head turns to the character's left: the eyes, the eyebrows and the nose are
1 block to the other side of where they are in the reference.
Frame 4: an exact copy of the reference image.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `arremessar` — Throw_Card

6 quadros, grade 3×2, proporção 3:2. A carta sai no 4º quadro; o Jogo a desenha.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 3 columns and 2 rows of equal cells, exactly 6 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background (#FF00FF)
everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell. All movements below are
measured in those pixel blocks. Do not draw the card, and do not draw any ball, weight or
bottle: the hand is empty.

ACTION: tosses a small playing card overhand toward someone in front of them, playful and
confident, like flicking a note to a friend, played once. Only the right arm and the upper
body move. The legs and the feet stay exactly as in the reference.
Frame 1: an exact copy of the reference image.
Frame 2: wind-up: the right arm bends and lifts, the right hand goes up next to the right
shoulder. The upper body turns a little toward the right arm.
Frame 3: full wind-up: the right hand is behind and above the right shoulder. The upper body
leans 1 block back.
Frame 4: release: the right arm snaps forward, straight, at shoulder height, pointing in the
facing direction, with the hand open. The upper body leans 1 block forward.
Frame 5: follow-through: the right arm comes down across the front of the body. The upper body
is upright again.
Frame 6: the right arm returns almost to the side, nearly the reference pose.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `receber` — Hit_By_Card

6 quadros, grade 3×2, proporção 3:2. O impacto é no 3º quadro.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 3 columns and 2 rows of equal cells, exactly 6 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background (#FF00FF)
everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell. All movements below are
measured in those pixel blocks. Do not draw the card, no impact star, no motion lines, and no
ball, weight or bottle.

ACTION: gets hit in the chest by a small card thrown at them, comic surprise, not pain, played
once. The legs and the feet stay exactly as in the reference.
Frame 1: an exact copy of the reference image.
Frame 2: notices it coming: the head moves 1 block up, the eyes open wide, both arms lift
1 block away from the body.
Frame 3: impact: the upper body jolts 2 blocks back, the head tilts back, both arms flick
outward, 2 blocks away from the torso, with the hands open.
Frame 4: recovers: the upper body comes back to its place, the arms come back toward the sides.
Frame 5: a small shake of the head: the head moves 1 block to one side. The body is as in the
reference.
Frame 6: an exact copy of the reference image.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `aceitar` — Fist_Pump

4 quadros, grade 2×2, proporção 1:1. Termina com o punho no alto.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 2 columns and 2 rows of equal cells, exactly 4 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background (#FF00FF)
everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, with the soles of both feet on the same
horizontal ground line in every cell. Leave enough room above the head for the raised arm.
All movements below are measured in those pixel blocks. Do not draw any card, ball, weight
or bottle.

ACTION: accepts a challenge with confidence, played once. The feet do not move.
Frame 1: an exact copy of the reference image.
Frame 2: a short crouch: the knees bend and the whole body is 2 blocks lower. Both hands close
into fists at the waist.
Frame 3: the body is back at the reference height. The right arm goes straight up above the
head, with the fist closed. The left fist stays at the waist.
Frame 4: the same as frame 3, with a small confident nod: the head is 1 block lower and the
mouth is a small smile.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `recusar` — Shrug

3 quadros, grade 3×1, proporção 21:9. Termina no alto do gesto.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: one row of 3 equal cells, exactly 3 frames, read left to right. Nothing else in the
image: no extra frames, no numbers, no labels, no arrows, no grid lines, no cell borders, no
floor, no shadow. Solid flat magenta background (#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell. All movements below are
measured in those pixel blocks. Do not draw any card, ball, weight or bottle.

ACTION: shrugs to decline, relaxed and good-humored, no hard feelings, played once. The legs
and the feet stay exactly as in the reference.
Frame 1: an exact copy of the reference image.
Frame 2: both shoulders lift 1 block. The forearms come halfway up, with the elbows next to
the torso.
Frame 3: the shoulders stay 1 block up. The forearms point forward and out at waist height,
with the open palms facing up. The head tilts 1 block to one side, with a small easy smile.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `provocar` — Taunting

4 quadros, grade 2×2, proporção 1:1.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 2 columns and 2 rows of equal cells, exactly 4 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background (#FF00FF)
everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell. All movements below are
measured in those pixel blocks. Do not draw any card, ball, weight or bottle.

ACTION: a friendly taunt to a rival standing in front of them, like trash talk between friends
before a game, played once. Not aggressive: no fists, no fighting stance. The feet do not move.
Frame 1: an exact copy of the reference image.
Frame 2: points forward: the right arm extends straight at shoulder height in the facing
direction, with the index finger out. The upper body leans 1 block forward.
Frame 3: a quick bounce: the right hand comes back to the chest, the knees bend and the whole
body is 1 block lower.
Frame 4: points again as in frame 2, with a confident grin.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

### `comemorar` — Cheering

8 quadros, grade 4×2, proporção 3:2.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left to right,
top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no arrows,
no grid lines, no cell borders, no floor, no shadow, no confetti, no stars. Solid flat magenta
background (#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same facing
direction (do not turn the character), the same size, the same head size, the same body
proportions, the same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size. The
character is centered horizontally in each cell, with the soles of the feet on the same
horizontal ground line in every cell, except in the jump frames below. Leave enough room above
the head for the raised arms. All movements below are measured in those pixel blocks. Do not
draw any card, ball, weight or bottle.

ACTION: celebrates a goal reached, joyful, like finishing a workout streak, a seamless loop.
The jump is small: the feet go at most 3 blocks above the ground line and land back on the
same spot.
Frame 1: an exact copy of the reference image.
Frame 2: crouch: the knees bend, the whole body is 2 blocks lower, both fists come up to the
chest.
Frame 3: jump: both feet leave the ground, 2 blocks above the ground line. Both fists go
straight up above the head.
Frame 4: top of the jump: the feet are 3 blocks above the ground line, the fists are up, the
mouth is a happy open smile.
Frame 5: landing: both soles back on the ground line, the body 1 block lower than in the
reference, the fists still up.
Frame 6: the body is back at the reference height. Both fists pump down to the level of the
head.
Frame 7: both fists pump up again, straight above the head, happy open smile.
Frame 8: the fists come down to the shoulders. The legs are as in the reference.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark. No
fantasy, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

## 5. Derivar personagens

Para os personagens padrão, use a folha de `rotations` aprovada como referência e troque só uma ou duas regiões, com as regras de `prompts-pixellab.md` §5:

```text
Same character sheet, same 8 directions in the same order, same body, same size, same pose and
same proportions as the reference. Change only: <short curly black hair> and <a coral #FF684A
tank top>. Keep everything else identical: face, skin, pants, sneakers, outline and shading style.
Solid flat magenta background (#FF00FF).
```
