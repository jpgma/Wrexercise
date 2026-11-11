# Prompts do Gemini — vida na praça

> Prompt para gerar no Gemini (Nano Banana) os sprites de ambiente da praça do modo Jogo: o pombo, a bola e as luminárias (`docs/gamificacao.md` §3). A imagem gerada é só a **fonte**: ela vai para `fonte/folha.png`, e o `build.py` recorta cada quadro, o reduz para 16×16, o ajusta à paleta `wrexercise` e grava os PNGs em `saida/`. Nunca copie a imagem gerada direto para `saida/` ou `webapp/public/`.
>
> **Estado:** a primeira folha está em `fonte/folha.png`. O Gemini desenhou os sprites com o dobro do tamanho pedido; o `build.py` os reduz pela metade, ancora cada quadro e desenha a bola por código, porque reduzida ela perdia o contorno redondo.

## 1. Como usar o Gemini

- **Onde:** de preferência o Google AI Studio, com o modelo de imagem do Gemini. O app do Gemini pode pôr uma marca visível num canto da imagem.
- **Uma folha só.** Gerados juntos, pombo, bola e luminárias saem com o mesmo contorno, a mesma luz e a mesma escala.
- **Proporção:** 4:5, em pé. A grade tem 4 colunas e 5 linhas de células quadradas iguais.
- **Fundo:** magenta chapado `#FF00FF`. O Gemini não gera transparência, e o magenta não existe na paleta, então o `build.py` o remove sem ambiguidade.
- **Referência:** anexe `Wrexercise/webapp/public/praca.png` e uma captura de um quadro de `pixel-art/base-masculino/preview/folha-parado.gif` (o Gemini não lê GIF animado), e diga que são só para estilo e escala, sem copiá-los. Não anexe arte de terceiros.
- **Iteração:** corrija uma linha por vez na mesma conversa ("keep every other cell identical, only redraw row 3, the flying pigeon"). Gerar do zero muda tudo.
- **Salvar:** baixe o PNG em resolução cheia e salve em `fonte/folha.png`.

## 2. Bloco de estilo

Cole este bloco no fim de toda geração e de toda edição:

```text
Style: contemporary urban pixel art sprites for a cozy, playful fitness game, matching the
attached rooftop court and character (attached only for style and scale, do not copy them).
Each sprite is drawn on a 16 by 16 pixel grid and enlarged with nearest-neighbor: every pixel is
a crisp square block, all blocks the same size across the whole image. Single-color near-black
outline (#14161f) of 1 pixel around each sprite. Flat colors with simple shading: one light,
one base and one shadow tone per material, light from the top left.
No anti-aliasing, no blur, no gradients, no glow halos, no drop shadows, no text, no numbers,
no letters, no logos, no watermark.
Palette: asphalt greys #14161f #363c52 #5f6783 #8b92ab #b3b8cb, chalk #f5f1e8 #d6d0c0,
coral #c4432c #ff684a #ff9c80, violet #4b35a6 #7957e8 #a58cf4, yellow #d9a02b #ffd34e,
lime #67a24b #a8d978.
```

## 3. Prompt

```text
A sprite sheet of small ambient sprites for a top-down 2.5D rooftop court, arranged in a grid of
4 columns and 5 rows of equal square cells, read left to right, top to bottom. Every cell is used.
Solid flat magenta background (#FF00FF) everywhere, with no grid lines and no cell borders.
Each sprite fits inside a 16 by 16 pixel area in the middle of its cell, with a wide magenta
margin so no two sprites touch. Everything shares one scale: a person in this game is 32 pixels
tall, so the pigeon is about 11 pixels long and 8 pixels tall, and the ball about 7 pixels wide.
Same view as the characters: seen from the side and slightly from above.

Rows 1 to 3 are animation frames of the same city pigeon, always facing right, in side view:
blue-grey body (#5f6783 / #8b92ab), darker wing bars and tail tip (#363c52), a small violet and
lime iridescent patch on the neck (#7957e8, #a8d978), coral feet and eye (#ff684a).
Keep the body the same size and at the same height in every frame of a row, so the frames
animate cleanly. Feet stand on the bottom of the 16-pixel area in rows 1 and 2.

Row 1, standing and pecking:
1. Standing still, head up.
2. Standing, head bobbed slightly forward.
3. Head lowered halfway toward the ground.
4. Beak touching the ground, pecking.

Row 2, walking (a 4-frame loop):
5. Left foot forward, head pulled back.
6. Feet together, head forward.
7. Right foot forward, head pulled back.
8. Feet together, head forward.

Row 3, flying away (a 4-frame loop), body horizontal, feet tucked:
9. Wings fully raised.
10. Wings level, spread.
11. Wings fully lowered.
12. Wings level, spread (same as 10).

Row 4, a small street rubber ball rolling (a 4-frame loop): the same coral ball (#ff684a, shadow
#c4432c) with one chalk-white stripe (#f5f1e8) around it, the stripe rotated a quarter step
further in each frame so it looks like it rolls to the right:
13. Stripe vertical.
14. Stripe tilted 45 degrees.
15. Stripe horizontal.
16. Stripe tilted 45 degrees the other way.

Row 5, lights for the edges of the rooftop, front view slightly from above:
17. A small wall lamp mounted on a short dark grey bracket, lamp turned off: grey metal shade,
    pale grey glass (#b3b8cb).
18. The same wall lamp turned on: the same shape, glass bright yellow (#ffd34e) with a chalk-white
    center (#f5f1e8). No glow outside the lamp itself.
19. A short round floor bollard light, about 6 pixels wide and 12 tall, dark grey body, turned off,
    pale grey glass band near the top.
20. The same bollard turned on: the glass band bright yellow with a chalk-white center.
    No glow outside the bollard itself.
```

Depois do prompt, acrescente o bloco de estilo de §2.

## 4. Ordem e nomes

A ordem da grade é fixa: o `build.py` lê as células nessa ordem.

| Células | Saída | Uso |
|---|---|---|
| 1–4 | `pombo-parado.png` (4 quadros) | parado, olhando e bicando o chão |
| 5–8 | `pombo-andar.png` (4 quadros) | anda alguns passos perto da borda |
| 9–12 | `pombo-voar.png` (4 quadros) | sai voando quando alguém passa perto |
| 13–16 | `bola.png` (4 quadros) | rola pela praça; espelhada para rolar para a esquerda |
| 17–18 | `luminaria-parede.png` (apagada, acesa) | na mureta, à noite com brilho em CSS |
| 19–20 | `luminaria-chao.png` (apagada, acesa) | nas bordas laterais, à noite com brilho em CSS |

O brilho das luzes não vem da arte: é um gradiente em CSS por cima da cena, que só aparece à noite.

## 5. Conferência

Antes de salvar em `fonte/`, confira:

- A grade de pixels é uniforme: todo bloco tem o mesmo tamanho, sem borrão nem degradê.
- São 20 sprites em 4 colunas e 5 linhas, nenhum encostando no outro, todos sobre magenta.
- O pombo tem o mesmo tamanho e a mesma altura do corpo em todos os quadros, e olha para a direita.
- O pombo é pequeno perto do avatar (cerca de um terço da altura) e se destaca no asfalto escuro.
- A bola é a mesma nos quatro quadros, só a listra gira.
- As luminárias acesas e apagadas têm exatamente o mesmo contorno.
- Não há letras, logo, brilho ou sombra fora dos objetos.
