# Prompts do ChatGPT — animações dos esportes do `jpgma`

> As 12 animações dos esportes (`docs/decisoes/0082-esportes-e-animacoes-por-tipo.md`) do `jpgma`, geradas no ChatGPT como as do base masculino. Os prompts são os de `../base-masculino/prompts-chatgpt.md`, com as trocas de `prompts-gemini.md` para o personagem (cabelo, barba, roupa preta e antebraços na cor da pele). As regras de lá (grade, tamanho, 1º e último quadro como calibração, conferência, correções no mesmo chat) continuam valendo.

## 1. Como usar

1. `python referencia.py` grava `saida/referencia-gemini/<direcao>.png`: a pose parada de cada direção, ampliada 8× sobre magenta.
2. Um chat novo para cada animação. Anexe só a referência de uma direção e cole o prompt, na ordem `south`, `southeast`, `east`, `northeast`, `north` (`barra`: só `south`; `largada`: só `east`). O `esportes.py` espelha o oeste, o noroeste e o sudoeste.
3. Baixe em PNG e salve como `fonte/chatgpt/<animacao>-<direcao>.png`.
4. `python gemini.py fonte/chatgpt/*.png` reduz as folhas para tiras de 48×48 em `saida/gemini/`, na paleta do `jpgma` (os objetos usam as cores de `EXTRAS`).
5. `python esportes.py` acrescenta os quadros ao fim de `fonte/jpgma.aseprite` (uma vez só), e `python build.py` monta a folha de 225 linhas.

São 52 gerações, com as mesmas grades e tamanhos do base masculino.

**Nordeste de costas:** em `volei` e `manchete`, o ChatGPT vira o nordeste de perfil. Acrescente ao fim do prompt "IMPORTANT VIEW: the reference shows the character from behind, in a three-quarter back view (…) Do not turn the character to a side profile or toward the viewer", e, se ainda sair de perfil, peça no mesmo chat para redesenhar de costas. Para trocar os quadros de uma direção depois do `esportes.py`, o `import_image` do pixel-mcp substitui a cel do quadro.

## 2. Prompts

Cada prompt é completo: grade, personagem, objetos, ação e estilo num bloco só, pronto para colar.

### `musculacao`: Rosca alternada com halteres

- **Tipo:** Musculação. Treino ao vivo de Musculação: no lugar da pessoa ou ao lado do suporte de halteres (Enfeite).
- **Folha:** 8 quadros, grade 4×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 7 (6 quadros, 120 ms cada).
- **Direções:** 5 (south, southeast, east, northeast, north).

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell, unless a frame below says
otherwise.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: In frames 2 to 7 only: one small dumbbell in each hand: a short grey bar with a round coral
rubber plate at each end (#ff684a, shadow #c4432c), about 5 blocks long and 3 blocks tall. No
other object.

ACTION: standing alternating dumbbell biceps curls, a seamless loop from frame 2 to frame 7. The legs
and the feet never move. The elbows stay at the sides of the torso.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: both arms down at the sides, a dumbbell in each hand, held horizontally.
Frame 3: the right forearm comes up to horizontal (elbow bent 90 degrees); the left arm stays
down.
Frame 4: full curl: the right dumbbell reaches the right shoulder; the left arm stays down. The
head and the chest are 1 block higher.
Frame 5: both arms down again, exactly as frame 2.
Frame 6: the same as frame 3, mirrored: the left forearm horizontal, the right arm down.
Frame 7: the same as frame 4, mirrored: the left dumbbell at the left shoulder, the head and the
chest 1 block higher.
Frame 8: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```

### `ciclismo`: Pedalando

- **Tipo:** Ciclismo. Treino ao vivo de Ciclismo: dá voltas pela praça; com a bicicleta (Enfeite) no chão, é ela.
- **Folha:** 8 quadros, grade 4×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 7 (6 quadros, 80 ms cada).
- **Direções:** 5 (south, southeast, east, northeast, north).

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell. In frame 1 the soles of the feet are on the
ground line; in frames 2 to 7 the bottoms of the wheels are on that same ground line. The spokes
turn a little from frame to frame; the frame, the seat and the handlebar do not move.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: In frames 2 to 7 only: a small city bicycle seen from the same angle as the character: coral
frame (#ff684a, shadow #c4432c), two grey wheels (#5f6783) with near-black tyres and thin
spokes, a dark seat and a dark handlebar. In a side view the bicycle is about 30 blocks long; in
a front or back view it is seen almost end-on, one narrow wheel in front of the other. The
bicycle stays in the same place in every cell. No other object.

ACTION: riding the bicycle at a steady pace, a seamless loop from frame 2 to frame 7. The character sits
on the seat, both hands on the handlebar, looking ahead. The wheels touch the ground line in
every frame. Frames 2 to 7 are one pedal revolution in 6 steps of 60 degrees; the left foot is
always opposite the right foot.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: the right foot at the top of the pedal circle, the left foot at the bottom.
Frame 3: the right foot forward and down, the left foot back and up. The body is 1 block lower.
Frame 4: the right foot forward at mid height, the left foot back at mid height.
Frame 5: the right foot at the bottom, the left foot at the top.
Frame 6: the right foot back and up, the left foot forward and down. The body is 1 block lower.
Frame 7: the right foot back at mid height, the left foot forward at mid height.
Frame 8: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```

### `natacao`: Nado crawl com água na cintura

- **Tipo:** Natação. Treino ao vivo de Natação: no lugar da pessoa, com o anel de água; com a piscininha (Enfeite) no chão, o Avatar fica dentro dela, e a piscina cobre as pernas.
- **Folha:** 8 quadros, grade 4×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 7 (6 quadros, 110 ms cada).
- **Direções:** 5 (south, southeast, east, northeast, north).

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell. In frame 1 the soles of the feet are on the
ground line; in frames 2 to 7 the bottom edge of the water oval is on that same ground line, and
the head is 2 blocks lower than in frame 1.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: In frames 2 to 7 only: a small flat oval of water around the waist, seen from above like the
rest of the image: blue (#4aa8ff), a darker blue rim at the front (#2f7fd6) and a few 1-block
white foam pixels, about 24 blocks wide and 8 blocks deep. Small violet swimming goggles
(#a58cf4) over the eyes. No other object.

ACTION: front crawl (freestyle) swimming in place, standing waist-deep in the water, a seamless loop
from frame 2 to frame 7. Everything below the waist is hidden under the water. The water oval
keeps the same size and place in every frame; only its foam pixels shift by 1 block.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: the right arm reaches up and forward over the head; the left arm is under the water.
Frame 3: the right hand enters the water in front, with a small white splash.
Frame 4: the right arm pulls back under the water while the left arm rises out of it; the head
turns to the side to breathe.
Frame 5: the left arm reaches up and forward over the head; the right arm is under the water.
Frame 6: the left hand enters the water in front, with a small white splash.
Frame 7: the left arm pulls back under the water while the right arm rises out of it; the head
faces forward.
Frame 8: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```

### `yoga`: Sequência de yoga

- **Tipo:** Yoga. Treino ao vivo de Yoga: no lugar da pessoa ou em cima do tapete (Enfeite).
- **Folha:** 6 quadros, grade 3×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 5 (4 quadros, 400 ms cada).
- **Direções:** 5 (south, southeast, east, northeast, north).

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 3 columns and 2 rows of equal cells, exactly 6 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell, unless a frame below says
otherwise.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: Do not draw any object: no ball, no weights, no bottle, no mat, no bar, no water.

ACTION: a slow, calm yoga flow, a loop from frame 2 to frame 5. Smooth and balanced, no jumping.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: mountain pose: standing straight, both arms raised straight overhead, palms together.
Frame 3: tree pose: standing on the left leg, the right foot pressed against the inside of the
left knee, palms together in front of the chest.
Frame 4: tree pose with the palms together straight overhead.
Frame 5: warrior pose: the feet wide apart, the front knee bent, both arms stretched out
horizontally to the sides, looking past the front hand.
Frame 6: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```

### `calistenia`: Flexões

- **Tipo:** Calistenia. Treino ao vivo de Calistenia, enquanto espera a vez na barra da praça; com as paralelas (Enfeite) no chão, em cima delas.
- **Folha:** 6 quadros, grade 3×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 5 (4 quadros, 150 ms cada).
- **Direções:** 5 (south, southeast, east, northeast, north).

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 3 columns and 2 rows of equal cells, exactly 6 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
In frame 1 the soles of the feet are on the ground line. In frames 2 to 5 the hands and the toes
are on that same ground line, and the body is centered horizontally in the cell.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: Do not draw any object: no ball, no weights, no bottle, no mat, no bar, no water.

ACTION: push-ups on the floor, a seamless loop from frame 2 to frame 5. The body is a straight plank
from the head to the heels, lying in the facing direction of the reference: in a front view the
head is toward the viewer, in a back view away from the viewer, in a side view the body is
horizontal. The hands are under the shoulders and the toes are on the ground line.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: top of the push-up: arms straight.
Frame 3: halfway down: elbows bent at 90 degrees.
Frame 4: bottom: the chest 1 block above the floor, the elbows bent back.
Frame 5: halfway up: exactly as frame 3.
Frame 6: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```

### `barra`: Barra fixa

- **Tipo:** Calistenia. Treino ao vivo de Calistenia: a vez de cada um na barra da praça, de frente para quem vê. A barra é do cenário; o Avatar só fecha as mãos no lugar dela.
- **Folha:** 8 quadros, grade 4×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 7 (6 quadros, 120 ms cada).
- **Direções:** 1 (south); o build copia para as outras.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell. Frame 1 stands with the soles of the feet
on the ground line; frames 2 to 7 hang from the hands as described, with the hands always at the
same height.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: Do not draw the bar or any object: only the hands closed around an invisible horizontal bar.

ACTION: pull-ups, facing the viewer, a seamless loop from frame 2 to frame 7. The closed hands stay at
exactly the same position in every frame from 2 to 7: shoulder-width apart, 1 block above the
top of the head of frame 1. The knees are bent and the feet are crossed behind, so the feet
never touch the ground in frames 2 to 7.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: dead hang: the arms straight up, the head between the arms, the body hanging below the
hands.
Frame 3: pulling: the elbows half bent and pointing down and out, the body 4 blocks higher than
in frame 2.
Frame 4: top: the chin just above the hands, the elbows bent down at the sides, the body 8
blocks higher than in frame 2.
Frame 5: hold at the top: exactly as frame 4.
Frame 6: lowering: exactly as frame 3.
Frame 7: dead hang again: exactly as frame 2.
Frame 8: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```

### `volei`: Manchete sozinho, com bola

- **Tipo:** Vôlei. Treino ao vivo de Vôlei, sozinho: no lugar da pessoa ou ao lado da redinha (Enfeite).
- **Folha:** 8 quadros, grade 4×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 7 (6 quadros, 100 ms cada).
- **Direções:** 5 (south, southeast, east, northeast, north).

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell, unless a frame below says
otherwise.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: In frames 2 to 7 only: a volleyball about 5 blocks wide, with yellow (#ffd34e) and blue
(#4aa8ff) panels and a near-black outline. The ball always stays inside the cell. No other
object.

ACTION: solo volleyball practice: forearm passes (bumps) to himself, a seamless loop from frame 2 to
frame 7.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: ready stance: the knees bent, both arms together and straight in front of the hips (the
platform); the ball 8 blocks above the forearms.
Frame 3: the ball drops onto the forearms.
Frame 4: the knees straighten and the straight arms swing up to chest height; the ball leaves
the forearms. The body is 1 block higher.
Frame 5: the ball flies up, 6 blocks above the head; the arms come down.
Frame 6: the ball comes down to the height of the head; the arms return to the platform.
Frame 7: the ball 4 blocks above the forearms, in the ready stance of frame 2.
Frame 8: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```

### `manchete`: Manchete sem bola (passe)

- **Tipo:** Vôlei. Treino ao vivo de Vôlei com outra pessoa: os dois trocam passes. A bola é desenhada pela cena e toca os braços no 2º quadro.
- **Folha:** 6 quadros, grade 3×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 5 (4 quadros, 100 ms cada).
- **Direções:** 5 (south, southeast, east, northeast, north).

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 3 columns and 2 rows of equal cells, exactly 6 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell, unless a frame below says
otherwise.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: Do not draw any object: no ball, no weights, no bottle, no mat, no bar, no water. The game draws
the ball.

ACTION: a volleyball forearm pass (bump) without the ball, a seamless loop from frame 2 to frame 5.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: ready stance: the knees bent, both arms together and straight in front of the hips.
Frame 3: contact: the knees straighten and the straight arms swing up to chest height.
Frame 4: follow-through: the arms at shoulder height, the body 1 block higher.
Frame 5: back to the ready stance, as frame 2, with the arms 1 block lower.
Frame 6: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```

### `futebol`: Embaixadinhas

- **Tipo:** Futebol. Treino ao vivo de Futebol, sozinho e sem os cones (Enfeite) no chão.
- **Folha:** 9 quadros, grade 3×3, 1024×1024 (quadrada, 1:1). A animação usa os quadros 2 a 9 (8 quadros, 90 ms cada).
- **Direções:** 5 (south, southeast, east, northeast, north).

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 3 columns and 3 rows of equal cells, exactly 9 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell, unless a frame below says
otherwise.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: In frames 2 to 9 only: a football about 5 blocks wide, white with near-black patches and a
near-black outline. The ball always stays inside the cell. No other object.

ACTION: keepy-uppies: juggling the football on the feet, a seamless loop from frame 2 to frame 9. The
arms are a little open for balance. The supporting foot stays on the ground line.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: the right foot raised forward 2 blocks touches the ball, just above the ground line.
Frame 3: the ball pops up to knee height; the right foot goes back down.
Frame 4: the ball at waist height, the top of its arc.
Frame 5: the ball falls to knee height; the left foot starts to rise.
Frame 6: the left foot raised forward 2 blocks touches the ball, just above the ground line.
Frame 7: the ball pops up to knee height; the left foot goes back down.
Frame 8: the ball at waist height, the top of its arc.
Frame 9: the ball falls to knee height; the right foot starts to rise.

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```

### `chutar`: Chute sem bola (passe e chute a gol)

- **Tipo:** Futebol e Outro esporte. Futebol com outra pessoa (passes) e Outro esporte com o golzinho (Enfeite) no chão. A bola é desenhada pela cena e sai do pé no 4º quadro.
- **Folha:** 8 quadros, grade 4×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 7 (6 quadros, 80 ms cada).
- **Direções:** 5 (south, southeast, east, northeast, north).

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell, unless a frame below says
otherwise.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: Do not draw any object: no ball, no weights, no bottle, no mat, no bar, no water. The game draws
the ball.

ACTION: a short side-foot pass without the ball, a seamless loop from frame 2 to frame 7. The kick goes
in the facing direction of the reference.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: stance: the weight on both feet, the arms relaxed.
Frame 3: a small step: the left foot plants beside the place of the ball, the right foot behind.
Frame 4: the right leg swings back, the arms open for balance.
Frame 5: contact: the right foot swings forward low, next to the left foot, the inside of the
foot facing forward.
Frame 6: follow-through: the right foot 3 blocks forward and 1 block off the ground.
Frame 7: the right foot comes back down beside the left foot, as in frame 2.
Frame 8: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```

### `luta`: Sombra (boxe)

- **Tipo:** Luta. Treino ao vivo de Luta: no lugar da pessoa ou de frente para o saco de pancada (Enfeite).
- **Folha:** 8 quadros, grade 4×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 7 (6 quadros, 80 ms cada).
- **Direções:** 5 (south, southeast, east, northeast, north).

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell, as tall as in the reference, with the soles
of both feet on the same horizontal ground line in every cell, unless a frame below says
otherwise.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: Do not draw any object: no ball, no weights, no bottle, no mat, no bar, no water. The hands are
bare closed fists: no gloves, no wraps.

ACTION: shadowboxing in place, a seamless loop from frame 2 to frame 7, with a light bounce on the balls
of the feet. The punches go in the facing direction of the reference.
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: boxing guard: both fists up at the chin, the elbows in, the feet apart (one a little
forward), the knees soft.
Frame 3: jab: the lead arm punches straight forward at head height; the body is 1 block lower.
Frame 4: back to the guard of frame 2, the body 1 block higher (the bounce).
Frame 5: cross: the rear arm punches straight forward, the hips and the shoulders turn with it.
Frame 6: back to the guard of frame 2.
Frame 7: guard with the bounce: the body 1 block higher.
Frame 8: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic. The punches are shadowboxing training: no opponent,
no impact, no blood, no gloves, a friendly and focused mood.
Friendly and comic tone.
```

### `largada`: Largada no bloco

- **Tipo:** Corrida. Treino ao vivo de Corrida com o bloco de largada (Enfeite) no chão: toca uma vez, e depois o Avatar sai correndo em volta da praça. O bloco é do Enfeite.
- **Folha:** 8 quadros, grade 4×2, 1536×1024 (paisagem, 3:2). A animação usa os quadros 2 a 7 (6 quadros, 120 ms cada).
- **Direções:** 1 (east); o oeste sai espelhado e o build copia para as outras.

```text
Animation sheet of the character in the attached reference image, for a pixel art game.

LAYOUT: a grid of 4 columns and 2 rows of equal cells, exactly 8 frames, read left
to right, top to bottom. Nothing else in the image: no extra frames, no numbers, no labels, no
arrows, no grid lines, no cell borders, no floor, no shadow. Solid flat magenta background
(#FF00FF) everywhere.

CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same size, the same head size, the same body proportions, the
same colors, the same near-black outline and the same pixel size. One pixel
of the reference is one square block; every frame uses blocks of exactly that size.
The character is centered horizontally in each cell. In frame 1 the soles of the feet are on the
ground line; in frames 2 and 3 the hands, the feet (and the back knee in frame 2) are on that
same ground line; in frames 4 to 7 the supporting foot is on it.
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
All movements below are measured in those pixel blocks. The character is about 33 blocks tall.

OBJECTS: Do not draw any object: no ball, no weights, no bottle, no mat, no bar, no water. The game draws
the starting blocks.

ACTION: a sprint start, played once, from frame 2 to frame 7, in the facing direction of the reference
(to the right).
Frame 1: an exact copy of the reference image, with no object. It is only the size reference.
Frame 2: on your marks: kneeling, the back knee on the ground, both hands on the ground line in
front of the shoulders, the head down.
Frame 3: set: the hips rise above the shoulders, both knees bent, the hands still on the ground.
Frame 4: go: the body explodes forward and up, the back leg drives, the opposite arm swings
forward.
Frame 5: first step: the body low and leaning forward, the back knee comes forward.
Frame 6: second step: the body rising, the arms pumping.
Frame 7: an upright running stride, like the first frame of a run.
Frame 8: an exact copy of frame 1 (only closes the grid).

STYLE: contemporary urban pixel art for a cozy, playful fitness game, low top-down view.
Every pixel is a crisp square block, all blocks the same size. Single-color near-black outline
(#0e071b), flat shading with 3 tones per material, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no rim light, no cast shadow, no text, no watermark.
No fantasy, no armor, no weapons, no magic, no combat poses.
Friendly and comic tone.
```
