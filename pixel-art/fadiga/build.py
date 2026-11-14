"""Ícone da FADIGA do modo Jogo (docs/gamificacao.md §5.4 e decisão 0059).

Bateria deitada, com uma só barra coral de carga, no traço dos ícones das cartas (pixel-art/cartas):
contorno em tinta por fora, corpo em giz, polo à direita. Desenhado em 16×16 e ampliado 2× para 32×32,
o mesmo tamanho dos ícones das cartas.

Uso: python build.py   (gera saida/fadiga.png, copiado para Wrexercise/webapp/public/fadiga.png, e preview/)
"""

import pathlib
import shutil
import sys

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parents[1] / ".claude/skills/pixel-art-studio/scripts"))
from pixelstudio import Sprite  # noqa: E402

PUBLICO = AQUI.parents[1] / "Wrexercise/webapp/public/fadiga.png"
LADO = 32

# As mesmas cores de pixel-art/cartas/build.py.
TINTA = "#14161f"
GIZ, GIZ_SOMBRA = "#f5f1e8", "#d6d0c0"
CORAL, CORAL_ESCURO = "#ff684a", "#c4432c"
CINZA = "#5f6783"

(AQUI / "saida").mkdir(exist_ok=True)
(AQUI / "preview").mkdir(exist_ok=True)


def bateria_fraca():
    s = Sprite(16, 16, palette="wrexercise")
    # Corpo, de x 1 a 12 e y 4 a 11, com sombra na base.
    s.rect(1, 4, 12, 11, GIZ)
    s.rect(1, 11, 12, 11, GIZ_SOMBRA)
    # Polo positivo.
    s.rect(13, 6, 14, 9, CINZA)
    # Uma barra de carga só, no fim: quase vazia.
    s.rect(2, 5, 4, 10, CORAL)
    s.rect(2, 10, 4, 10, CORAL_ESCURO)
    s.outline(TINTA, where="outside")
    # Divisões vazias onde estariam as outras barras.
    for x in (6, 9):
        s.rect(x, 6, x + 1, 9, GIZ_SOMBRA)
    return s


sprite = bateria_fraca()
destino = AQUI / "saida" / "fadiga.png"
sprite.save_png(str(destino), scale=LADO // 16)
sprite.preview(str(AQUI / "preview" / "fadiga-8x.png"), scale=8, bg="#222633", labels=False)
shutil.copyfile(destino, PUBLICO)
print("fadiga.png", LADO, "px")
