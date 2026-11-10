"""Chão da praça do modo Jogo (docs/gamificacao.md §1.3 e §4.4).

A cena vem de `fonte/praca.png`, gerada no Gemini com `prompts-gemini.md`. Este script a reduz
para o mestre 1× de 180×230, ajusta as cores à paleta `wrexercise` e desenha por cima só a
assinatura "WX". O app mostra o mestre em 2× (360×460), com `image-rendering: pixelated`.

Uso: python build.py   (gera saida/praca.png e preview/praca-2x.png)
"""

import pathlib
import sys
from collections import Counter

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parents[1] / ".claude/skills/pixel-art-studio/scripts"))
from pixelstudio import Sprite, hex2rgba, nearest, rgba2hex  # noqa: E402

L, A = 180, 230
A_FONTE = 240  # a fonte é 3:4; as últimas linhas, de chão livre, ficam de fora

s = Sprite(L, A, palette="wrexercise")
paleta = [hex2rgba(c) for c in s.palette]

# --- Fonte: cada pixel vai para a cor mais próxima da paleta, e cada bloco fica com a cor mais
# comum (não a média, que inventaria cores e borraria as bordas). Ajustar antes de contar deixa
# o ruído da compressão da imagem gerada fora da contagem.
fonte = Image.open(AQUI / "fonte" / "praca.png").convert("RGB")
fonte = fonte.resize((L * 5, A_FONTE * 5), Image.NEAREST)
cache: dict[tuple, str] = {}


def cor_da_paleta(rgb):
    if rgb not in cache:
        cache[rgb] = rgba2hex(nearest((*rgb, 255), paleta))
    return cache[rgb]


pixels = fonte.load()
for y in range(A):
    for x in range(L):
        bloco = Counter(
            cor_da_paleta(pixels[x * 5 + dx, y * 5 + dy]) for dy in range(5) for dx in range(5)
        )
        s.px(x, y, bloco.most_common(1)[0][0])

# --- Assinatura discreta na mureta do fundo: "WX" (coral e violeta), com sombra de 1 px.
# Fica no trecho liso entre os caixotes e o ar-condicionado; texto gerado sai torto.
CORAL, CORAL_ESCURO = "#ff684a", "#c4432c"
VIOLETA, VIOLETA_PROFUNDO = "#7957e8", "#2f2468"
W = ["X...X", "X...X", "X.X.X", "X.X.X", ".X.X."]
X = ["X...X", ".X.X.", "..X..", ".X.X.", "X...X"]


def letra(bitmap, x, y, cor, sombra):
    for dy, linha in enumerate(bitmap):
        for dx, c in enumerate(linha):
            if c == "X":
                s.px(x + dx + 1, y + dy + 1, sombra)
    for dy, linha in enumerate(bitmap):
        for dx, c in enumerate(linha):
            if c == "X":
                s.px(x + dx, y + dy, cor)


ASSINATURA_X, ASSINATURA_Y = 84, 11
letra(W, ASSINATURA_X, ASSINATURA_Y, CORAL, CORAL_ESCURO)
letra(X, ASSINATURA_X + 7, ASSINATURA_Y, VIOLETA, VIOLETA_PROFUNDO)

s.preview(str(AQUI / "preview" / "praca-2x.png"), scale=2, bg=None, labels=False)
s.save_png(str(AQUI / "saida" / "praca.png"))
s.stats()
