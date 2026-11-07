"""Chão da praça do modo Jogo (docs/gamificacao.md §1.3 e §4.4).

Quadra urbana vista de cima: asfalto, linhas tracejadas de giz, alambrado e mureta ao fundo
(com uma assinatura discreta), círculo central e uma barra de calistenia no canto.

Mestre 1× de 180×150. O app mostra em 2× (360×300), com `image-rendering: pixelated`.
Uso: python build.py   (gera saida/praca.png e preview/praca-2x.png)
"""

import math
import pathlib
import sys

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parents[1] / ".claude/skills/pixel-art-studio/scripts"))
from pixelstudio import Sprite  # noqa: E402

L, A = 180, 150

# Rampa da paleta `wrexercise` (blue-grey do asfalto ao concreto) e acentos da marca.
A0, A1, A2, A3, A4, A5, A6, A7, A8, A9 = (
    "#14161f", "#1b1e29", "#222633", "#2b3042", "#363c52",
    "#474e68", "#5f6783", "#8b92ab", "#b3b8cb", "#d4d8e4",
)
GIZ, GIZ_SOMBRA = "#f5f1e8", "#d6d0c0"
CORAL, CORAL_ESCURO = "#ff684a", "#c4432c"
VIOLETA, VIOLETA_FUNDO, VIOLETA_PROFUNDO = "#7957e8", "#4b35a6", "#2f2468"

s = Sprite(L, A, palette="wrexercise")


def tracejado(x0, y0, x1, y1, cor, on=3, off=3):
    """Linha reta (horizontal ou vertical) tracejada, começando no ponto inicial."""
    passos = max(abs(x1 - x0), abs(y1 - y0))
    for i in range(passos + 1):
        if i % (on + off) < on:
            t = i / passos if passos else 0
            s.px(round(x0 + (x1 - x0) * t), round(y0 + (y1 - y0) * t), cor)


def circulo_tracejado(cx, cy, r, cor, on=3, off=3):
    """Círculo tracejado: pixels únicos em ordem angular, alternando traço e vão."""
    vistos, ordem = set(), []
    for k in range(720):
        a = 2 * math.pi * k / 720
        p = (round(cx + r * math.cos(a)), round(cy + r * math.sin(a)))
        if p not in vistos:
            vistos.add(p)
            ordem.append(p)
    for i, (x, y) in enumerate(ordem):
        if i % (on + off) < on:
            s.px(x, y, cor)


# --- Asfalto -----------------------------------------------------------------
s.rect(0, 0, L - 1, A - 1, A2)
s.noise(0, 0, L - 1, A - 1, A3, density=0.07, seed=1)
s.noise(0, 0, L - 1, A - 1, A1, density=0.05, seed=2)
s.noise(0, 0, L - 1, A - 1, A4, density=0.012, seed=3)
# Uma rachadura, para não parecer carpete.
for (x0, y0, x1, y1) in [(140, 104, 146, 108), (146, 108, 149, 107), (149, 107, 156, 112)]:
    s.line(x0, y0, x1, y1, A1)

# --- Quadra: círculo central pintado e linhas de giz -------------------------
CX, CY, R = 90, 78, 22
for y in range(CY - R, CY + R + 1):
    for x in range(CX - R, CX + R + 1):
        if (x - CX) ** 2 + (y - CY) ** 2 <= R * R and x % 2 == 0 and y % 2 == 0:
            s.px(x, y, VIOLETA_PROFUNDO)

M = 9  # margem das linhas de fundo
tracejado(M, 28, L - 1 - M, 28, GIZ)
tracejado(M, A - 1 - M, L - 1 - M, A - 1 - M, GIZ)
tracejado(M, 28, M, A - 1 - M, GIZ)
tracejado(L - 1 - M, 28, L - 1 - M, A - 1 - M, GIZ)
tracejado(M, CY, L - 1 - M, CY, GIZ_SOMBRA, on=2, off=4)  # linha do meio, mais discreta
circulo_tracejado(CX, CY, R, GIZ, on=3, off=2)

# --- Fundo: alambrado e mureta -------------------------------------------------
# Alambrado (y 0–9): losangos de tela sobre o escuro, trilho no alto e embaixo.
for y in range(0, 10):
    for x in range(L):
        s.px(x, y, A5 if (x + y) % 4 == 0 or (x - y) % 4 == 0 else A1)
s.rect(0, 0, L - 1, 1, A6)
s.rect(0, 0, L - 1, 0, A7)
s.rect(0, 9, L - 1, 9, A5)
for px_ in range(2, L, 36):  # postes
    s.rect(px_, 0, px_ + 1, 9, A6)
    s.px(px_, 1, A8)
# Mureta de concreto (y 10–17): tampo claro, juntas e base na sombra.
s.rect(0, 10, L - 1, 17, A7)
s.rect(0, 10, L - 1, 10, A9)
s.rect(0, 16, L - 1, 16, A6)
s.rect(0, 17, L - 1, 17, A5)
for jx in range(18, L, 30):
    s.rect(jx, 11, jx, 15, A6)
# Sombra da mureta no chão.
s.rect(0, 18, L - 1, 18, A1)
s.dither(0, 19, L - 1, 19, A1, A2, mix=0.5, pattern="bayer4", only=A2)

# Assinatura discreta na mureta: "WX" (coral e violeta), com sombra de 1 px.
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


letra(W, 126, 11, CORAL, CORAL_ESCURO)
letra(X, 133, 11, VIOLETA, VIOLETA_PROFUNDO)

# --- Barra de calistenia no canto inferior esquerdo ---------------------------
# Tapete de borracha no chão, depois a barra (postes, travessa e sombra para a direita).
s.rect(5, 126, 41, 146, A1)
s.rect(5, 126, 41, 126, A0)
s.rect(5, 146, 41, 146, A0)
s.rect(5, 126, 5, 146, A0)
s.rect(41, 126, 41, 146, A0)
for poste_x in (11, 33):
    s.rect(poste_x, 106, poste_x + 1, 140, A6)   # corpo do poste
    s.rect(poste_x, 106, poste_x, 140, A8)       # luz à esquerda
    s.rect(poste_x + 1, 106, poste_x + 1, 140, A4)  # lado na sombra
    s.rect(poste_x - 1, 140, poste_x + 2, 141, A5)  # base chumbada
    s.rect(poste_x + 2, 141, poste_x + 6, 142, A0)  # sombra no chão
s.rect(10, 106, 36, 106, A9)   # travessa: topo claro
s.rect(10, 107, 36, 107, A6)
s.rect(10, 108, 36, 108, A4)   # e sua sombra
s.rect(37, 108, 40, 109, A0)

s.preview(str(AQUI / "preview" / "praca-2x.png"), scale=2, bg=None, labels=False)
s.save_png(str(AQUI / "saida" / "praca.png"))
s.stats()
