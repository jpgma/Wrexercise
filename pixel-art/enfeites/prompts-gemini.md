# Prompts do Gemini — Enfeites

> Prompt para gerar no Gemini (Nano Banana) os 19 Enfeites (`docs/gamificacao.md` §9.1) numa folha só. A imagem gerada é só a **fonte**: ela vai para `fonte/folha.png`, e o `build.py` recorta cada objeto, o reduz para 32×32, o ajusta à paleta `wrexercise` e grava `saida/enfeite-<conquista>.png`. Nunca copie a imagem gerada direto para `saida/` ou `webapp/public/`.
>
> **Estado:** a primeira folha está em `fonte/folha.png`, e o `build.py` a lê. Os 19 Enfeites desta folha já estão em `webapp/public/`. Sem a fonte, o script amplia 2× o desenho de 16×16 feito por código.

## 1. Como usar o Gemini

- **Onde:** de preferência o Google AI Studio, com o modelo de imagem do Gemini. O app do Gemini pode pôr uma marca visível num canto da imagem.
- **Uma folha, não 19 imagens.** Gerados juntos, os objetos saem com o mesmo contorno, a mesma luz e o mesmo tamanho. Gerados um por um, cada um sai num estilo diferente.
- **Proporção:** 5:4, deitada. A grade tem 5 colunas e 4 linhas; a última célula fica vazia.
- **Fundo:** magenta chapado `#FF00FF`. O Gemini não gera transparência, e o magenta não existe na paleta, então o `build.py` o remove sem ambiguidade.
- **Referência:** rode `python build.py` e anexe `preview/folha-4x.png`. Ela tem os mesmos objetos, na mesma ordem e com as mesmas cores. Diga ao Gemini que mantenha a forma geral e melhore o acabamento. Não anexe arte de terceiros: os Enfeites são CC0.
- **Iteração:** corrija uma célula por vez na mesma conversa ("keep every other cell identical, only redraw cell 10, the dog"). Gerar do zero muda todos os objetos.
- **Salvar:** baixe o PNG em resolução cheia e salve em `fonte/folha.png`.

## 2. Bloco de estilo

Cole este bloco no fim de toda geração e de toda edição:

```text
Style: contemporary urban pixel art icons for a cozy, playful fitness game. Each object is
drawn on a 32 by 32 pixel grid and enlarged with nearest-neighbor: every pixel is a crisp
square block, all blocks the same size across the whole image. Single-color near-black
outline (#14161f) of 1 pixel around each object. Flat colors with simple shading: one light
tone, one base tone and one shadow tone per material, light from the top left. Medium detail: use the larger
grid for small highlights, seams, textures and secondary shapes, but keep a bold, simple
silhouette that still reads when shown at half size.
No anti-aliasing, no blur, no gradients, no glow, no drop shadow, no text, no numbers,
no letters, no logos, no watermark.
Palette: asphalt greys #14161f #363c52 #5f6783 #8b92ab #b3b8cb, chalk #f5f1e8 #d6d0c0,
coral #c4432c #ff684a #ff9c80, violet #4b35a6 #7957e8 #a58cf4, yellow #d9a02b #ffd34e,
lime #67a24b #a8d978, plus bronze #8d5a36 #c98a5a and brown #6e4125 #a8693f.
```

## 3. Prompt

```text
A sprite sheet of 19 small everyday objects, arranged in a grid of 5 columns and 4 rows of
equal square cells, read left to right, top to bottom. The last cell (row 4, column 5) is empty.
Solid flat magenta background (#FF00FF) everywhere, with no grid lines and no cell borders.
Each object is centered in its cell, fits inside a 32 by 32 pixel area, and leaves a wide
magenta margin so no two objects touch. All objects share the same scale, the same outline
and the same light direction. Front view, slightly from above, like trophies on a shelf.

Row 1:
1. A small green sprout with two leaves in a coral clay pot.
2. The same coral pot with a taller lime-green plant and two small coral flowers.
3. The same coral pot with a small round tree: a thin brown trunk and a lime-green crown
   with a few coral fruits.
4. A bronze medal: round disk with an inner ring, hanging from a coral and dark ribbon.
5. A silver medal, the same shape, hanging from a violet ribbon.

Row 2:
6. A gold medal, the same shape, hanging from a coral and dark ribbon.
7. A gold trophy cup with two handles on a dark grey base with a small chalk plate.
8. A grey dumbbell seen from the side: two thick weight plates joined by a short handle.
9. A sports stopwatch: round chalk-white face, coral hand, a small button on top.
10. A small brown dog standing in side view with a coral collar and a short coral leash.

Row 3:
11. A bicycle in side view: two grey wheels, a coral frame, a dark seat and handlebar.
12. A swimming ring buoy seen from the front: chalk-white ring with four coral stripes.
13. A classic soccer ball: chalk-white with dark pentagon patches.
14. A rolled-up violet yoga mat lying on its side, the spiral visible at one end.
15. Low parallettes: a pair of small grey parallel bars for calisthenics, side by side.

Row 4:
16. An old-style alarm clock: round chalk-white face, black hands, two coral bells on top.
17. A city street lamp post: grey pole on a base and a lamp head with a yellow light.
18. A portable speaker box, dark grey, with two round grey speakers and a tiny coral light.
19. A violet gym backpack with coral straps and a small yellow tag.
```

Depois do prompt, acrescente o bloco de estilo de §2.

## 4. Ordem e nomes

A ordem da grade é fixa: o `build.py` lê as células nessa ordem.

| Célula | Enfeite | Arquivo em `saida/` |
|---:|---|---|
| 1 | Muda num vaso | `enfeite-sequencia4.png` |
| 2 | Planta com flor | `enfeite-sequencia8.png` |
| 3 | Árvore pequena em vaso | `enfeite-sequencia12.png` |
| 4 | Medalha de bronze | `enfeite-treinos10.png` |
| 5 | Medalha de prata | `enfeite-treinos50.png` |
| 6 | Medalha de ouro | `enfeite-treinos100.png` |
| 7 | Troféu | `enfeite-desafiocompleto.png` |
| 8 | Haltere | `enfeite-ratodeacademia.png` |
| 9 | Cronômetro | `enfeite-corredor.png` |
| 10 | Cachorro na coleira | `enfeite-andarilho.png` |
| 11 | Bicicleta | `enfeite-ciclista.png` |
| 12 | Boia | `enfeite-peixe.png` |
| 13 | Bola | `enfeite-boleiro.png` |
| 14 | Tapete de ioga enrolado | `enfeite-zen.png` |
| 15 | Paralelas | `enfeite-pesodocorpo.png` |
| 16 | Despertador | `enfeite-madrugador.png` |
| 17 | Poste de luz da praça | `enfeite-noturno.png` |
| 18 | Caixa de som portátil | `enfeite-fimdesemana.png` |
| 19 | Mochila de academia | `enfeite-ecletico.png` |

## 5. Conferência

Antes de salvar em `fonte/`, confira:

- São 19 objetos, na ordem de §4, e a última célula está vazia.
- O fundo é só magenta, sem sombra nem borda de célula, e nenhum objeto encosta em outro.
- A grade de pixels é uniforme, com blocos do mesmo tamanho em todos os objetos.
- As três etapas da planta usam o mesmo vaso, e as três medalhas têm a mesma forma: a evolução precisa ser lida como a mesma família (§9.2).
- As medalhas se distinguem pela cor do disco, não por número ou texto.
- Os objetos se leem sobre o asfalto `#222633`: veja a folha reduzida sobre um fundo dessa cor.

## 6. O que o `build.py` faz com a fonte

1. Divide a imagem na grade de 5×4. Separa as manchas que não são magenta (nem rosa da borda). Cada mancha grande vai para a célula onde fica o seu centro, e cada mancha pequena (uma alça ou um pino solto) vai para o objeto grande mais próximo. Assim um objeto que passa um pouco da borda da célula não é cortado nem invade o vizinho. Uma célula vazia faz o script parar.
2. Reduz todas as caixas com a mesma escala, para que a maior ocupe os 32 px, pela cor mais comum de cada bloco (não pela média), centrada embaixo.
3. Troca cada cor pela mais próxima da paleta `wrexercise` (e das cores do script); o magenta vira transparente.
4. Grava `saida/enfeite-<conquista>.png` e `preview/folha-4x.png`.
