"""Vida na praça do modo Jogo (docs/gamificacao.md §3.4): pombo, bola e luminárias.

A fonte é `fonte/folha.png`, gerada no Gemini com `prompts-gemini.md`: 20 sprites numa grade de
4×5 sobre magenta. O Gemini desenhou cada pixel com cerca de 6,3 px, o dobro do tamanho pedido;
este script reduz cada sprite pela metade (blocos de 12,6 px, pela cor mais comum, não pela média),
ajusta as cores à paleta `wrexercise` e põe cada quadro numa célula de 16×16.

A posição na célula não vem da fonte, onde o Gemini moveu o pombo de um quadro para outro: cada
linha tem uma âncora fixa, para a animação não tremer.

Uso: python build.py   (gera saida/ambiente.png e preview/ambiente-4x.png;
copie saida/ambiente.png para Wrexercise/webapp/public/ambiente.png)
"""

import pathlib
import sys
from collections import Counter

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parents[1] / ".claude/skills/pixel-art-studio/scripts"))
from pixelstudio import Sprite, hex2rgba, nearest  # noqa: E402

FONTE = AQUI / "fonte" / "folha.png"
COLUNAS, LINHAS = 4, 5
LADO = 16  # célula da saída
BLOCO = 12.6  # px da fonte por pixel da saída

paleta = [hex2rgba(c) for c in Sprite(1, 1, palette="wrexercise").palette]
CORAIS = {hex2rgba(c)[:3] for c in ("#ff684a", "#c4432c", "#ff9c80")}

fonte = Image.open(FONTE).convert("RGB")
largura, altura = fonte.size
px = fonte.load()
cache: dict[tuple, tuple] = {}


def e_fundo(c):
    # Magenta e o rosa da borda que o Gemini mistura com ele: a paleta não tem nenhum dos dois.
    r, g, b = c
    return r > 170 and b > 170 and g < 100


def da_paleta(c):
    if c not in cache:
        cache[c] = tuple(nearest((*c, 255), paleta))
    return cache[c]


def caixa_da_celula(col, lin):
    cw, ch = largura / COLUNAS, altura / LINHAS
    x0, y0, x1, y1 = int(col * cw), int(lin * ch), int((col + 1) * cw), int((lin + 1) * ch)
    xs, ys = [], []
    for y in range(y0, y1):
        for x in range(x0, x1):
            if not e_fundo(px[x, y]):
                xs.append(x)
                ys.append(y)
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def reduzir(caixa):
    """O sprite em pixels da saída, com fundo transparente. A grade começa na borda do sprite."""
    x0, y0, x1, y1 = caixa
    w, h = round((x1 - x0) / BLOCO), round((y1 - y0) / BLOCO)
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for oy in range(h):
        for ox in range(w):
            votos = Counter()
            for y in range(int(y0 + oy * BLOCO), min(y1, int(y0 + (oy + 1) * BLOCO))):
                for x in range(int(x0 + ox * BLOCO), min(x1, int(x0 + (ox + 1) * BLOCO))):
                    c = px[x, y]
                    votos[None if e_fundo(c) else da_paleta(c)] += 1
            cor = votos.most_common(1)[0][0]
            if cor is not None:
                im.putpixel((ox, oy), cor)
    return im


def centro_dos_pes(im):
    """O x médio dos pixels coral nas duas linhas de baixo: os pés do pombo."""
    xs = [
        x
        for y in range(im.height - 2, im.height)
        for x in range(im.width)
        if im.getpixel((x, y))[3] and im.getpixel((x, y))[:3] in CORAIS
    ]
    return round(sum(xs) / len(xs)) if xs else im.width // 2


def bico(im):
    """A linha do pixel mais à direita: o bico do pombo em voo."""
    x = im.width - 1
    ys = [y for y in range(im.height) if im.getpixel((x, y))[3]]
    return ys[len(ys) // 2]


def ancorar(lin, im):
    """Onde o canto de cima à esquerda do sprite fica na célula de 16×16."""
    if lin in (0, 1):  # pombo no chão: pés no fundo da célula, sempre no mesmo x
        return 7 - centro_dos_pes(im), LADO - im.height
    if lin == 2:  # pombo em voo: o bico fica parado e as asas batem
        return LADO - im.width, 8 - bico(im)
    if lin == 3:  # bola: no centro, apoiada no fundo
        return (LADO - im.width) // 2, LADO - im.height
    return (LADO - im.width) // 2, LADO - im.height  # luminárias: apoiadas no fundo


BOLA = [
    "...###...",
    ".##...##.",
    ".#.....#.",
    "#.......#",
    "#.......#",
    "#.......#",
    ".#.....#.",
    ".##...##.",
    "...###...",
]


def bola(quadro):
    """A bola de 9×9, desenhada aqui: reduzida da fonte, ela perdia o contorno redondo.

    A listra passa pelo centro e gira 45° por quadro no sentido horário, então a bola rola para a
    direita: vertical, /, horizontal, \\.
    """
    na_listra = [
        lambda x, y: x == 4,
        lambda x, y: x + y == 8,
        lambda x, y: y == 4,
        lambda x, y: x == y,
    ][quadro]
    im = Image.new("RGBA", (9, 9), (0, 0, 0, 0))
    for y, linha in enumerate(BOLA):
        dentro = [x for x, c in enumerate(linha) if c == "#"]
        for x, c in enumerate(linha):
            if c == "#":
                im.putpixel((x, y), hex2rgba("#14161f"))
            elif dentro and dentro[0] < x < dentro[-1]:
                sombra = x + y > 9  # luz do alto à esquerda
                if na_listra(x, y):
                    cor = "#d6d0c0" if sombra else "#f5f1e8"
                elif (x, y) in ((2, 2), (3, 2), (2, 3)):
                    cor = "#ff9c80"
                else:
                    cor = "#c4432c" if sombra else "#ff684a"
                im.putpixel((x, y), hex2rgba(cor))
    return im


saida = Image.new("RGBA", (LADO * COLUNAS, LADO * LINHAS), (0, 0, 0, 0))
for lin in range(LINHAS):
    for col in range(COLUNAS):
        im = bola(col) if lin == 3 else reduzir(caixa_da_celula(col, lin))
        x, y = ancorar(lin, im)
        if x < 0 or y < 0 or x + im.width > LADO or y + im.height > LADO:
            print(f"aviso: o sprite {lin * COLUNAS + col + 1} ({im.width}×{im.height}) passa da célula em ({x}, {y})")
        celula = Image.new("RGBA", (LADO, LADO), (0, 0, 0, 0))
        celula.alpha_composite(im, (max(x, 0), max(y, 0)), (max(-x, 0), max(-y, 0)))
        saida.alpha_composite(celula, (col * LADO, lin * LADO))

(AQUI / "saida").mkdir(exist_ok=True)
(AQUI / "preview").mkdir(exist_ok=True)
saida.save(AQUI / "saida" / "ambiente.png")
previa = Image.new("RGBA", saida.size, hex2rgba("#222633"))
previa.alpha_composite(saida)
previa.resize((saida.width * 4, saida.height * 4), Image.NEAREST).save(AQUI / "preview" / "ambiente-4x.png")
print("saida/ambiente.png", saida.size)
