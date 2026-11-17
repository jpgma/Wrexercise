# Prompts do Gemini — animações de dormir do `jpgma`

> As 4 animações de dormir (`dormindo`, `adormecer`, `acordar` e `levantar`; `docs/decisoes/0057-avatar-dormindo.md`) do `jpgma` foram geradas no Gemini, uma folha por direção, e não no PixelLab. Os prompts são os de `../base-masculino/prompts-gemini.md` §4, "Dormir", com as trocas abaixo. As regras de lá (grade, proporção, célula de calibração, ordem) continuam valendo.

## Como gerar

1. `python referencia.py` grava `saida/referencia-gemini/<direcao>.png`: a pose parada de cada direção, ampliada 8× sobre magenta (384×512).
2. Gere `dormindo` primeiro, anexando a pose parada. Salve cada folha em `fonte/gemini/<animacao>-<direcao>.png`.
3. `python gemini.py` reduz as folhas para tiras de 48×48 em `saida/gemini/`, na paleta do `jpgma`.
4. `python referencia.py` de novo: agora ele grava também `saida/referencia-gemini/dormindo-<direcao>.png`, a pose sentada. Nas outras três animações, anexe a pose parada **e** essa, nessa ordem.
5. `python dormir.py` acrescenta os quadros ao fim de `fonte/jpgma.aseprite` (uma vez só), e `python build.py` monta a folha de 129 linhas.

- **Direções:** só `south`, `southeast`, `east`, `northeast` e `north`; o `dormir.py` espelha as outras três.
- **Quadros da folha:** a ordem fica em `SEQUENCIA`, no `dormir.py`. A célula de calibração (a cópia da pose parada) não entra: onde a animação mostra a pose parada, o quadro é o da linha `rotacao`.

## Trocas nos prompts

Na primeira linha e na ação, onde o prompt diz "the reference image" e há duas imagens anexadas, diga "the first attached reference image" e "the second reference image".

No bloco `CHARACTER`, a primeira frase passa a descrever o personagem:

```text
CHARACTER: exactly the same character as in the reference in every frame: the same messy wavy
dark brown hair with the small tuft on top, the same short full dark beard and mustache, the
same plain black t-shirt, charcoal black pants and white sneakers. The same facing direction
(do not turn the character), the same head size, the same body proportions, the same colors,
the same near-black outline and the same pixel size.
```

No mesmo bloco, antes de "All movements below are measured in those pixel blocks", acrescente (a roupa é quase da cor do contorno):

```text
The bare forearms and hands stay skin-colored, so they read clearly against the dark shirt
and the dark pants.
```

A boca fica dentro da barba, em vermelho escuro: troque "a small open dark dot" por "a small open dark red dot of 1 block inside the beard", e "a dark oval" por "a dark red oval".

## Conferência

- A folha tem a quantidade certa de figuras na grade, e nenhuma encosta na borda da célula.
- `preview/gemini-<animacao>-<direcao>-6x.png` mostra a pose parada e os quadros reduzidos: o cabelo não tem pontos vermelhos e a camiseta não ficou marrom (o Gemini às vezes esquenta a folha; `mais_proxima`, no `gemini.py`, separa os materiais).
- `preview/<animacao>.gif` mostra as 8 direções depois do `build.py`.
