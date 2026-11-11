# Prompts do Gemini — praça em ¾

> Prompts para gerar no Gemini (Nano Banana) a praça do modo Jogo vista em ¾, no mesmo ângulo dos avatares (`docs/gamificacao.md` §1.3, §3.1 e §4.4). As imagens geradas são só a **fonte**: elas vão para `fonte/`, e o `build.py` as reduz, as ajusta à paleta `wrexercise` e grava `saida/`. Nunca copie uma imagem gerada direto para `saida/` ou `webapp/public/`.
>
> **Estado:** as três fontes de §5 estão em `fonte/` e foram aceitas como vieram: um terraço com a porta da escada à esquerda, a barra no fundo e o oval já pintado no chão. A paleta `wrexercise` esfria a mureta e os caixotes, que vieram em tons quentes.

## 1. Por que mudar

Os avatares são desenhados em ¾ (de frente, um pouco de cima), e a praça antiga foi desenhada quase de cima. Na praça nova:

- o fundo (prédios, céu, mureta ou fachada) aparece de frente, na faixa de cima;
- o chão aparece achatado, e a elipse dos avatares fica larga e baixa: quem está na frente cobre parte de quem está atrás;
- a borda de baixo é aberta (é o lado da câmera), e a borda do fundo tem uma porta ou passagem. Na entrada do dia, ninguém atravessa parede: quem fica na frente sobe pela borda de baixo, e quem fica atrás sai pela porta;
- os objetos que ficam no chão (barra, banco, floreira…) viram camadas separadas, ordenadas pelo y da base, como os avatares. Assim, alguém pode passar atrás deles.

## 2. Como usar o Gemini

- **Onde:** de preferência o Google AI Studio, com o modelo de imagem do Gemini. O app do Gemini pode pôr uma marca visível num canto da imagem.
- **Proporção:** 4:5, em pé. O `build.py` reduz a imagem para 180×225 e corta as 25 linhas de cima (só céu). Por isso, o prompt deixa os 11% de cima sem nada importante.
- **Referência:** anexe só o primeiro quadro de `pixel-art/base-masculino/preview/gemini-parado-south-6x.png` (o avatar parado, de frente; recorte os 288×288 px da esquerda). Com os 5 quadros, o Gemini lê 5 pessoas. O prompt já diz que ele é só para o ângulo e a escala. **Não** anexe a praça antiga: ela puxa a vista de cima. Não anexe arte de terceiros: a praça é CC0.
- **Uma conversa, três passos:** o passo A explora a cena; os passos B e C são edições curtas da cena escolhida, na mesma conversa. Não gere do zero entre eles, porque as três imagens precisam se sobrepor pixel a pixel.
- **Explorar:** no passo A, gere várias vezes, com o mesmo prompt ou com uma das sementes de §4. Guarde as melhores e escolha uma antes de seguir.
- **Salvar:** baixe cada PNG em resolução cheia, com os nomes de §5.

## 3. Bloco de estilo

Cole este bloco no fim de toda geração e de toda edição:

```text
Style: contemporary urban pixel art for a cozy, playful fitness game. Low resolution pixel art,
as if drawn on a 180 by 225 pixel canvas and enlarged 5 times with nearest-neighbor: every
pixel is a crisp square block of the same size, aligned to one grid. No anti-aliasing, no blur,
no gradients, no noise, no photographic texture, no painterly strokes. Flat colors with simple
shading: one light, one base and one shadow tone per material, light from the top left.
Single-color near-black outline (#14161f) on standing objects, none on the floor.
Palette: asphalt greys #14161f #363c52 #5f6783 #8b92ab #b3b8cb, chalk #f5f1e8 #d6d0c0,
coral #c4432c #ff684a #ff9c80, violet #4b35a6 #7957e8 #a58cf4, yellow #d9a02b #ffd34e,
lime #67a24b #a8d978.
No fantasy, no medieval, no grass fields, no stones, no torches. No text, no letters, no
numbers, no logos, no signature, no watermark.
```

## 4. Prompts

### Passo A — a cena (explorar)

Salve a escolhida em `fonte/praca.png`.

```text
Design a small urban outdoor spot where a group of friends meets to train: a street court,
a rooftop, a corner of a city square, a schoolyard, a pier, a parking deck, an alley
court, or anything else you find more interesting. Surprise me with the setting, the
layout and the small details that give the place character. Portrait 4:5, filling the
whole image.

Camera: three-quarter top-down view, the same angle as the attached character (attached
only for the camera angle and the scale, do not draw it). The camera looks down at about
40 degrees, like a classic 16-bit RPG town: the floor is foreshortened, and every vertical
surface shows its front face. Orthographic, no vanishing point, no fisheye, no change of
size with depth. A standing person in this scene is about 17% of the image width tall.

Layout, measured from the top of the image:
- The sky is not drawn: every pixel of sky is solid flat magenta (#FF00FF), with no clouds,
  no sun, no birds and no gradient. The game replaces it with its own background. Nothing
  else in the image uses magenta.
- 0% to 11%: only magenta sky. This band is cropped away later.
- 11% to 38%: the back of the space, seen from the front. A wall, a fence, a facade, a
  low parapet with the city skyline behind it, or anything that fits the setting. Its top
  outline against the magenta sky is clean and readable. Its foot meets the floor in a
  straight horizontal line at about 38% of the image height.
- In that back boundary, one door, gate or opening that people can walk out of (a
  stairwell door, a gate in a fence, a gap between buildings...). Its bottom sits on the
  foot line. It is about 1.3 times as tall as a person, and its center is in the left
  third or in the right third of the image, not in the middle. Draw it closed.
- Leave one plain stretch of the back boundary, about as wide as a person, with nothing on
  it: the game hangs its own small lamp there.
- 38% to 100%: the floor. The left and right sides of the space are closed by a low wall,
  a fence or a railing that stays within the outer 6% of each side.
- The bottom edge is open: the floor simply continues out of frame toward the viewer.
  No wall, fence or railing along the bottom edge.

Ten small characters will be placed later on a wide horizontal oval on the floor: centered
horizontally, from 11% to 89% of the image width, and from 48% to 96% of the image height.
They will also walk in from the bottom edge and out of the door to reach it.
- Keep the inside of that oval, the strip between the oval and the door, and the strip
  between the oval and the bottom edge open and walkable: no objects there.
- The floor is calm and low in contrast, mostly dark blue-grey, so small dark characters
  still read clearly on it. Markings, paint and texture are welcome if they stay subtle.
- Put the interesting things in the back boundary, along the side walls and in the four
  corners of the floor outside the oval. The bottom corners may have one low object each,
  partly cut by the image edge.
- Somewhere outside the oval, a fixed calisthenics pull-up bar standing on the floor,
  fully visible, with room under it for a person to hang from it later. Its exact form and
  position are up to you.
- At most one discreet piece of graffiti or painted art, and no readable text anywhere.
- No people, no animals, no loose clutter on the open floor. No lamps or light fixtures:
  the game adds its own.
```

Depois do prompt, acrescente o bloco de estilo de §3.

Sementes opcionais, uma por geração, antes do bloco de estilo:

```text
Mood: a few lit windows in the buildings behind.
Mood: fresh and quiet, long soft shadows.
Mood: right after rain, a few small puddles near the side walls.
Mood: lively neighborhood, signs of daily life beyond the back boundary.
Mood: minimal and graphic, bold court paint, very few objects.
```

### Passo B — a porta aberta

Na mesma conversa, com a cena escolhida. Salve em `fonte/praca-porta-aberta.png`.

```text
Keep the image exactly the same, pixel for pixel. Change only the door (or gate) in the
back boundary: draw it open, with a dark inside where a person could step out. Same size,
same position, same outline. Nothing else changes.
```

Depois do prompt, acrescente o bloco de estilo de §3.

### Passo C — os objetos soltos

Na mesma conversa, a partir da cena com a porta **fechada**. Salve em `fonte/praca-objetos.png`.

```text
Keep the image exactly the same size and keep every object exactly where it is, pixel for
pixel. Then paint everything that is not a standalone object standing on the floor in
solid flat magenta (#FF00FF): the sky, the buildings, the back boundary, the door, the
side walls, the floor, the floor markings and the shadows. Standalone objects stay
untouched: the pull-up bar, benches, planters, boxes, bags, speakers, cones, bikes and
anything similar that stands on the floor. No new objects, no outlines added, no
magenta inside the objects.
```

Depois do prompt, acrescente o bloco de estilo de §3.

## 5. Fontes e conferência

| Arquivo | Passo | Uso |
|---|---|---|
| `fonte/praca.png` | A | o fundo inteiro, com os objetos; vira `saida/praca.png` |
| `fonte/praca-porta-aberta.png` | B | só o recorte da porta, que vira o quadro "aberta" |
| `fonte/praca-objetos.png` | C | cada mancha fora do magenta vira um objeto, desenhado por cima dos avatares que estão atrás dele |

Antes de salvar, confira:

- A grade de pixels é uniforme: todo bloco tem o mesmo tamanho, sem borrão nem degradê.
- O ângulo combina com o avatar anexado: paredes e objetos mostram a frente, e nada parece visto de cima.
- O céu é todo magenta chapado, sem nuvem nem degradê, e nada fora do céu usa magenta.
- Os 11% de cima podem ser cortados sem perder nada.
- O pé da parede do fundo é uma linha reta, perto de 38% da altura.
- A porta fica num terço lateral, com a base no pé da parede.
- A elipse larga, a faixa até a porta e a faixa até a borda de baixo estão livres.
- A borda de baixo é aberta, e as laterais ficam nos 6% de cada lado.
- O chão é calmo. Veja a imagem reduzida para 360×400: um avatar escuro precisa se destacar.
- Há uma barra de calistenia fora da elipse (§9.3).
- Não há letras, logo, pessoa, animal, luminária ou tralha no meio do chão.
- Nos passos B e C, a imagem se sobrepõe à do passo A: abra as duas em camadas e alterne. Se algo se moveu, peça a correção numa edição curta ("the bench moved, put it back exactly where it was").

## 6. O que o `build.py` vai fazer com as fontes

1. Troca cada pixel pela cor mais próxima da paleta `wrexercise` (mais o magenta) e reduz pela cor mais comum de cada bloco para 180×225. Depois, corta as 25 linhas de cima.
2. Torna o magenta transparente: o céu é o fundo do aplicativo, e cada período do dia continua tingindo só a praça. As fileiras mais altas do fundo (prédios distantes) somem num pontilhado em degraus, sem borrão, para a silhueta não terminar num corte seco.
3. Desenha a assinatura "WX" (coral e violeta) num trecho liso da cena.
4. Grava `saida/praca.png` (180×200, RGBA), o fundo.
5. Recorta a porta aberta de `praca-porta-aberta.png` e cada objeto de `praca-objetos.png`, numa folha `saida/praca-objetos.png`, com a posição e o y da base de cada recorte. A cena desenha cada objeto por cima do fundo, ordenado pela base, como os avatares.
6. Grava `preview/praca-2x.png`. Copie as saídas para `Wrexercise/webapp/public/` (`docs/avatar-kit.md`).
