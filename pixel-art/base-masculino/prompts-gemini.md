# Prompts do Gemini — personagem base masculino

> Prompts para gerar no Gemini (Nano Banana) o personagem base masculino **neutro** em 8 direções, a mesma peça descrita em `prompts-pixellab.md` (identidade, paleta e regras de §1 e §2 de lá continuam valendo). A imagem gerada é só a **fonte**: ela vai para `fonte/base.png`, e o `build.py` separa as 8 direções, as reduz para 33 px de altura e grava `saida/rotations/<direcao>.png`. Nunca copie a imagem gerada direto para `saida/` ou `webapp/public/`.
>
> **Divisão de trabalho:** o Gemini faz bem uma folha de poses paradas e mal uma animação quadro a quadro. Use o Gemini para a `rotations` (§3) e o PixelLab para as animações (`prompts-pixellab.md` §3 e §4), partindo da `rotations` que o `build.py` gerou. §4 traz um prompt de tira de animação para quando o PixelLab não der conta de uma ação.

## 1. Como usar o Gemini

- **Onde:** de preferência o Google AI Studio, com o modelo de imagem do Gemini. O app do Gemini pode pôr uma marca visível num canto da imagem, por cima de um personagem.
- **Proporção:** 3:2, deitada (o `build.py` foi feito para 1536×1024). A grade tem 4 colunas e 2 linhas.
- **Fundo:** magenta chapado `#FF00FF`. O Gemini não gera transparência, e o magenta não existe no personagem. **Atenção:** o `build.py` atual separa o personagem de um fundo escuro com brilho (o da `fonte/base.png` de hoje). Para uma fonte em magenta, ele precisa tratar o magenta como fundo antes de procurar o contorno.
- **Referência:** anexe `../jpgma/saida/` ou a `saida/rotations/` atual, montada numa imagem só, como referência de proporção e de vista. Diga que ela mostra o corpo e o ângulo, e não o rosto, o cabelo ou a roupa. Não anexe arte de terceiros: as folhas padrão são CC0.
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

## 4. Tira de animação (opcional)

Use só quando o PixelLab não der conta de uma ação. Gere **uma direção por vez**, anexando a imagem dessa direção em `saida/rotations/` como referência do personagem. A ação vem da tabela e dos prompts de `prompts-pixellab.md` §4, com a mesma quantidade de quadros.

```text
Animation strip of the character in the attached reference image, facing <south>, in
<6> frames placed side by side in one row of equal cells, left to right, on a solid flat
magenta background (#FF00FF), with no grid lines and no floor. Same character, same body,
same size, same colors, same outline and same proportions as the reference in every frame.
The feet stay on the same horizontal line in every frame<, and do not move>.
The first and last frames match the reference standing pose.
Action: <paste the action text from prompts-pixellab.md §4, without the frame count line>.
Do not draw any card, ball, weight or bottle.
```

Depois do prompt, acrescente o bloco de estilo de §2. A proporção acompanha a quantidade de quadros: 21:9 para 6 a 8 quadros e 16:9 para 3 ou 4.

Limites: o Gemini não garante que o mesmo quadro saia igual nas 8 direções, nem um ciclo sem salto. Por isso, a tira gerada serve de esboço: importe os quadros no Aseprite (pelo `pixel-plugin`), acerte-os e exporte o `.aseprite` com as tags `Acao-direcao`, como em `prompts-pixellab.md` §3.

## 5. Derivar personagens

Para os personagens padrão, use a folha de `rotations` aprovada como referência e troque só uma ou duas regiões, com as regras de `prompts-pixellab.md` §5:

```text
Same character sheet, same 8 directions in the same order, same body, same size, same pose and
same proportions as the reference. Change only: <short curly black hair> and <a coral #FF684A
tank top>. Keep everything else identical: face, skin, pants, sneakers, outline and shading style.
Solid flat magenta background (#FF00FF).
```
