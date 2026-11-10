# Prompts do Gemini — chão da praça

> Prompt para gerar no Gemini (Nano Banana) uma versão mais rica do chão da praça (`docs/gamificacao.md` §1.3, §3.1 e §4.4). A imagem gerada é só a **fonte**: ela vai para `fonte/praca.png`, e o `build.py` a reduz, a ajusta à paleta `wrexercise` e grava `saida/praca.png`. Nunca copie a imagem gerada direto para `saida/` ou `webapp/public/`.
>
> **Estado:** o `build.py` lê `fonte/praca.png`. A fonte atual é uma quadra num terraço, com a barra no canto de cima à esquerda; ela foi aceita como veio, sem correções por código.

O prompt fixa só as **restrições** (vista, escala, paleta, área livre para os avatares, legibilidade) e deixa a composição por conta do gerador. A ideia é ver cenas que não pensaríamos em desenhar e escolher a melhor, e não reproduzir a praça atual com mais acabamento.

## 1. Como usar o Gemini

- **Onde:** de preferência o Google AI Studio, com o modelo de imagem do Gemini. O app do Gemini pode pôr uma marca visível num canto, e os cantos são parte da praça.
- **Proporção:** 3:4, em pé. O mestre tem 180×230; o `build.py` reduz a imagem para 180×240 e corta 10 linhas embaixo. Por isso, o prompt pede uma faixa de chão vazio no pé da imagem.
- **Referência:** não anexe a praça atual na primeira geração, porque o Gemini tende a copiá-la. Anexe-a só se quiser puxar uma variação de volta para a composição conhecida. Não anexe arte de terceiros: a praça é CC0.
- **Explorar:** gere várias vezes do zero, com o mesmo prompt ou com uma das sementes de §4. Guarde as que funcionarem e só depois refine uma delas.
- **Refinar:** corrija com edições curtas na mesma conversa ("keep everything, only make the floor calmer"). Gerar do zero muda a composição.
- **Salvar:** baixe o PNG em resolução cheia e salve em `fonte/praca.png`.

## 2. Bloco de estilo

Cole este bloco no fim de toda geração e de toda edição:

```text
Style: contemporary urban pixel art for a cozy, playful fitness game. Low resolution pixel art,
as if drawn on a 180 by 240 pixel canvas and enlarged 5 times with nearest-neighbor: every
pixel is a crisp square block of the same size, aligned to one grid. No anti-aliasing, no blur,
no gradients, no noise, no photographic texture, no painterly strokes. Limited palette of about
20 flat colors, built around dark blue-grey asphalt (#222633), chalk white (#f5f1e8) and small
accents of coral (#ff684a), violet (#7957e8), yellow (#ffd34e) and lime (#a8d978).
No fantasy, no medieval, no grass fields, no stones, no torches. No text, no letters, no logos,
no signature, no watermark.
```

## 3. Prompt

```text
Design the floor of a small urban outdoor spot where a group of friends meets to train:
a street court, a rooftop, a corner of a city square, a schoolyard, a pier, a parking deck,
or anything else you find more interesting. Surprise me with the setting, the layout and the
small details that give the place character. Portrait 3:4, filling the whole image.

Constraints the scene must respect:
- Seen from directly above, orthographic, no perspective on the ground. Things along the
  edges may show a little of their front side.
- Up to ten small characters will be placed later on an upright ellipse that spans almost
  the full height of the image and most of its width. Keep that area open and walkable:
  no objects, no strong shapes, no busy patterns inside it. Put the interesting things
  around the edges and in the corners.
- The ground is calm and low in contrast, mostly dark blue-grey, so small dark characters
  still read clearly on it. Markings, paint and texture are welcome if they stay subtle.
- Somewhere near an edge or a corner, a fixed calisthenics pull-up bar. Its exact form and
  position are up to you.
- At most one discreet piece of graffiti or painted art, and no readable text anywhere.
- No people, no animals, no loose clutter on the open floor.
- Leave a strip of plain ground about 5% of the image height along the bottom edge.
```

Depois do prompt, acrescente o bloco de estilo de §2.

## 4. Sementes opcionais

Para variar sem ditar a cena, acrescente uma linha destas antes do bloco de estilo. Use uma por geração, ou nenhuma.

```text
Mood: late evening, a few warm lights along the edges.
Mood: early morning, long soft shadows, fresh and quiet.
Mood: right after rain, a few small puddles near the edges.
Mood: lively neighborhood, signs of daily life just outside the open area.
Mood: minimal and graphic, bold court paint, very few objects.
```

## 5. Conferência

Antes de salvar em `fonte/`, confira:

- A grade de pixels é uniforme: todo bloco tem o mesmo tamanho, sem borrão nem degradê.
- A elipse em pé, na altura toda da praça, está livre (§3.1). Coisas nas bordas e nos cantos podem sobrar.
- O chão é calmo. Veja a imagem reduzida para 360×460: um avatar escuro precisa se destacar.
- A barra de calistenia aparece, perto de uma borda ou de um canto (§9.3).
- A faixa de chão livre embaixo existe.
- Não há letras, logo, pessoa, animal, grama de RPG ou tralha no meio do chão.
- A cena continua urbana e contemporânea, nos tons da marca. Se ficou fantasiosa ou colorida demais, peça a correção numa edição curta.

## 6. O que o `build.py` faz com a fonte

1. Troca cada pixel pela cor mais próxima da paleta `wrexercise`. Fazer isso antes de reduzir tira o ruído da compressão da contagem.
2. Reduz para 180×240 pela cor mais comum de cada bloco (não pela média) e corta as 10 últimas linhas.
3. Desenha por cima só a assinatura "WX" (coral e violeta), num trecho liso da borda que a cena oferecer, porque texto gerado sai torto. Ao trocar a fonte, confira a posição em `ASSINATURA_X` e `ASSINATURA_Y`.
4. Grava `saida/praca.png` e `preview/praca-2x.png`. Copie `saida/praca.png` para `Wrexercise/webapp/public/praca.png` (`docs/avatar-kit.md`).
