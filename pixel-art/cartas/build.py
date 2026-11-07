"""Moldura e ícones das cartas do modo Jogo (docs/gamificacao.md §1.3 e §4.4).

- Moldura da Provocação, 24×32: contorno grosso escuro, cantos recortados, faixa coral no topo
  com "linhas de movimento" e corpo em giz. Feita para `border-image` (fatias de 10/6 px): a
  faixa e as bordas repetem a cada 8 px, então a carta pode ter qualquer tamanho.
- Ícones 16×16 das cinco Provocações.

A moldura da Resposta (acento violeta, faixa em halftone) e os ícones das Respostas entram na G4.
Uso: python build.py   (gera saida/*.png e preview/*.png)
"""

import pathlib
import sys

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parents[1] / ".claude/skills/pixel-art-studio/scripts"))
from pixelstudio import Sprite  # noqa: E402

TINTA = "#14161f"  # contorno
GIZ, GIZ_SOMBRA, GIZ_FUNDO = "#f5f1e8", "#d6d0c0", "#a9a392"
CORAL, CORAL_ESCURO, CORAL_CLARO = "#ff684a", "#c4432c", "#ff9c80"
VIOLETA, VIOLETA_CLARO = "#7957e8", "#a58cf4"
AMARELO, AMARELO_ESCURO = "#ffd34e", "#d9a02b"
LIMA, LIMA_ESCURO = "#a8d978", "#67a24b"
CINZA = "#5f6783"

(AQUI / "saida").mkdir(exist_ok=True)
(AQUI / "preview").mkdir(exist_ok=True)


# --- Moldura -----------------------------------------------------------------

def moldura_provocacao():
    w, h = 24, 32
    s = Sprite(w, h, palette="wrexercise")

    def dentro(x, y):
        if not (0 <= x < w and 0 <= y < h):
            return False
        # Cantos recortados: diagonal de 3 px no topo à esquerda e embaixo à direita, 1 px nos outros.
        if x + y < 3 or (w - 1 - x) + (h - 1 - y) < 3:
            return False
        if (x, y) in {(w - 1, 0), (0, h - 1)}:
            return False
        return True

    def borda(x, y, espessura):
        return any(
            not dentro(x + dx, y + dy)
            for dx in range(-espessura, espessura + 1)
            for dy in range(-espessura, espessura + 1)
            if abs(dx) + abs(dy) <= espessura
        )

    for y in range(h):
        for x in range(w):
            if not dentro(x, y):
                continue
            if borda(x, y, 2):
                cor = TINTA
            elif y <= 8:
                cor = CORAL
            elif y == 9:
                cor = CORAL_ESCURO
            elif y == h - 3:
                cor = GIZ_SOMBRA
            else:
                cor = GIZ
            s.px(x, y, cor)

    # Linhas de movimento: traços claros com período de 8 px, deslocados a cada linha.
    # Dois traços longos e um curto, afinando para a direita, como num quadrinho.
    for y, inicio, comprimento in [(4, 1, 6), (6, 5, 3)]:
        for x in range(2, w - 2):
            if (x - inicio) % 8 < comprimento and y <= 8 and 2 <= x <= 21:
                s.px(x, y, CORAL_CLARO)

    return s


# --- Ícones ------------------------------------------------------------------

def icone():
    return Sprite(16, 16, palette="wrexercise")


def cronometro(fatia):
    """Cronômetro com uma fatia coral: 1/6 do mostrador para +10 MIN, 1/3 para +20 MIN."""
    import math

    s = icone()
    cx, cy, r = 7.5, 9.0, 5.6
    for y in range(16):
        for x in range(16):
            d = math.hypot(x - cx, y - cy)
            if d <= r:
                ang = (math.degrees(math.atan2(x - cx, -(y - cy))) + 360) % 360
                s.px(x, y, CORAL if ang < fatia and d > 0.8 else GIZ)
    # Coroa e botão.
    s.rect(6, 1, 9, 1, CINZA)
    s.rect(7, 2, 8, 2, CINZA)
    s.px(12, 3, CINZA)
    s.outline(TINTA, where="outside")
    # Ponteiro a partir do centro, na borda da fatia.
    ang = math.radians(fatia)
    for t in (1, 2, 3):
        s.px(round(cx + math.sin(ang) * t), round(cy - math.cos(ang) * t), TINTA)
    s.px(7, 9, TINTA)
    s.px(8, 9, TINTA)
    return s


def variedade():
    """Duas setas que se cruzam (embaralhar): trocar de tipo de treino."""
    s = icone()
    def seta(cor, de_baixo):
        # Haste de 2 px na diagonal e ponta em "L", com a quina na ponta da seta.
        for i in range(9):
            y = 12 - i if de_baixo else 3 + i
            s.px(2 + i, y, cor)
            s.px(3 + i, y, cor)
        y_ponta = 2 if de_baixo else 13
        s.rect(9, y_ponta, 13, y_ponta, cor)
        y0, y1 = (2, 6) if de_baixo else (9, 13)
        s.rect(13, y0, 13, y1, cor)

    seta(VIOLETA, de_baixo=True)
    seta(LIMA, de_baixo=False)  # por cima: as setas se cruzam
    s.outline(TINTA, where="outside")
    return s


def fim_de_semana():
    """Folha de calendário com as duas últimas colunas (sábado e domingo) em coral."""
    s = icone()
    s.rect(1, 3, 14, 14, GIZ)
    s.rect(1, 3, 14, 5, CORAL)  # cabeçalho
    s.outline(TINTA, where="inside")
    # Argolas.
    for x in (4, 11):
        s.rect(x, 1, x, 3, TINTA)
    # Grade de dias: 5 colunas de 2 px; as duas da direita são o fim de semana.
    for linha, y in enumerate((7, 10)):
        for coluna in range(5):
            x = 3 + coluna * 2
            fds = coluna >= 3
            s.px(x if not fds else x + 1, y, CORAL if fds else CINZA)
            s.px(x if not fds else x + 1, y + 1, CORAL_ESCURO if fds else GIZ_FUNDO)
    s.px(12, 13, GIZ_SOMBRA)
    s.px(3, 13, GIZ_SOMBRA)
    return s


def antes_das_8():
    """Sol nascendo atrás da linha do horizonte: treino de manhã cedo."""
    import math

    s = icone()
    cx, cy, r = 7.5, 11.0, 4.8
    for y in range(16):
        for x in range(16):
            if math.hypot(x - cx, y - cy) <= r and y <= 10:
                s.px(x, y, AMARELO if y < 9 else AMARELO_ESCURO)
    s.outline(TINTA, where="outside")
    # Raios curtos, sem contorno, saindo do sol.
    for x, y in [(7, 2), (8, 2), (2, 5), (3, 6), (13, 5), (12, 6), (0, 9), (1, 9), (14, 9), (15, 9)]:
        s.px(x, y, AMARELO_ESCURO)
    # Horizonte e marcação tracejada da quadra.
    s.rect(0, 11, 15, 11, TINTA)
    for x in range(2, 14):
        if x % 4 < 3:
            s.px(x, 13, CINZA)
    return s


# --- Exportação --------------------------------------------------------------

pecas = {
    "carta-provocacao": moldura_provocacao(),
    "carta-mais10min": cronometro(60),
    "carta-mais20min": cronometro(120),
    "carta-variedade": variedade(),
    "carta-fimdesemana": fim_de_semana(),
    "carta-antesdas8": antes_das_8(),
}

for nome, sprite in pecas.items():
    sprite.save_png(str(AQUI / "saida" / f"{nome}.png"))
    sprite.preview(str(AQUI / "preview" / f"{nome}-8x.png"), scale=8, bg="#f5f1e8" if nome != "carta-provocacao" else "#222633", labels=False)
    print(nome, len(sprite.used_colors()), "cores")
