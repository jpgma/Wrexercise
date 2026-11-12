# Prompts do ChatGPT — Enfeites

> Prompts para gerar no ChatGPT (geração de imagem) os 19 Enfeites (`docs/gamificacao.md` §9.1), apoiados no chão da praça e animados (`docs/decisoes/0050-enfeites-no-chao.md`). As imagens geradas são só a **fonte**: elas vão para `fonte/`, e o `build.py` recorta cada objeto, o reduz à escala do mestre da praça, o ajusta à paleta `wrexercise` e monta o atlas `saida/enfeites.png` (4 quadros × 19 linhas de 40×40), copiado para `webapp/public/`. Nunca copie uma imagem gerada direto para `saida/` ou `webapp/public/`.
>
> **Estado:** a folha-base está em `fonte/base.png` e já está no atlas; as tiras ainda não foram geradas, então os Enfeites ficam parados. Sem `fonte/base.png`, o `build.py` usa os ícones antigos de `fonte/folha.png` (do Gemini).

## Como usar

1. Gere a folha-base (prompt 1 + bloco de estilo) e salve em `fonte/base.png`.
2. Rode `python build.py --recortes`. Ele grava em `preview/recorte-NN-<conquista>.png` cada objeto já reduzido, ampliado 6× sobre magenta.
3. Para cada objeto, abra **um chat novo**, anexe o recorte e use o prompt 2 com a linha da tabela. Salve em `fonte/tira-NN-<conquista>.png` (o `NN` e a conquista são os do nome do recorte).
4. Rode `python build.py` de novo e cole a lista impressa em `webapp/src/features/jogo/enfeitesNaPraca.ts`. Confira `preview/animado.gif`.

**Sem tira:** 04, 05, 06 (kettlebells), 07 (troféu), 08 (suporte de halteres) e 15 (paralelas). O único movimento deles é um brilho, e o `build.py` o desenha por código a partir da base (`BRILHO`): num objeto de 13 px, o brilho do ChatGPT some na redução.

Uma tira que falta não quebra nada: o objeto fica parado no quadro da base. Ao trocar a folha-base, gere de novo os recortes e as tiras: a escala de cada tira vem do objeto na base.

- **Fundo:** magenta chapado `#FF00FF`. O ChatGPT não garante transparência, e o magenta não existe na paleta, então o `build.py` o remove sem ambiguidade.
- **Tamanhos:** a base é paisagem (1536×1024); as tiras são quadradas (1024×1024).
- **Referência de estilo:** para refazer a folha num chat novo sem perder o estilo, anexe a folha anterior e comece o prompt com "Use the attached sheet only as the style reference."
- **Iteração:** corrija na mesma conversa ("keep everything else identical, only redraw cell 10, the dog"). Gerar do zero muda todos os objetos.
- **Licença:** não anexe arte de terceiros: os Enfeites são CC0.

## Bloco de estilo (cole no fim de **todo** prompt)

```text
Style: contemporary urban pixel art for a cozy, playful fitness game, seen in 3/4 top-down view
(front face visible, top surface slightly visible, like objects standing on a city square floor).
Real pixel art enlarged with nearest-neighbor: every art pixel is a crisp 6×6 square block, the
same block size everywhere in the image. 1-pixel near-black outline (#14161f) around each object.
Flat colors with simple shading: one light, one base and one shadow tone per material, light from
the top left. Bold, simple silhouettes that still read at small size.
No anti-aliasing, no blur, no gradients, no glow, no cast shadow on the ground, no ground plane,
no text, no numbers, no letters, no logos, no watermark, no labels, no grid lines, no borders.
Palette only: asphalt greys #14161f #363c52 #5f6783 #8b92ab #b3b8cb, chalk #f5f1e8 #d6d0c0,
coral #c4432c #ff684a #ff9c80, violet #4b35a6 #7957e8 #a58cf4, yellow #d9a02b #ffd34e,
lime #67a24b #a8d978, bronze #8d5a36 #c98a5a, brown #6e4125 #a8693f, silver #d4d8e4,
water blue #4aa8ff.
Background: solid flat magenta #FF00FF everywhere outside the objects.
```

## Prompt 1 — folha-base (1536×1024, paisagem)

```text
Create a 1536×1024 landscape sprite sheet of 19 pixel-art objects that stand on the floor of a
small urban square, arranged in a grid of 5 columns and 4 rows of equal cells (each cell about
307×256 px), read left to right, top to bottom. There are EXACTLY 19 objects: 5 in each of
rows 1, 2 and 3, and 4 in row 4. The last cell (row 4, column 5) stays empty magenta. Do not
skip, merge or reorder any object.
Each object is centered horizontally in its cell, with its base resting on an invisible line near
the bottom of the cell, and a wide magenta margin so no two objects touch.

All objects share ONE scale: a standing adult person would be 40 art pixels tall. The sizes below
are width × height in art pixels (each art pixel = 6×6 screen pixels). Respect them so the objects
keep real-world proportions relative to each other.

Row 1:
1. (16×20) A small green sprout with two leaves in a coral clay pot.
2. (20×28) The SAME coral pot with a taller lime-green plant and three small coral flowers.
3. (28×40) The SAME coral pot, larger, with a small round tree: brown trunk, lime-green crown,
   a few coral fruits.
4. (16×18) A bronze kettlebell sitting on the floor.
5. (19×21) The SAME kettlebell shape, slightly bigger, in silver.

Row 2:
6. (22×25) The SAME kettlebell shape, bigger again, in gold with a small chalk highlight.
7. (20×38) A gold trophy cup with two handles standing on a short square grey concrete pedestal.
8. (28×20) A low grey floor rack holding two pairs of dumbbells.
9. (32×14) Athletics starting blocks on the floor, with a pair of coral running shoes beside them.
10. (22×22) A small brown dog sitting, facing 3/4 toward the viewer, with a coral collar.
    No leash.

Row 3:
11. (36×24) A bicycle in side view parked on its kickstand: grey wheels, coral frame, dark seat.
12. (36×18) A small round inflatable kiddie pool (coral and chalk stripes) filled with blue
    water, with a chalk-and-coral ring buoy floating in it.
13. (34×26) A small street football goal (mini goal) with a chalk net and grey posts, and a
    chalk-and-dark football resting at its foot.
14. (32×12) A violet yoga mat half unrolled flat on the floor, the rolled part at one end.
15. (28×14) A pair of low grey parallettes (calisthenics bars) side by side.

Row 4:
16. (20×24) A rooster standing: chalk-white body, coral comb and wattle, yellow beak and legs,
    dark violet tail feathers.
17. (14×40) A city street lamp post: dark grey pole on a base, lamp head with the glass OFF
    (light grey, not lit).
18. (18×26) A portable party speaker standing upright on two small wheels, dark grey body,
    one big round woofer, a small coral light, a pull-up handle.
19. (22×18) A violet gym duffel bag lying on the floor with coral straps and a chalk water
    bottle leaning against it.
```

(cole o bloco de estilo)

## Prompt 2 — tira de animação (1024×1024, um chat novo por objeto, anexando `recorte-NN-*.png`)

Modelo: troque `{NOME}` e `{MOVIMENTO}` pela linha da tabela abaixo.

```text
The attached image is one pixel-art object on a magenta background: {NOME}.
Create a 1024×1024 image with a 4-frame idle animation of EXACTLY this object, in a 2×2 grid of
equal 512×512 cells: frame 1 top-left, frame 2 top-right, frame 3 bottom-left, frame 4
bottom-right. No borders or lines between cells.

Frame 1 is a faithful copy of the attached object. In every frame the object keeps the same
pixel scale (6×6 blocks per art pixel), the same size, the same colors and the same position in
its cell, centered horizontally, with its base on the same line near the bottom of the cell.
Only this part moves: {MOVIMENTO}
Everything else stays pixel-identical across the 4 frames. The motion is small (1 to 2 art
pixels) and loops smoothly from frame 4 back to frame 1.
```

(cole o bloco de estilo)

| NN | `{NOME}` | `{MOVIMENTO}` |
|---:|---|---|
| 01 | a sprout in a coral pot | the two leaves sway: 1 still, 2 lean right 1 px, 3 still, 4 lean left 1 px. |
| 02 | a flowering plant in a coral pot | the stem and flowers sway: 1 still, 2 lean right 1 px, 3 still, 4 lean left 1 px; the pot does not move. |
| 03 | a small tree in a coral pot | the crown sways: 1 still, 2 crown shifts right 1 px, 3 still, 4 crown shifts left 1 px; one fruit glints chalk in frame 2. |
| 04 | a bronze kettlebell | a light glint slides across it: 1 none, 2 short chalk streak on the upper left, 3 streak at the center, 4 streak on the right fading. |
| 05 | a silver kettlebell | the same glint as a bronze kettlebell: 1 none, 2 upper left, 3 center, 4 right fading. |
| 06 | a gold kettlebell | the glint (1 none, 2 upper left, 3 center, 4 right) plus a 1-pixel chalk sparkle above the handle in frames 2 and 4. |
| 07 | a gold trophy on a concrete pedestal | the cup glints: 1 none, 2 streak on the left of the cup, 3 center, 4 a 2-pixel sparkle above a handle; the pedestal never changes. |
| 08 | a dumbbell rack | a glint runs along the dumbbells: 1 none, 2 on the left pair, 3 on the right pair, 4 none. |
| 09 | starting blocks with running shoes | the shoelaces flutter: 1 rest, 2 lace ends lift 1 px, 3 rest, 4 lace ends shift right 1 px. |
| 10 | a sitting dog with a collar | the tail wags and it breathes: 1 tail left, 2 tail up, 3 tail right, 4 tail up; in frames 2 and 4 the chest rises 1 px. |
| 11 | a parked bicycle | the front wheel turns slowly: the spokes rotate a little each frame (1, 2, 3, 4), and the frame and the rear wheel stay still. |
| 12 | a kiddie pool with a floating buoy | the water shimmers and the buoy bobs: the light ripples on the water move each frame, and the buoy is 1 px lower in frames 2 and 3. |
| 13 | a mini football goal with a ball | the ball rocks and the net ripples: 1 rest, 2 ball rolls 1 px left, 3 rest, 4 ball rolls 1 px right; the net bulges 1 px in frames 2 and 4. |
| 14 | a half-unrolled yoga mat | the mat rolls up and unrolls slightly, like a breath: 1 rest; 2 the roll moves 1 px toward the flat end, which gets 1 px shorter, and the roll turns a little (the spiral at its end rotates); 3 the roll moves 2 px toward the flat end, the flat part 2 px shorter; 4 back to 1 px, as in frame 2. The roll keeps its size; only the flat part gets shorter and longer. |
| 15 | a pair of parallettes | a glint runs along the bars: 1 none, 2 left bar, 3 right bar, 4 none. |
| 16 | a standing rooster | the head bobs: 1 neutral, 2 head forward 1 px and down 1 px, 3 neutral, 4 head up with the beak open and the tail feathers fluffed. |
| 17 | a street lamp post | frame 1 lamp OFF exactly as attached; frames 2, 3 and 4 the glass is lit yellow #ffd34e with a chalk core, and the brightness flickers slightly (2 full, 3 one row dimmer #d9a02b, 4 full). The pole never changes. |
| 18 | a portable party speaker | it pulses to music: 1 rest, 2 the woofer cone pushes out 1 px and the coral light is on, 3 rest, 4 the woofer pushes out and the whole body squashes down 1 px. |
| 19 | a gym duffel bag with a water bottle | a light glint slides down the water bottle: 1 none, 2 top, 3 middle, 4 bottom; the bag stays still. |

## Conferência antes de salvar em `fonte/`

- Base: 19 objetos na ordem acima, 5 por linha nas três primeiras e 4 na última (os três kettlebells são as células 4, 5 e 6, esta já na linha 2), a última célula vazia, só magenta em volta, sem texto e sem chão desenhado. Os três vasos e os três kettlebells têm a mesma forma.
- Proporções: o poste e a árvore são os mais altos; a muda e o kettlebell de bronze, os menores.
- Tira: o quadro 1 é igual ao recorte anexado, a base fica na mesma linha nos 4 quadros e só a parte descrita se mexe.
- Se uma tira sair desalinhada, peça no mesmo chat: "keep frame 1 identical to the attachment and redraw frames 2–4 with the object's base on exactly the same line".

## O que o `build.py` faz com as fontes

1. Folha-base: separa as manchas que não são magenta e junta as que quase se encostam (o bloco de largada e os tênis) num objeto. Lê os objetos em ordem de leitura (linhas pela altura, da esquerda para a direita), e não pela grade: o ChatGPT às vezes põe 6 objetos numa linha. Se não achar exatamente 19, para com erro. Reduz **todas com a mesma escala**, pela cor mais comum de cada bloco, para que a maior ocupe os 40 px; assim os objetos mantêm as proporções reais. Cada objeto fica centrado e apoiado na base da célula.
2. Tira: divide em 2×2 e reduz os 4 quadros com a escala que deixa o quadro 1 com a altura do objeto na base. Cada quadro mantém a posição relativa ao quadro 1 (um rabo que balança não arrasta o corpo), e o quadro 1 é alinhado ao objeto da base.
   Se o quadro 1 da tira não combinar com os outros, ponha o Enfeite em `ORDEM_DA_TIRA` com a ordem dos quadros a usar (por exemplo, 2, 3, 4, 3).
   Se a parte que devia ficar parada (um vaso, uma base) tremer entre os quadros, ponha o Enfeite em `PARADO_EMBAIXO` no `build.py`: as linhas de baixo de todos os quadros passam a ser as do quadro 1.
3. Troca cada cor pela mais próxima da paleta `wrexercise` (e das cores do script); o magenta vira transparente.
4. Grava `saida/enfeites.png`, copia para `webapp/public/enfeites.png` e grava `preview/folha-4x.png` e `preview/animado.gif`.
