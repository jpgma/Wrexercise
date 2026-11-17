"""Quadros das animações de dormir do `jpgma` a partir das folhas geradas no Gemini.

Cada folha fica em fonte/gemini/<animacao>-<direcao>.png (por exemplo dormindo-south.png): uma
grade de quadros sobre magenta, gerada com os prompts de prompts-gemini.md a partir de
saida/referencia-gemini/<direcao>.png. É a versão do `jpgma` de
pixel-art/base-masculino/gemini.py. Para cada folha, este script:
- separa cada quadro do magenta e descarta as manchas soltas;
- reduz todos com a mesma escala, a do quadro de calibração (a cópia da pose parada): a altura
  dele vira a altura do `jpgma` na linha `rotacao` de saida/jpgma.png;
- leva a cor de cada bloco para a mais próxima de PALETA e redesenha o contorno de 1 px;
- põe o quadro de calibração onde a `rotacao` está na célula de 48×48 (mesmo chão, mesmas
  pernas); os outros acompanham as pernas dele, e os sentados ficam sobre o primeiro sentado de
  `dormindo`, para a troca entre as animações não pular.

Uso: python gemini.py [arquivo ...]   (sem argumentos: todas as folhas de fonte/gemini/)
Gera saida/gemini/<animacao>-<direcao>.png (os quadros lado a lado, 48×48 cada) e
preview/gemini-<animacao>-<direcao>-6x.png (a pose parada e os quadros).
"""

from __future__ import annotations

import pathlib
import sys
from collections import deque

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
FONTE = AQUI / "fonte" / "gemini"
SAIDA = AQUI / "saida" / "gemini"
FOLHA = AQUI / "saida" / "jpgma.png"

N = 48  # célula da folha
DIRECOES = ["south", "southeast", "east", "northeast", "north", "northwest", "west", "southwest"]

# As 18 cores do `jpgma` (PALETA do build.py); a primeira é o contorno.
PALETA = [
    "#0e071b", "#150b0d", "#1c1215", "#1f1a21", "#282128", "#291a1b", "#2f2930", "#322c33",
    "#363037", "#392627", "#493334", "#8a523e", "#c26c4d", "#dcd1d9", "#e0855a", "#f7a972",
    "#e74647", "#702623",
]

# Células de cada folha e a grade (colunas, linhas) por quantidade de células.
QUADROS = {"dormindo": 4, "adormecer": 8, "acordar": 6, "levantar": 8}
GRADES = {4: (2, 2), 6: (3, 2), 8: (4, 2)}
# Célula de pé, cópia da referência, de onde saem a escala e o chão.
CALIBRACAO = {"dormindo": 0, "adormecer": 0, "acordar": 0, "levantar": 7}
# Células que ficam no chão da calibração, mesmo que o Gemini as desenhe 1 ou 2 px acima (as
# outras guardam a altura em relação a ela: o 3º de `acordar` é o susto, no ar).
NO_CHAO = {"dormindo": range(4), "adormecer": range(8), "acordar": (0, 1, 3, 4, 5), "levantar": range(8)}
# Células sentadas: são postas sobre a pose sentada de `dormindo` (a 2ª célula da tira dele).
SENTADOS = {"dormindo": (1, 2, 3), "adormecer": (4, 5, 6, 7), "acordar": (1, 3, 4, 5), "levantar": (0, 1, 2, 3)}

MANCHA = 0.5  # componente com menos que esta fração de um bloco é mancha solta
MARGEM = 0.2  # fração do bloco ignorada em cada borda ao medir a cor (fica só o miolo)
PERNAS = 8  # linhas de baixo (em pixels do Jogo) usadas para alinhar os quadros pelas pernas
AJUSTE = 3  # deslocamento máximo (px) do ajuste fino dos sentados
BASE = 7  # linhas de baixo comparadas no ajuste fino (pernas e tênis, que não se mexem)


def rgb(c: str) -> tuple[int, int, int]:
    return int(c[1:3], 16), int(c[3:5], 16), int(c[5:7], 16)


CORES = [rgb(c) for c in PALETA]
CONTORNO = CORES[0]


def magenta(p) -> bool:
    r, g, b = p
    return r > 150 and b > 150 and g < 110


VERMELHOS = {CORES[16], CORES[17]}  # a boca
MARRONS = {CORES[5], CORES[9], CORES[10]}  # cabelo e barba
CINZAS = {CORES[3], CORES[4], CORES[6], CORES[7], CORES[8]}  # camiseta e calça


def mais_proxima(p) -> tuple[int, int, int]:
    """A cor de PALETA mais próxima, sem trocar de material: o Gemini às vezes esquenta a folha,
    e aí a roupa cairia no marrom do cabelo e o cabelo no vermelho da boca. Vermelho só para
    bloco bem vermelho; marrom só para bloco quente, e cinza só para bloco neutro."""
    r, g, b = p
    fora = set()
    if r < 2.2 * max(g, b):
        fora |= VERMELHOS
    fora |= CINZAS if r >= 1.4 * g and b < 0.85 * r else MARRONS
    return min((c for c in CORES if c not in fora), key=lambda c: sum((a - b) ** 2 for a, b in zip(p, c)))


def mascara(px, x0, y0, w, h, area_min) -> set[tuple[int, int]]:
    """Pixels do personagem na célula, sem os componentes menores que area_min."""
    livre = {(x, y) for y in range(h) for x in range(w) if not magenta(px[x0 + x, y0 + y])}
    m: set[tuple[int, int]] = set()
    while livre:
        comp = [livre.pop()]
        fila = deque(comp)
        while fila:
            x, y = fila.popleft()
            for v in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if v in livre:
                    livre.remove(v)
                    comp.append(v)
                    fila.append(v)
        if len(comp) >= area_min:
            m.update(comp)
    return m


def reduzir(px, x0, y0, m, bx, by, tw, th, escala) -> dict[tuple[int, int], tuple]:
    """Cor de cada bloco: a de PALETA mais próxima da média do miolo; vazio quando o bloco é
    mais magenta."""
    grade = {}
    borda = escala * MARGEM
    for ty in range(th):
        for tx in range(tw):
            soma, dentro, total = [0, 0, 0], 0, 0
            for sy in range(round(by + ty * escala + borda), round(by + (ty + 1) * escala - borda)):
                for sx in range(round(bx + tx * escala + borda), round(bx + (tx + 1) * escala - borda)):
                    total += 1
                    if (sx, sy) in m:
                        dentro += 1
                        for k, v in enumerate(px[x0 + sx, y0 + sy]):
                            soma[k] += v
            if total and dentro >= total / 2:
                grade[(tx, ty)] = mais_proxima(tuple(v / dentro for v in soma))
    return grade


def desenhar(grade: dict[tuple[int, int], tuple]) -> dict[tuple[int, int], tuple]:
    """A grade com o contorno de 1 px redesenhado na borda."""
    out = {}
    for (tx, ty), c in grade.items():
        viz = ((tx + 1, ty), (tx - 1, ty), (tx, ty + 1), (tx, ty - 1))
        out[(tx, ty)] = CONTORNO if any(v not in grade for v in viz) else c
    return out


def centro_pernas(pixels: dict, base: int) -> float:
    """Centro horizontal das PERNAS linhas de baixo (base é a linha depois da última)."""
    xs = [x for x, y in pixels if y >= base - PERNAS]
    return (min(xs) + max(xs) + 1) / 2


def ler(im: Image.Image) -> dict[tuple[int, int], tuple]:
    px = im.convert("RGBA").load()
    return {(x, y): px[x, y][:3] for y in range(im.height) for x in range(im.width) if px[x, y][3]}


def sentado_de_dormindo(direcao: str) -> dict[tuple[int, int], tuple] | None:
    arq = SAIDA / f"dormindo-{direcao}.png"
    if not arq.exists():
        return None
    return ler(Image.open(arq).crop((N, 0, 2 * N, N)))


def processar(arq: pathlib.Path) -> None:
    animacao, _, direcao = arq.stem.partition("-")
    if animacao not in QUADROS or direcao not in DIRECOES:
        raise SystemExit(f"{arq.name}: o nome deve ser <animacao>-<direcao>.png")
    n = QUADROS[animacao]
    cols, linhas = GRADES[n]

    i_dir = DIRECOES.index(direcao)
    ref_im = Image.open(FOLHA).convert("RGBA").crop((i_dir * N, 0, (i_dir + 1) * N, N))
    ref = ler(ref_im)
    chao = max(y for _, y in ref) + 1  # a linha depois da última do personagem parado
    alt_ref = chao - min(y for _, y in ref)

    img = Image.open(arq).convert("RGB")
    px = img.load()
    cw, ch = img.width // cols, img.height // linhas

    # 1. Caixa de cada célula; a escala sai da de calibração (pose parada).
    area_min = (img.height / linhas / alt_ref / 2) ** 2 * MANCHA
    celulas = []
    for i in range(n):
        x0, y0 = (i % cols) * cw, (i // cols) * ch
        m = mascara(px, x0, y0, cw, ch, area_min)
        if not m:
            raise SystemExit(f"{arq.name}: quadro {i + 1} vazio")
        xs = [x for x, _ in m]
        ys = [y for _, y in m]
        celulas.append((x0, y0, m, min(xs), min(ys), max(xs) + 1, max(ys) + 1))
    k = CALIBRACAO[animacao]
    y1max = celulas[k][6]
    escala = (y1max - celulas[k][4]) / alt_ref

    # 2. Cada célula numa grade de blocos alinhada à borda do próprio personagem.
    grades = []
    for x0, y0, m, xmin, ymin, xmax, ymax in celulas:
        tw, th = round((xmax - xmin) / escala), round((ymax - ymin) / escala)
        grades.append((desenhar(reduzir(px, x0, y0, m, xmin, ymin, tw, th, escala)), th))

    # 3. Posição na célula de 48: as pernas onde estão na `rotacao`; a altura acompanha a
    #    calibração pela linha do chão.
    pernas_ref = centro_pernas(ref, chao)
    sentado = sentado_de_dormindo(direcao) if animacao != "dormindo" else None
    quadros: list[dict] = [{} for _ in range(n)]
    for i in sorted(range(n), key=lambda i: (i != k, i)):
        grade, th = grades[i]
        no_chao = i in NO_CHAO[animacao]
        dy = 0 if no_chao else round((celulas[i][6] - y1max) / escala)
        if no_chao:  # a limpeza pode tirar a linha de baixo: mede pelo desenho
            th = max(ty for _, ty in grade) + 1
        oy = chao - th + dy
        ox = round(pernas_ref - centro_pernas(grade, th))
        q = {(ox + tx, oy + ty): c for (tx, ty), c in grade.items()}
        # Os sentados ficam sobre o primeiro sentado (o de `dormindo`): o deslocamento que mais
        # repete as linhas de baixo dele.
        if i in SENTADOS[animacao]:
            if sentado is None and animacao == "dormindo":
                sentado = q
            elif sentado is not None:
                alvo = {p: c for p, c in sentado.items() if p[1] >= chao - BASE}
                s = max(
                    range(-AJUSTE, AJUSTE + 1),
                    key=lambda s: (sum(alvo.get((x + s, y)) == c for (x, y), c in q.items()), -abs(s)),
                )
                q = {(x + s, y): c for (x, y), c in q.items()}
        fora = [p for p in q if not (0 <= p[0] < N and 0 <= p[1] < N)]
        if fora:
            raise SystemExit(f"{arq.name}: quadro {i + 1} sai da célula de {N} px ({len(fora)} pixels)")
        quadros[i] = q
        xs, ys = [x for x, _ in q], [y for _, y in q]
        print(f"{arq.name} quadro {i + 1}: caixa ({min(xs)}, {min(ys)}, {max(xs) + 1}, {max(ys) + 1})")

    tira = Image.new("RGBA", (N * n, N), (0, 0, 0, 0))
    for i, q in enumerate(quadros):
        for (x, y), c in q.items():
            tira.putpixel((i * N + x, y), c + (255,))
    SAIDA.mkdir(parents=True, exist_ok=True)
    tira.save(SAIDA / f"{animacao}-{direcao}.png")

    # Prévia: a pose parada e os quadros, 6× sobre o asfalto da cena.
    p = Image.new("RGBA", (N * (n + 1), N), (0x22, 0x26, 0x33, 255))
    p.alpha_composite(ref_im, (0, 0))
    p.alpha_composite(tira, (N, 0))
    prev = AQUI / "preview"
    prev.mkdir(exist_ok=True)
    p.resize((p.width * 6, p.height * 6), Image.NEAREST).save(prev / f"gemini-{animacao}-{direcao}-6x.png")
    print(f"escala {escala:.2f}; saida/gemini/{animacao}-{direcao}.png")


def main() -> None:
    arquivos = [pathlib.Path(a) for a in sys.argv[1:]] or sorted(FONTE.glob("*.png"))
    if not arquivos:
        raise SystemExit("nenhuma folha em fonte/gemini/")
    # `dormindo` primeiro: os sentados das outras animações se apoiam nele.
    for arq in sorted(arquivos, key=lambda a: not a.stem.startswith("dormindo-")):
        processar(arq)


if __name__ == "__main__":
    main()
