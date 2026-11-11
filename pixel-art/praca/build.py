"""Praça do modo Jogo, vista em ¾ (docs/gamificacao.md §1.3, §3.1 e §4.4).

As fontes vêm do Gemini, com `prompts-gemini.md`:
- `fonte/praca.png`: a cena inteira, com o céu em magenta;
- `fonte/praca-porta-aberta.png`: a mesma cena, com a porta aberta;
- `fonte/praca-objetos.png`: a mesma cena, só com os objetos soltos no chão; o resto é magenta.

Este script reduz cada fonte para 180×225, ajusta as cores à paleta `wrexercise` e corta a faixa de
céu de cima e um pouco do pé, chegando ao mestre 1× de 180×200. O magenta vira transparente: o céu é
o fundo do aplicativo. Grava:
- `saida/praca.png`: o fundo, com a assinatura "WX";
- `saida/praca-objetos.png`: uma folha com cada objeto solto e a porta aberta, lado a lado.
No fim, imprime a posição de cada recorte para `Wrexercise/webapp/src/features/jogo/ObjetosPraca.tsx`.

Uso: python build.py   (gera saida/ e preview/)
"""

import pathlib
import sys
from collections import Counter

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parents[1] / ".claude/skills/pixel-art-studio/scripts"))
from pixelstudio import Sprite, hex2rgba, nearest, rgba2hex  # noqa: E402

L, A = 180, 200
A_FONTE = 225  # a fonte é 4:5
CORTE_TOPO = 13  # linhas só de céu, acima dos prédios mais altos
TRANSPARENTE = None

s = Sprite(L, A, palette="wrexercise")
paleta = [hex2rgba(c) for c in s.palette]
cache: dict[tuple, str | None] = {}


def cor_da_paleta(rgb):
    if rgb not in cache:
        r, g, b = rgb
        # O magenta do Gemini nunca sai puro; nada da paleta chega perto dele.
        magenta = r > 170 and b > 170 and g < 110
        cache[rgb] = TRANSPARENTE if magenta else rgba2hex(nearest((*rgb, 255), paleta))
    return cache[rgb]


def reduzir(nome: str) -> list[list[str | None]]:
    """Cada bloco de 5×5 fica com a cor mais comum (não a média, que inventaria cores e borraria
    as bordas). Ajustar antes de contar deixa o ruído da compressão fora da contagem."""
    fonte = Image.open(AQUI / "fonte" / nome).convert("RGB")
    fonte = fonte.resize((L * 5, A_FONTE * 5), Image.NEAREST)
    pixels = fonte.load()
    grade = []
    for y in range(CORTE_TOPO, CORTE_TOPO + A):
        linha = []
        for x in range(L):
            bloco = Counter(
                cor_da_paleta(pixels[x * 5 + dx, y * 5 + dy]) for dy in range(5) for dx in range(5)
            )
            linha.append(bloco.most_common(1)[0][0])
        grade.append(linha)
    return grade


cena = reduzir("praca.png")
porta_aberta = reduzir("praca-porta-aberta.png")
objetos = reduzir("praca-objetos.png")

# --- O topo dos prédios distantes some num pontilhado em degraus (Bayer 4×4), sem borrão: a
# silhueta não termina num corte seco contra o fundo do aplicativo. Só o horizonte, à direita do
# volume da escada, se dissolve; a casa de máquinas e as plantas ficam inteiras.
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
HORIZONTE_X, HORIZONTE_Y = 87, (8, 16)  # colunas do horizonte e faixa (topo, base) que se dissolve
PREDIOS = {"#363c52", "#5f6783", "#14161f", "#d9a02b", "#ffd34e"}
for y in range(HORIZONTE_Y[1]):
    fracao = max(0.0, (y - HORIZONTE_Y[0]) / (HORIZONTE_Y[1] - HORIZONTE_Y[0]))
    for x in range(HORIZONTE_X, L):
        if cena[y][x] in PREDIOS and BAYER[y % 4][x % 4] >= fracao * 16:
            cena[y][x] = TRANSPARENTE

for y in range(A):
    for x in range(L):
        if cena[y][x]:
            s.px(x, y, cena[y][x])

# --- Assinatura discreta no trecho liso da mureta, entre a porta e a barra: "WX" (coral e
# violeta), com sombra de 1 px. Texto gerado sai torto. A luminária de parede fica à direita dela.
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


ASSINATURA_X, ASSINATURA_Y = 70, 62
letra(W, ASSINATURA_X, ASSINATURA_Y, CORAL, CORAL_ESCURO)
letra(X, ASSINATURA_X + 7, ASSINATURA_Y, VIOLETA, VIOLETA_PROFUNDO)

s.preview(str(AQUI / "preview" / "praca-2x.png"), scale=2, bg=None, labels=False)
s.save_png(str(AQUI / "saida" / "praca.png"))
s.stats()


# --- Objetos soltos: cada mancha (8-vizinhança) fora do magenta em `praca-objetos.png`.
def manchas(grade) -> list[set[tuple[int, int]]]:
    vistos: set[tuple[int, int]] = set()
    lista = []
    for y0 in range(A):
        for x0 in range(L):
            if not grade[y0][x0] or (x0, y0) in vistos:
                continue
            pilha, mancha = [(x0, y0)], set()
            vistos.add((x0, y0))
            while pilha:
                x, y = pilha.pop()
                mancha.add((x, y))
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        v = (x + dx, y + dy)
                        if 0 <= v[0] < L and 0 <= v[1] < A and v not in vistos and grade[v[1]][v[0]]:
                            vistos.add(v)
                            pilha.append(v)
            lista.append(mancha)
    return [m for m in lista if len(m) >= 12]  # o resto é ruído do magenta


# A porta aberta é um retângulo fixo da fonte: a folha da porta e o vão escuro.
PORTA = (33, 33, 60, 80)  # x0, y0, x1, y1 (exclusivo), já sem o corte do topo


def caixa(mancha):
    xs = [x for x, _ in mancha]
    ys = [y for _, y in mancha]
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


recortes = []  # (nome, x0, y0, x1, y1, pixels {(x, y): cor})
NOMES = {"caixotes": (0, 60), "barra": (60, 140), "floreira": (140, 180)}
for mancha in sorted(manchas(objetos), key=lambda m: caixa(m)[0]):
    x0, y0, x1, y1 = caixa(mancha)
    if y0 > 150:
        nome = "floreira-esquerda" if x0 < L / 2 else "floreira-direita"
    else:
        nome = next(n for n, (a, b) in NOMES.items() if a <= (x0 + x1) / 2 < b)
    recortes.append((nome, x0, y0, x1, y1, {(x, y): objetos[y][x] for x, y in mancha}))
px0, py0, px1, py1 = PORTA
recortes.append(
    ("porta", px0, py0, px1, py1, {(x, y): porta_aberta[y][x] for y in range(py0, py1) for x in range(px0, px1)})
)

folha = Image.new("RGBA", (sum(r[3] - r[1] for r in recortes), max(r[4] - r[2] for r in recortes)))
sx = 0
print("\n// Cole em ObjetosPraca.tsx (px do mestre 1×):")
for nome, x0, y0, x1, y1, pixels in recortes:
    for (x, y), cor in pixels.items():
        if cor:
            folha.putpixel((sx + x - x0, y - y0), hex2rgba(cor))
    print(f'  {{ nome: "{nome}", sx: {sx}, x: {x0}, y: {y0}, largura: {x1 - x0}, altura: {y1 - y0} }},')
    sx += x1 - x0
folha.save(AQUI / "saida" / "praca-objetos.png", optimize=True)
folha.resize((folha.width * 4, folha.height * 4), Image.NEAREST).save(AQUI / "preview" / "praca-objetos-4x.png")
