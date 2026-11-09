# Prompts do Gemini — chão da praça

> Prompt para gerar no Gemini (Nano Banana) uma versão mais rica do chão da praça (`docs/gamificacao.md` §1.3, §3.1 e §4.4). A imagem gerada é só a **fonte**: ela vai para `fonte/praca.png`, e o `build.py` a reduz, a ajusta à paleta `wrexercise` e grava `saida/praca.png`. Nunca copie a imagem gerada direto para `saida/` ou `webapp/public/`.
>
> **Estado:** hoje o `build.py` desenha a praça só por código. Ler `fonte/praca.png` é o próximo passo dele; até lá, a imagem gerada serve de referência para o desenho.

## 1. Como usar o Gemini

- **Onde:** de preferência o Google AI Studio, com o modelo de imagem do Gemini. O app do Gemini pode pôr uma marca visível no canto de baixo à direita, e o canto é parte da praça.
- **Proporção:** 3:4, em pé. O mestre tem 180×230; o `build.py` reduz a imagem para 180×240 e corta 10 linhas de asfalto livre embaixo. Por isso, o prompt deixa uma faixa de asfalto vazio abaixo da linha de fundo da quadra.
- **Referência:** rode `python build.py` e anexe `preview/praca-2x.png`. Diga ao Gemini que ela mostra a composição, e não o acabamento. Não anexe arte de terceiros: a praça é CC0.
- **Iteração:** corrija com edições curtas na mesma conversa ("keep everything, only make the floor texture calmer"). Gerar do zero muda a composição.
- **Salvar:** baixe o PNG em resolução cheia e salve em `fonte/praca.png`.

## 2. Bloco de estilo

Cole este bloco no fim de toda geração e de toda edição:

```text
Style: contemporary urban pixel art for a cozy, playful fitness game. Low resolution pixel art,
as if drawn on a 180 by 240 pixel canvas and enlarged 5 times with nearest-neighbor: every
pixel is a crisp square block of the same size, aligned to one grid. No anti-aliasing, no blur,
no gradients, no noise, no photographic texture, no painterly strokes. Limited palette of about
20 flat colors. Dark blue-grey asphalt, chalk white lines, small accents of coral, violet,
yellow and lime. No fantasy, no medieval, no grass, no stones, no torches. No text, no letters,
no logos, no signature, no watermark.
```

## 3. Prompt

```text
Top-down view of an empty urban sports court in a city square, seen from directly above,
filling the whole image, portrait 3:4. Orthographic floor, no perspective on the ground.
No people, no animals, no loose objects on the floor.

Top edge, a thin back wall seen from the front, only about 7% of the image height:
- at the very top, a chain-link fence: small diamond mesh in mid blue-grey over a dark
  background, a light rail at the top and bottom, thin posts at regular intervals;
- right below it, a low concrete wall: light top edge, vertical joints, darker base;
- below the wall, a soft dark band of shadow on the asphalt.
Leave a short plain area of the concrete wall, about two thirds of the way to the right,
for a small signature that will be painted later.

The floor: dark blue-grey asphalt (#222633) with very subtle variation: a few slightly lighter
and darker specks and one thin crack. Keep it calm and low in contrast, because small characters
stand on it and must read clearly.

Court markings in dashed chalk white (#f5f1e8), short dashes with equal gaps:
- a rectangle inset a little from the left, right and bottom edges, starting just below
  the wall shadow;
- a fainter dashed half-court line across the middle;
- in the exact center, a filled circle of deep violet paint (#2f2468 with a slightly lighter
  #4b35a6 rim) about one quarter of the image width across, outlined by a dashed chalk circle.

Bottom-left corner, inside the court: a fixed calisthenics pull-up bar seen from above and
a little in front, standing on a dark rubber mat: two thin grey metal posts with a light
left side and a shaded right side, one horizontal bar with a light top, a short shadow
cast to the right on the mat. About one fifth of the image width.

Below the bottom court line, leave a strip of plain asphalt about 5% of the image height.
```

Depois do prompt, acrescente o bloco de estilo de §2.

## 4. Conferência

Antes de salvar em `fonte/`, confira:

- A grade de pixels é uniforme: todo bloco tem o mesmo tamanho, sem borrão nem degradê.
- O centro e a altura toda da quadra estão livres: os avatares ficam numa elipse em pé que ocupa a altura da praça (§3.1).
- O asfalto é calmo. Veja a imagem reduzida para 360×460: um avatar escuro precisa se destacar.
- A barra está no canto de baixo à esquerda e a faixa livre de asfalto está embaixo.
- Não há letras, logo, pessoa, grama ou objeto solto.
- O trecho livre da mureta existe. A assinatura "WX" (coral e violeta) continua desenhada pelo `build.py`, porque texto gerado sai torto.

## 5. O que o `build.py` faz com a fonte

1. Reduz para 180×240 pela cor mais comum de cada bloco (não pela média) e corta as 10 últimas linhas.
2. Troca cada cor pela mais próxima da paleta `wrexercise`.
3. Desenha por cima o que precisa de posição exata: a assinatura "WX" e, se a geração errar, as linhas da quadra.
4. Grava `saida/praca.png` e `preview/praca-2x.png`.
