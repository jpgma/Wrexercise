# Prompts do Gemini — cartas

> Prompts para gerar no Gemini (Nano Banana) os 9 ícones de 32×32 e as 2 molduras das cartas (`docs/gamificacao.md` §1.3, "Cartas", e §6.1). As imagens geradas são só a **fonte**: elas vão para `fonte/`, e o `build.py` as reduz, as ajusta à paleta `wrexercise` e grava `saida/`. Nunca copie uma imagem gerada direto para `saida/` ou `webapp/public/`.
>
> **Estado:** a primeira folha de ícones está em `fonte/icones.png`, e o `build.py` a lê. Sem ela, o script amplia 2× os ícones de 16×16 feitos por código. As células 4 (FIM DE SEMANA) e 5 (ANTES DAS 8) podem ser redesenhadas depois: em 32 px, elas têm objetos demais (§3, "Redesenhar uma célula"). Os 9 ícones desta folha já estão em `webapp/public/`. As molduras continuam só no código; ler o padrão da faixa de `fonte/molduras.png` ainda é um próximo passo.

## 1. Como usar o Gemini

- **Onde:** de preferência o Google AI Studio, com o modelo de imagem do Gemini. O app do Gemini pode pôr uma marca visível num canto da imagem.
- **Duas gerações:** uma folha com os 9 ícones e outra com as 2 molduras, cada uma numa conversa própria.
- **Fundo:** magenta chapado `#FF00FF`. O Gemini não gera transparência, e o magenta não existe na paleta, então o `build.py` o remove sem ambiguidade.
- **Referência:** rode `python build.py` e anexe `preview/icones-4x.png`. Para os ícones, diga que ela mostra a escala, o contorno e a paleta, e que as ideias podem mudar (§3). Não anexe arte de terceiros: as cartas são CC0.
- **Iteração:** corrija uma célula por vez na mesma conversa ("keep every other cell identical, only redraw cell 7"). Gerar do zero muda tudo.
- **Salvar:** baixe os PNGs em resolução cheia e salve em `fonte/icones.png` e `fonte/molduras.png`.

## 2. Bloco de estilo

Cole este bloco no fim de toda geração e de toda edição:

```text
Style: contemporary urban pixel art for a cozy, playful fitness game, like a street arcade.
Drawn on a small pixel grid and enlarged with nearest-neighbor: every pixel is a crisp square
block, all blocks the same size across the whole image. Single-color near-black outline
(#14161f). Flat colors with simple shading, light from the top left. No anti-aliasing,
no blur, no gradients, no glow, no drop shadow, no text, no numbers, no letters, no logos,
no watermark. No fantasy, no magic, no spells, no weapons.
Palette: asphalt greys #14161f #363c52 #5f6783, chalk #f5f1e8 #d6d0c0 #a9a392,
coral #c4432c #ff684a #ff9c80, violet #4b35a6 #7957e8 #a58cf4, yellow #d9a02b #ffd34e,
lime #67a24b #a8d978.
```

## 3. Ícones

O prompt dá ao Gemini o contexto de cada carta e deixa a ideia do desenho com ele. A ideia atual de cada ícone aparece como uma opção, não como ordem. Isso vale a pena porque o ícone de 32×32 tem espaço para uma pequena cena (um objeto com um detalhe que conta a história), e não só um símbolo.

### Contexto

- **O que são as cartas.** Amigos de um grupo de treino compram cartas com Energia e as jogam uns nos outros. Uma **Provocação** desafia um colega a fazer um treino com uma condição, dentro de um prazo. Uma **Resposta** é a carta que o desafiado joga de volta para aceitar a Provocação com uma vantagem. São bilhetes de provocação entre amigos, não feitiços (§1.2).
- **Onde o ícone aparece.** No centro da carta, em 64 px, sobre o giz `#f5f1e8`, abaixo do nome em caixa alta; em 32 px, num quadradinho de giz, na lista de Jogadas e na carta que voa entre dois avatares na cena. O nome da carta está sempre ao lado, então o ícone não precisa explicar tudo: ele dá o sabor.
- **Hierarquia de cor.** A moldura já diz o tipo: coral para Provocação, violeta para Resposta. O ícone pode puxar para o acento do tipo, mas não pode depender dele.

### Prompt

Proporção 1:1.

```text
A sprite sheet of 9 icons for the cards of a casual fitness game, arranged in a grid of
3 columns and 3 rows of equal square cells, read left to right, top to bottom. Solid flat
magenta background (#FF00FF) everywhere, with no grid lines and no cell borders. Each icon is
centered in its cell, fits inside a 32 by 32 pixel area, and leaves a wide magenta margin so
no two icons touch. All icons share the same scale, the same outline and the same light.

Context: friends in a small workout group buy cards with points and throw them at each other.
A "dare" card challenges a friend to do a workout with a condition before a deadline.
A "reply" card is played back to accept a dare with an advantage. The tone is friendly trash
talk between friends, urban street and gym life, never fantasy or combat. Each icon sits on a
chalk-white card (#f5f1e8), shown at 64 px on the card and at 32 px in small lists, always next
to the card's name, so it only needs to give the flavor of the card, not explain it.

You have creative freedom: for each card, pick the clearest and most charming idea you can draw
at 32 by 32 pixels. A small object with one telling detail works better than an abstract sign.
The ideas in brackets are only suggestions. Rules for every icon:
- a bold silhouette that reads at 32 px, different from the other 8 icons;
- one main object, with at most one small extra detail: a scene of three objects does not read
  at 32 px;
- a near-black outline, so it stands out on a chalk-white card;
- no text, no letters and no numbers, except where a card says otherwise;
- no weapons, no money, no alcohol, no casino tables, no fantasy objects;
- do not use an alarm clock, a trophy or a medal: those are achievement rewards elsewhere.

Dare cards (coral accent, cells 1 to 5):
1. "+10 MIN": a workout at least 10 minutes longer than the group's minimum duration.
   [a stopwatch with a small coral wedge on the dial; a sweaty towel with a timer]
2. "+20 MIN": the same as card 1, with 20 minutes. It must look like a stronger version of
   card 1: the same main object, with clearly more of the extra (a bigger wedge, more sweat,
   a small flame).
3. "VARIEDADE" (variety): a workout of a different type than the friend's last one.
   [two crossing shuffle arrows; two different sports objects swapping places,
   like a sneaker and a dumbbell; a spinning selector]
4. "FIM DE SEMANA" (weekend): a workout that starts on Saturday or Sunday.
   [a calendar page with the last two columns highlighted in coral; sneakers next to
   sunglasses; a sunny park bench]
5. "ANTES DAS 8" (before 8 am): a workout that starts before 08:00.
   [a yellow sun rising behind the court line; a steaming coffee cup next to a sneaker;
   the first light over city rooftops]

Reply cards (violet accent, cells 6 to 9):
6. "DESAFIO DUPLO" (double dare): accepts the dare and sends the same challenge back to the
   friend who sent it. [two arrows going opposite ways; a ping-pong paddle returning a ball;
   an envelope with a curved arrow coming back]
7. "APOSTA" (the bet): accepts the dare; if the workout is done, the reward doubles, and if not,
   the card is lost. Double or nothing. A large "2" or "x2" made of pixels is allowed here.
   [a game token with a 2; a coin mid-flip; two stacked tokens]
8. "PRAZO EXTRA" (extra time): accepts the dare with 24 more hours of deadline.
   [an hourglass with a small plus sign; a calendar page with a plus; a clock with an
   extra loop of the hand]
9. "MUDANÇA DE PLANOS" (change of plans): accepts the dare but swaps its goal for another
   goal of the same value. [a street signpost with boards pointing opposite ways;
   a route on a map with a detour; two cards swapping places]
```

Depois do prompt, acrescente o bloco de estilo de §2.

### Explorar uma carta (opcional)

Quando um ícone não convencer, peça opções só para ele numa conversa nova, escolha uma e volte à folha pedindo para redesenhar aquela célula com a ideia escolhida.

```text
Four different ideas for one 32 by 32 pixel icon, in a 2 by 2 grid of equal cells on a solid
flat magenta background (#FF00FF), with no grid lines. The icon is for the card <"APOSTA">:
<paste the card's line from the prompt above, with its meaning>. Each idea must use a different
object, not the same object in four styles.
```

Depois do prompt, acrescente o bloco de estilo de §2 e as regras da lista "Rules for every icon".

### Redesenhar uma célula

Na mesma conversa da folha. Na primeira folha, as células 4 e 5 ficaram com três objetos cada, e o tênis aparecia nas duas. Em 32 px, os óculos viravam um borrão, e as casas coral do calendário se fundiam.

```text
Keep every other cell exactly identical, at the same scale and position. Only redraw cells 4 and 5.
Cell 4, "FIM DE SEMANA" (weekend): only a calendar page with two rings on top, larger, filling
the cell like the other icons. A grid of 5 columns by 3 rows of day squares, with a 1-pixel gap
between squares. The 2 rightmost columns are solid coral (#ff684a), the other columns are light
grey. No sneaker, no sunglasses, no other object.
Cell 5, "ANTES DAS 8" (before 8 am): a yellow sun rising behind a dark horizon line, larger,
with a small steaming coffee cup in front of the line at the lower left. No sneaker, no other
object.
```

Depois do prompt, acrescente o bloco de estilo de §2.

### Ordem e nomes

A ordem da grade é fixa: o `build.py` lê as células nessa ordem.

| Célula | Carta | Tipo | Arquivo em `saida/` |
|---:|---|---|---|
| 1 | +10 MIN | `Provocacao` | `carta-mais10min.png` |
| 2 | +20 MIN | `Provocacao` | `carta-mais20min.png` |
| 3 | VARIEDADE | `Provocacao` | `carta-variedade.png` |
| 4 | FIM DE SEMANA | `Provocacao` | `carta-fimdesemana.png` |
| 5 | ANTES DAS 8 | `Provocacao` | `carta-antesdas8.png` |
| 6 | DESAFIO DUPLO | `Resposta` | `carta-desafioduplo.png` |
| 7 | APOSTA | `Resposta` | `carta-aposta.png` |
| 8 | PRAZO EXTRA | `Resposta` | `carta-prazoextra.png` |
| 9 | MUDANÇA DE PLANOS | `Resposta` | `carta-mudancadeplanos.png` |

### Conferência

- São 9 ícones, na ordem acima, e cada um tem uma silhueta própria.
- Nenhum tem texto. A única exceção é o "2" ou "x2" da APOSTA.
- +20 MIN é claramente a versão mais forte de +10 MIN.
- Nenhum ícone repete um Enfeite (despertador, troféu, medalha), para que carta e conquista não se confundam. O Enfeite do `Corredor` também é um cronômetro: se +10 MIN e +20 MIN usarem um, a fatia ou o detalhe extra precisa diferenciá-lo.
- Os ícones se leem em 32 px sobre o giz `#f5f1e8`: veja `preview/icones-4x.png` depois do `build.py`.
- Nenhum depende só da cor do tipo para fazer sentido.

## 4. Molduras

A moldura vira um `border-image` com fatias de 10 px em cima e 6 px nos lados e embaixo, e a carta pode ter qualquer tamanho. Por isso, a faixa do topo e as bordas precisam de um padrão que repita a cada 8 px. O Gemini não garante isso. A forma fica no código (contorno de 2 px, cantos recortados, corpo em giz), e a geração serve só para o **padrão da faixa**: a ideia é copiar um trecho de 8 px da faixa gerada e repeti-lo.

### Prompt

Proporção 3:2.

```text
Two blank playing card frames side by side, both portrait, with the same size and the same
shape, on a solid flat magenta background (#FF00FF), with a wide magenta gap between them.
Each card is drawn on a 24 by 32 pixel grid: a thick 2-pixel near-black outline (#14161f),
corners cut diagonally (3 pixels at top-left and bottom-right, 1 pixel at the other two),
and a plain chalk-white body (#f5f1e8) with one slightly darker chalk line near the bottom.
The body is empty: no text, no icon, no price, no picture.

Left card, the "taunt" card: a coral band (#ff684a) across the top, 8 pixels tall, with a
darker coral line under it. On the band, light coral comic-style speed lines: two long dashes
and one short dash per row, shifted on each row, getting thinner toward the right. The pattern
repeats exactly every 8 pixels horizontally.

Right card, the "reply" card: the same frame with a violet band (#7957e8) and a darker violet
line under it. On the band, a light violet halftone dot pattern: dots get sparser from top to
bottom. The pattern repeats exactly every 8 pixels horizontally.
```

Depois do prompt, acrescente o bloco de estilo de §2.

### Conferência

- As duas molduras têm a mesma forma, e só a cor e o padrão da faixa mudam (§1.3).
- A faixa da Provocação tem linhas de movimento, e a da Resposta tem halftone.
- O corpo está vazio: nome, selo, ícone, efeito e preço são desenhados pela interface.

## 5. O que o `build.py` faz com as fontes

1. Divide `fonte/icones.png` na grade de 3×3 e, em cada célula, pega a caixa do que não é magenta. Uma célula vazia faz o script parar.
2. Reduz todas as caixas com a mesma escala, para que a maior ocupe os 32 px, pela cor mais comum de cada bloco (não pela média), e centra cada ícone.
3. Troca cada cor pela mais próxima da paleta `wrexercise` (e das cores do script); o magenta vira transparente.
4. Grava `saida/carta-*.png`, `preview/icones-4x.png` e as molduras feitas por código.

O recorte é o mesmo dos Enfeites, em `pixel-art/fonte_gerada.py`.
