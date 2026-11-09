"""Moldura e ícones das cartas do modo Jogo (docs/gamificacao.md §1.3 e §4.4).

- Moldura da Provocação, 24×32: contorno grosso escuro, cantos recortados, faixa coral no topo
  com "linhas de movimento" e corpo em giz. Feita para `border-image` (fatias de 10/6 px): a
  faixa e as bordas repetem a cada 8 px, então a carta pode ter qualquer tamanho.
- Moldura da Resposta, 24×32: a mesma forma, com faixa violeta em halftone (G4).
- Ícones 32×32 das cinco Provocações e das quatro Respostas.

Se existir fonte/icones.png (uma folha gerada no Gemini, ver prompts-gemini.md), cada ícone sai
dela: a célula da grade de 3×3 é reduzida para 32×32 por moda de cor, com a mesma escala para
todos, e as cores vão para a paleta. Sem a fonte, o desenho de 16×16 deste script é ampliado 2×.

Uso: python build.py   (gera saida/*.png e preview/*.png)
"""

import pathlib
import sys

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parents[1] / ".claude/skills/pixel-art-studio/scripts"))
sys.path.insert(0, str(AQUI.parent))
from fonte_gerada import cores_do_script, recortar_grade  # noqa: E402
from pixelstudio import Sprite  # noqa: E402

FONTE = AQUI / "fonte" / "icones.png"
LADO = 32  # tamanho final de cada ícone

TINTA = "#14161f"  # contorno
GIZ, GIZ_SOMBRA, GIZ_FUNDO = "#f5f1e8", "#d6d0c0", "#a9a392"
CORAL, CORAL_ESCURO, CORAL_CLARO = "#ff684a", "#c4432c", "#ff9c80"
VIOLETA, VIOLETA_ESCURO, VIOLETA_CLARO = "#7957e8", "#4b35a6", "#a58cf4"
AMARELO, AMARELO_ESCURO = "#ffd34e", "#d9a02b"
LIMA, LIMA_ESCURO = "#a8d978", "#67a24b"
CINZA = "#5f6783"

(AQUI / "saida").mkdir(exist_ok=True)
(AQUI / "preview").mkdir(exist_ok=True)


# --- Moldura -----------------------------------------------------------------

def moldura(faixa, faixa_escura):
    """A forma comum das duas molduras, com a faixa do topo na cor do tipo."""
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
                cor = faixa
            elif y == 9:
                cor = faixa_escura
            elif y == h - 3:
                cor = GIZ_SOMBRA
            else:
                cor = GIZ
            s.px(x, y, cor)
    return s


def moldura_provocacao():
    s = moldura(CORAL, CORAL_ESCURO)
    w = 24

    # Linhas de movimento: traços claros com período de 8 px, deslocados a cada linha.
    # Dois traços longos e um curto, afinando para a direita, como num quadrinho.
    for y, inicio, comprimento in [(4, 1, 6), (6, 5, 3)]:
        for x in range(2, w - 2):
            if (x - inicio) % 8 < comprimento and y <= 8 and 2 <= x <= 21:
                s.px(x, y, CORAL_CLARO)

    return s


def moldura_resposta():
    s = moldura(VIOLETA, VIOLETA_ESCURO)
    # Halftone: pontos claros que rareiam de cima para baixo, com período de 8 px na horizontal.
    for y, passo, desvio in [(3, 2, 0), (5, 2, 1), (7, 4, 1)]:
        for x in range(2, 22):
            if (x - desvio) % passo == 0 and s.get(x, y) == s.get(12, 5):
                s.px(x, y, VIOLETA_CLARO)
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


def desafio_duplo():
    """Duas setas em sentidos opostos: o mesmo desafio vai e volta."""
    s = icone()
    # Seta coral para a direita, em cima.
    s.rect(1, 3, 10, 4, CORAL)
    for i in range(3):
        s.rect(11 + i, 1 + i, 11 + i, 6 - i, CORAL)
    # Seta violeta para a esquerda, embaixo, com uma linha de folga entre as duas.
    s.rect(4, 11, 14, 12, VIOLETA)
    for i in range(3):
        s.rect(4 - i, 9 + i, 4 - i, 14 - i, VIOLETA)
    s.outline(TINTA, where="outside")
    return s


def aposta():
    """Ficha amarela com um 2: a recompensa dobra."""
    import math

    s = icone()
    cx, cy, r = 7.5, 7.5, 6.6
    for y in range(16):
        for x in range(16):
            d = math.hypot(x - cx, y - cy)
            if d <= r:
                s.px(x, y, AMARELO_ESCURO if d > r - 1.2 else AMARELO)
    s.outline(TINTA, where="outside")
    # Algarismo 2 em 4×6, em tinta.
    for x, y in [(6, 4), (7, 4), (8, 4), (9, 5), (9, 6), (8, 7), (7, 8), (6, 9),
                 (6, 10), (7, 10), (8, 10), (9, 10), (5, 5)]:
        s.px(x, y, TINTA)
    return s


def prazo_extra():
    """Ampulheta com um sinal de mais: 24 horas a mais."""
    s = icone()
    # Tampas.
    s.rect(2, 1, 11, 2, CINZA)
    s.rect(2, 13, 11, 14, CINZA)
    # Vidro: dois triângulos que se tocam no meio.
    for i in range(5):
        s.rect(3 + i, 3 + i, 10 - i, 3 + i, GIZ)
        s.rect(3 + i, 12 - i, 10 - i, 12 - i, GIZ)
    # Areia: pouca em cima, monte embaixo.
    s.rect(5, 5, 8, 5, AMARELO)
    s.rect(6, 6, 7, 6, AMARELO)
    s.rect(6, 7, 7, 9, AMARELO_ESCURO)
    s.rect(5, 11, 8, 11, AMARELO)
    s.rect(4, 12, 9, 12, AMARELO)
    s.outline(TINTA, where="outside")
    # Mais, em lima, no canto.
    s.rect(11, 9, 15, 11, TINTA)
    s.rect(12, 7, 14, 13, TINTA)
    s.rect(12, 10, 14, 10, LIMA)
    s.rect(13, 8, 13, 12, LIMA)
    return s


def mudanca_de_planos():
    """Placa de rua com duas setas para lados opostos: trocar o objetivo."""
    s = icone()
    s.rect(7, 2, 8, 15, CINZA)  # poste
    # Placa de cima, para a direita (lima).
    s.rect(3, 2, 12, 5, LIMA)
    s.rect(13, 3, 13, 4, LIMA)
    # Placa de baixo, para a esquerda (coral).
    s.rect(4, 8, 12, 11, CORAL)
    s.rect(3, 9, 3, 10, CORAL)
    s.outline(TINTA, where="outside")
    s.rect(4, 3, 10, 3, LIMA_ESCURO)
    s.rect(6, 9, 11, 9, CORAL_ESCURO)
    return s


# --- Exportação --------------------------------------------------------------

molduras = {
    "carta-provocacao": moldura_provocacao(),
    "carta-resposta": moldura_resposta(),
}

# Na ordem da grade de fonte/icones.png.
icones = {
    "carta-mais10min": lambda: cronometro(60),
    "carta-mais20min": lambda: cronometro(120),
    "carta-variedade": variedade,
    "carta-fimdesemana": fim_de_semana,
    "carta-antesdas8": antes_das_8,
    "carta-desafioduplo": desafio_duplo,
    "carta-aposta": aposta,
    "carta-prazoextra": prazo_extra,
    "carta-mudancadeplanos": mudanca_de_planos,
}

for nome, sprite in molduras.items():
    sprite.save_png(str(AQUI / "saida" / f"{nome}.png"))
    sprite.preview(str(AQUI / "preview" / f"{nome}-8x.png"), scale=8, bg="#222633", labels=False)

if FONTE.exists():
    cores = cores_do_script(globals(), Sprite(1, 1, palette="wrexercise").palette)
    for nome, imagem in zip(icones, recortar_grade(FONTE, 3, 3, len(icones), LADO, cores, na_base=False)):
        imagem.save(AQUI / "saida" / f"{nome}.png")
    print("fonte:", FONTE.name)
else:
    for nome, fabrica in icones.items():
        fabrica().save_png(str(AQUI / "saida" / f"{nome}.png"), scale=LADO // 16)

folha = Sprite(LADO * 3, LADO * 3)
for i, nome in enumerate(icones):
    folha.paste_png(AQUI / "saida" / f"{nome}.png", (i % 3) * LADO, (i // 3) * LADO)
folha.save_png(AQUI / "preview" / "icones-4x.png", scale=4, bg="#f5f1e8")
print(len(molduras), "molduras e", len(icones), "ícones")
