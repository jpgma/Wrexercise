"""Enfeites do modo Jogo (docs/gamificacao.md §9): 19 objetos de 16×16, um por conquista.

O arquivo de cada um tem o código da conquista em minúsculas: saida/enfeite-<conquista>.png.
A planta e a medalha evoluem (três níveis cada), então a vitrine mostra o nível mais alto.

Uso: python build.py   (gera saida/*.png e preview/folha-8x.png)
"""

import math
import pathlib
import sys

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parents[1] / ".claude/skills/pixel-art-studio/scripts"))
from pixelstudio import Sprite  # noqa: E402

TINTA = "#14161f"
GIZ, GIZ_SOMBRA = "#f5f1e8", "#d6d0c0"
CORAL, CORAL_ESCURO, CORAL_CLARO = "#ff684a", "#c4432c", "#ff9c80"
VIOLETA, VIOLETA_ESCURO, VIOLETA_CLARO = "#7957e8", "#4b35a6", "#a58cf4"
AMARELO, AMARELO_ESCURO = "#ffd34e", "#d9a02b"
LIMA, LIMA_ESCURO = "#a8d978", "#67a24b"
CINZA, CINZA_CLARO, CINZA_ESCURO = "#5f6783", "#8b92ab", "#363c52"
BRONZE, BRONZE_ESCURO = "#c98a5a", "#8d5a36"
PRATA, PRATA_ESCURO = "#d4d8e4", "#8b92ab"
AZUL = "#4aa8ff"
MARROM, MARROM_ESCURO = "#a8693f", "#6e4125"

(AQUI / "saida").mkdir(exist_ok=True)
(AQUI / "preview").mkdir(exist_ok=True)


def novo():
    return Sprite(16, 16, palette="wrexercise")


def vaso(s, y0=11):
    s.rect(4, y0, 11, y0 + 4, CORAL)
    s.rect(4, y0, 11, y0, CORAL_CLARO)
    s.rect(4, y0 + 3, 11, y0 + 3, CORAL_ESCURO)
    s.rect(5, y0 + 4, 10, y0 + 4, CORAL_ESCURO)


def muda():
    s = novo()
    vaso(s)
    s.rect(7, 7, 8, 10, LIMA_ESCURO)
    s.rect(4, 5, 7, 7, LIMA)
    s.rect(8, 4, 11, 6, LIMA)
    s.px(4, 5, LIMA_ESCURO)
    s.px(11, 6, LIMA_ESCURO)
    s.outline(TINTA)
    return s


def planta():
    s = novo()
    vaso(s)
    s.rect(7, 5, 8, 10, LIMA_ESCURO)
    s.rect(4, 8, 6, 9, LIMA)
    s.rect(9, 7, 11, 8, LIMA)
    for cx, cy in [(5, 3), (10, 4), (8, 2)]:
        s.rect(cx - 1, cy, cx + 1, cy, CORAL_CLARO)
        s.rect(cx, cy - 1, cx, cy + 1, CORAL_CLARO)
        s.px(cx, cy, AMARELO)
    s.outline(TINTA)
    return s


def arvore():
    s = novo()
    vaso(s, 12)
    s.rect(7, 7, 8, 11, MARROM)
    s.px(8, 8, MARROM_ESCURO)
    s.px(8, 10, MARROM_ESCURO)
    s.ellipse(2, 0, 13, 8, LIMA)
    s.px(10, 3, CORAL)
    s.px(5, 5, CORAL)
    s.px(9, 6, CORAL)
    s.px(5, 2, LIMA_ESCURO)
    s.px(11, 5, LIMA_ESCURO)
    s.outline(TINTA)
    return s


def medalha(corpo, sombra, fita, fita_escura):
    s = novo()
    s.rect(4, 0, 6, 5, fita)
    s.rect(9, 0, 11, 5, fita_escura)
    s.circle(7, 10, 5, corpo, fill=True)
    s.circle(7, 10, 5, sombra)
    s.circle(7, 10, 3, sombra)
    s.rect(7, 9, 8, 11, sombra)
    s.px(6, 8, GIZ)
    s.px(5, 9, GIZ)
    s.outline(TINTA)
    return s


def trofeu():
    s = novo()
    s.rect(4, 1, 11, 7, AMARELO)
    s.rect(5, 8, 10, 8, AMARELO)
    s.rect(6, 9, 9, 9, AMARELO_ESCURO)
    s.rect(7, 10, 8, 11, AMARELO_ESCURO)
    s.rect(4, 7, 11, 7, AMARELO_ESCURO)
    s.rect(5, 2, 5, 5, GIZ)
    for y in (2, 3, 4):
        s.px(3, y, AMARELO_ESCURO)
        s.px(12, y, AMARELO_ESCURO)
    s.rect(5, 12, 10, 14, CINZA)
    s.rect(5, 12, 10, 12, CINZA_CLARO)
    s.outline(TINTA)
    return s


def haltere():
    s = novo()
    s.rect(1, 5, 3, 10, CINZA_ESCURO)
    s.rect(4, 3, 5, 12, CINZA)
    s.rect(10, 3, 11, 12, CINZA)
    s.rect(12, 5, 14, 10, CINZA_ESCURO)
    s.rect(6, 7, 9, 8, CINZA_CLARO)
    s.rect(4, 3, 4, 12, CINZA_CLARO)
    s.rect(10, 3, 10, 12, CINZA_CLARO)
    s.outline(TINTA)
    return s


def cronometro():
    s = novo()
    cx, cy, r = 7.5, 9.0, 5.6
    for y in range(16):
        for x in range(16):
            if math.hypot(x - cx, y - cy) <= r:
                s.px(x, y, GIZ)
    s.rect(6, 1, 9, 1, CINZA)
    s.rect(7, 2, 8, 2, CINZA)
    s.px(12, 3, CINZA)
    s.px(7, 9, TINTA)
    s.rect(8, 6, 8, 9, CORAL)
    s.rect(9, 9, 10, 9, CORAL)
    s.outline(TINTA)
    return s


def cachorro():
    s = novo()
    s.rect(4, 6, 12, 10, MARROM)
    s.rect(11, 3, 14, 7, MARROM)
    s.rect(13, 4, 14, 5, MARROM_ESCURO)
    s.px(14, 6, TINTA)
    s.rect(4, 11, 5, 14, MARROM_ESCURO)
    s.rect(10, 11, 11, 14, MARROM_ESCURO)
    s.rect(2, 5, 3, 7, MARROM_ESCURO)
    s.rect(11, 7, 12, 7, CORAL)
    s.line(11, 8, 8, 12, CORAL)
    s.px(12, 4, TINTA)
    s.outline(TINTA)
    return s


def bicicleta():
    s = novo()
    for cx in (3, 12):
        s.circle(cx, 11, 3, CINZA_CLARO)
        s.circle(cx, 11, 2, CINZA_CLARO)
        s.px(cx, 11, TINTA)
    s.line(3, 11, 7, 6, CORAL)
    s.line(7, 6, 12, 11, CORAL)
    s.line(3, 11, 8, 11, CORAL)
    s.line(7, 6, 8, 11, CORAL_ESCURO)
    s.rect(5, 4, 8, 4, TINTA)
    s.rect(11, 5, 12, 5, TINTA)
    s.outline(TINTA)
    return s


def boia():
    s = novo()
    s.circle(7, 8, 6, GIZ, fill=True)
    s.circle(7, 8, 6, TINTA)
    s.circle(7, 8, 2, AZUL, fill=True)
    s.circle(7, 8, 2, TINTA)
    for x, y in [(7, 3), (7, 13), (2, 8), (12, 8)]:
        s.rect(x - 1, y - 1, x + 1, y + 1, CORAL)
    s.outline(TINTA)
    return s


def bola():
    s = novo()
    s.circle(7, 8, 6, GIZ, fill=True)
    s.circle(7, 8, 6, GIZ_SOMBRA)
    s.rect(6, 6, 8, 8, TINTA)
    s.line(7, 3, 7, 5, TINTA)
    s.line(3, 7, 5, 7, TINTA)
    s.line(9, 7, 11, 7, TINTA)
    s.line(5, 9, 4, 12, TINTA)
    s.line(9, 9, 10, 12, TINTA)
    s.outline(TINTA)
    return s


def tapete():
    s = novo()
    s.rect(3, 5, 13, 12, VIOLETA)
    s.circle(3, 8, 3, VIOLETA, fill=True)
    s.circle(3, 8, 3, VIOLETA_ESCURO)
    s.circle(3, 8, 1, VIOLETA_ESCURO)
    s.rect(4, 5, 13, 5, VIOLETA_CLARO)
    s.rect(4, 12, 13, 12, VIOLETA_ESCURO)
    s.rect(11, 5, 11, 12, VIOLETA_ESCURO)
    s.outline(TINTA)
    return s


def paralelas():
    s = novo()
    for x in (2, 12):
        s.rect(x, 4, x + 1, 13, CINZA)
        s.rect(x, 4, x, 13, CINZA_CLARO)
    for x in (4, 10):
        s.rect(x, 3, x + 1, 13, CINZA_ESCURO)
    s.rect(2, 13, 13, 14, CINZA_ESCURO)
    s.rect(2, 4, 5, 4, CINZA_CLARO)
    s.rect(10, 3, 13, 3, CINZA_CLARO)
    s.outline(TINTA)
    return s


def despertador():
    s = novo()
    s.circle(3, 3, 2, CORAL, fill=True)
    s.circle(11, 3, 2, CORAL, fill=True)
    s.circle(7, 9, 5, GIZ, fill=True)
    s.circle(7, 9, 5, GIZ_SOMBRA)
    s.rect(7, 6, 7, 9, TINTA)
    s.rect(8, 9, 9, 9, TINTA)
    s.px(5, 14, TINTA)
    s.px(9, 14, TINTA)
    s.outline(TINTA)
    return s


def poste():
    s = novo()
    s.rect(7, 4, 8, 14, CINZA)
    s.rect(7, 4, 7, 14, CINZA_CLARO)
    s.rect(5, 14, 10, 14, CINZA_ESCURO)
    s.rect(4, 0, 11, 1, CINZA_ESCURO)
    s.rect(5, 2, 10, 4, AMARELO)
    s.rect(6, 3, 9, 3, GIZ)
    s.px(3, 4, AMARELO_ESCURO)
    s.px(12, 4, AMARELO_ESCURO)
    s.outline(TINTA)
    return s


def caixa_de_som():
    s = novo()
    s.rect(3, 4, 12, 14, CINZA_ESCURO)
    s.rect(3, 4, 12, 4, CINZA)
    s.circle(7, 7, 2, CINZA_CLARO)
    s.circle(7, 11, 2, CINZA_CLARO, fill=True)
    s.px(7, 7, TINTA)
    s.px(7, 11, TINTA)
    s.rect(5, 1, 10, 3, TINTA)
    s.rect(6, 2, 9, 2, CINZA)
    s.px(11, 6, CORAL)
    s.outline(TINTA)
    return s


def mochila():
    s = novo()
    s.rect(4, 4, 11, 14, VIOLETA)
    s.rect(5, 2, 10, 3, VIOLETA_ESCURO)
    s.rect(4, 4, 11, 4, VIOLETA_CLARO)
    s.rect(5, 8, 10, 12, VIOLETA_ESCURO)
    s.rect(6, 9, 9, 9, AMARELO)
    s.rect(2, 7, 3, 12, CORAL)
    s.rect(12, 7, 13, 12, CORAL)
    s.rect(7, 0, 8, 1, TINTA)
    s.outline(TINTA)
    return s


ENFEITES = {
    "sequencia4": muda,
    "sequencia8": planta,
    "sequencia12": arvore,
    "treinos10": lambda: medalha(BRONZE, BRONZE_ESCURO, CORAL, CORAL_ESCURO),
    "treinos50": lambda: medalha(PRATA, PRATA_ESCURO, VIOLETA, VIOLETA_ESCURO),
    "treinos100": lambda: medalha(AMARELO, AMARELO_ESCURO, CORAL, CORAL_ESCURO),
    "desafiocompleto": trofeu,
    "ratodeacademia": haltere,
    "corredor": cronometro,
    "andarilho": cachorro,
    "ciclista": bicicleta,
    "peixe": boia,
    "boleiro": bola,
    "zen": tapete,
    "pesodocorpo": paralelas,
    "madrugador": despertador,
    "noturno": poste,
    "fimdesemana": caixa_de_som,
    "ecletico": mochila,
}

if __name__ == "__main__":
    folha = Sprite(16 * 10, 16 * 2, palette="wrexercise")
    for i, (nome, fabrica) in enumerate(ENFEITES.items()):
        caminho = AQUI / "saida" / f"enfeite-{nome}.png"
        fabrica().save_png(caminho)
        folha.paste_png(caminho, (i % 10) * 16, (i // 10) * 16)
    folha.save_png(AQUI / "preview" / "folha-8x.png", scale=8, bg="#2b3042")
    print(len(ENFEITES), "enfeites")
