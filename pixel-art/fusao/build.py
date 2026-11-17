"""Faíscas da Fusão do modo Jogo (docs/gamificacao.md §6.5 e decisão 0070).

Seis quadros de 32×32 numa faixa de 192×32: o clarão da batida das duas cartas cresce numa estrela e
se desfaz em faíscas que voam para fora. No traço dos ícones das cartas (pixel-art/cartas): contorno em
tinta por fora, miolo em giz e amarelo, pontas em coral, a cor das Provocações. Desenhado em 16×16 por
quadro e ampliado 2×.

Uso: python build.py   (gera saida/fusao.png, copiado para Wrexercise/webapp/public/fusao.png, e preview/)
"""

import pathlib
import shutil
import sys

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parents[1] / ".claude/skills/pixel-art-studio/scripts"))
from pixelstudio import Sprite  # noqa: E402

PUBLICO = AQUI.parents[1] / "Wrexercise/webapp/public/fusao.png"
CELULA = 16
QUADROS = 6
ESCALA = 2

# As mesmas cores de pixel-art/cartas/build.py.
TINTA = "#14161f"
GIZ = "#f5f1e8"
AMARELO, AMARELO_ESCURO = "#ffd34e", "#d9a02b"
CORAL, CORAL_ESCURO = "#ff684a", "#c4432c"

(AQUI / "saida").mkdir(exist_ok=True)
(AQUI / "preview").mkdir(exist_ok=True)

s = Sprite(CELULA * QUADROS, CELULA, palette="wrexercise")

# As oito direções, das retas para as diagonais.
RETAS = [(1, 0), (-1, 0), (0, 1), (0, -1)]
DIAGONAIS = [(1, 1), (-1, 1), (1, -1), (-1, -1)]


def miolo(quadro, raio, cor):
    """Um quadrado centrado no quadro: o centro fica entre os pixels 7 e 8."""
    x0 = quadro * CELULA
    s.rect(x0 + 8 - raio, 8 - raio, x0 + 7 + raio, 7 + raio, cor)


def raios(quadro, direcoes, de, ate, cor):
    """Raios de 2 px de largura nas retas e de 1 px nas diagonais, do raio `de` ao `ate`."""
    x0 = quadro * CELULA
    for dx, dy in direcoes:
        for r in range(de, ate + 1):
            if dx and dy:
                s.px(x0 + (7 if dx < 0 else 8) + dx * r, (7 if dy < 0 else 8) + dy * r, cor)
            elif dx:
                x = x0 + (7 if dx < 0 else 8) + dx * r
                s.px(x, 7, cor)
                s.px(x, 8, cor)
            else:
                y = (7 if dy < 0 else 8) + dy * r
                s.px(x0 + 7, y, cor)
                s.px(x0 + 8, y, cor)


# 0: o ponto de contato.
miolo(0, 1, GIZ)
raios(0, RETAS, 1, 1, AMARELO)

# 1: o clarão abre numa cruz.
miolo(1, 2, GIZ)
raios(1, RETAS, 2, 4, AMARELO)
raios(1, DIAGONAIS, 2, 2, AMARELO)

# 2: a estrela inteira, com as pontas em coral.
miolo(2, 2, GIZ)
miolo(2, 1, GIZ)
raios(2, RETAS, 2, 4, AMARELO)
raios(2, RETAS, 5, 5, CORAL)
raios(2, DIAGONAIS, 2, 3, AMARELO)
raios(2, DIAGONAIS, 4, 4, CORAL)

# 3: o miolo encolhe e os raios se soltam dele.
miolo(3, 1, AMARELO)
raios(3, RETAS, 3, 4, AMARELO)
raios(3, RETAS, 5, 5, CORAL)
raios(3, DIAGONAIS, 3, 3, AMARELO)
raios(3, DIAGONAIS, 4, 4, CORAL)

# 4: só as faíscas, mais longe.
raios(4, RETAS, 5, 5, CORAL)
raios(4, DIAGONAIS, 4, 4, AMARELO_ESCURO)
raios(4, DIAGONAIS, 5, 5, CORAL)

# 5: as últimas brasas.
raios(5, DIAGONAIS, 5, 5, CORAL_ESCURO)
for dx, dy in RETAS:
    s.px(5 * CELULA + (7 if dx <= 0 else 8) + dx * 5, (7 if dy <= 0 else 8) + dy * 5, CORAL_ESCURO)

s.outline(TINTA, where="outside")

destino = AQUI / "saida" / "fusao.png"
s.save_png(str(destino), scale=ESCALA)
s.preview(str(AQUI / "preview" / "fusao-8x.png"), scale=8, bg="#222633", labels=False)
s.preview(str(AQUI / "preview" / "fusao-giz-8x.png"), scale=8, bg=GIZ, labels=False)
shutil.copyfile(destino, PUBLICO)
print("fusao.png", CELULA * QUADROS * ESCALA, "x", CELULA * ESCALA, "px,", QUADROS, "quadros")
