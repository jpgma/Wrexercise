"""Rotações do personagem base masculino em 8 direções, a partir de fonte/base.png.

fonte/base.png é uma imagem gerada (1536×1024) com as 8 direções em 2 linhas de 4, sobre um
fundo escuro com brilho, desenhada numa escala bem maior que a do Jogo. Este script:
- separa cada personagem do fundo (o brilho em volta é marrom e escuro; o contorno é quase preto);
- reduz todos com a mesma escala para 33 px de altura (o tamanho do `jpgma`), por moda de cor;
- redesenha o contorno de 1 px e põe os pés na linha 45 de um quadro de 64×64, centrado.

Uso: python build.py   (exige Pillow)
Gera saida/rotations/<direcao>.png (64×64, referência para o PixelLab), saida/rotations.png
(faixa 512×64), saida/rotacao-48.png (faixa de células 48×48, como a folha) e preview/.
"""

from __future__ import annotations

import pathlib
from collections import Counter, deque

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
FONTE = AQUI / "fonte" / "base.png"

# Ordem das células na imagem (2 linhas de 4) = ordem do PixelLab.
DIRECOES = ["south", "south-east", "east", "north-east", "north", "north-west", "west", "south-west"]

QUADRO = 64  # quadro do PixelLab
ALTURA = 33  # altura do personagem parado, com o contorno (igual ao jpgma)
PE_Y = 45  # linha dos pés no quadro de 64
CENTRO_X = 31.5  # centro horizontal do quadro
OX, OY, CELULA = 8, 1, 48  # recorte do quadro de 64 para a célula da folha (igual ao jpgma)

ESCURO = 22  # max(r,g,b) abaixo disso é contorno (ou o fundo preto dos cantos)
CONTORNO = (14, 7, 27)  # #0e071b, o contorno do jpgma
# Cores de cada região, tiradas dos grupos de cor de fonte/base.png (que é quase chapada).
# Cada pixel da fonte vai para a mais próxima; as cores de transição da fonte somem.
PALETA = {
    "cabelo_sombra": "#231514",
    "cabelo": "#33201f",
    "pele": "#ca8c65",
    "pele_sombra": "#896354",
    "camiseta": "#9e9599",
    "camiseta_gola": "#3b3236",  # gola e barra das mangas
    "calca": "#3e3e48",
    "calca_sombra": "#2a2830",
    "branco": "#d3cfd1",  # tênis e branco dos olhos
}
LIMIAR_LINHA = 0.55  # fração de escuro nos votos do bloco para virar linha interna
PUREZA = 3  # distância (px da fonte) dos vizinhos que precisam ter a mesma cor para votar


def hex_rgb(h: str) -> tuple[int, int, int]:
    return int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)


def escuro(p) -> bool:
    return max(p[:3]) < ESCURO


def brilho(p) -> bool:
    """Cor mediana do brilho em volta do personagem: bem escura (as partes escuras do personagem,
    cabelo e calça, ficam acima de 50)."""
    return max(p[:3]) < 40


def componentes(px, x0, y0, w, h, pred):
    """Componentes 4-conectados de pixels que satisfazem pred, dentro da célula."""
    rot = [-1] * (w * h)
    comps = []
    for sy in range(h):
        for sx in range(w):
            i = sy * w + sx
            if rot[i] >= 0 or not pred(px[x0 + sx, y0 + sy]):
                continue
            k = len(comps)
            rot[i] = k
            q = deque([(sx, sy)])
            pts = []
            while q:
                x, y = q.popleft()
                pts.append((x, y))
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < w and 0 <= ny < h:
                        j = ny * w + nx
                        if rot[j] < 0 and pred(px[x0 + nx, y0 + ny]):
                            rot[j] = k
                            q.append((nx, ny))
            comps.append(pts)
    return comps


def mascara(px, x0, y0, w, h) -> set[tuple[int, int]]:
    """Pixels do personagem na célula: as regiões claras de dentro e o contorno escuro em volta."""
    dentro = set()
    for pts in componentes(px, x0, y0, w, h, lambda p: not escuro(p)):
        toca_borda = any(x in (0, w - 1) or y in (0, h - 1) for x, y in pts)
        ordem = sorted(pts, key=lambda t: sum(px[x0 + t[0], y0 + t[1]]))
        mx, my = ordem[len(ordem) // 2]
        if toca_borda or len(pts) < 40 or brilho(px[x0 + mx, y0 + my]):
            continue
        dentro.update(pts)
    # O contorno: pixels escuros a até R_MAX pixels de dentro (o contorno da fonte tem ~12 px).
    r_max = 14
    dist = {p: 0 for p in dentro}
    q = deque(dentro)
    while q:
        x, y = q.popleft()
        d = dist[(x, y)]
        if d == r_max:
            continue
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in dist and escuro(px[x0 + nx, y0 + ny]):
                dist[(nx, ny)] = d + 1
                q.append((nx, ny))
    return set(dist)


VIZ4 = ((1, 0), (-1, 0), (0, 1), (0, -1))
VIZ8 = VIZ4 + ((1, 1), (1, -1), (-1, 1), (-1, -1))


def limpar(grade: dict[tuple[int, int], int]) -> dict[tuple[int, int], int]:
    """Tira as sobras da redução: a sombra da pele longe da pele (vem da mistura do cinza com o
    contorno) e os pixels soltos, sem nenhum vizinho da mesma cor. O branco (olhos) e as linhas
    escuras ficam."""
    nomes = list(PALETA)
    pele, sombra, branco = nomes.index("pele"), nomes.index("pele_sombra"), nomes.index("branco")
    g = dict(grade)
    # A borda da silhueta vira contorno no fim; aqui ela já conta como escura.
    for p in grade:
        if any((p[0] + dx, p[1] + dy) not in grade for dx, dy in VIZ4):
            g[p] = -1
    for _ in range(2):
        novo = dict(g)
        for (x, y), k in g.items():
            if k < 0 or k == branco:
                continue
            viz = [g.get((x + dx, y + dy)) for dx, dy in VIZ8]
            iguais = sum(v == k for v in viz)
            toca_pele = any(g.get((x + dx, y + dy)) == pele for dx, dy in VIZ4)
            if iguais == 0 or (k == sombra and not toca_pele):
                outros = Counter(v for v in viz if v is not None and v >= 0 and v != k)
                if outros:
                    novo[(x, y)] = outros.most_common(1)[0][0]
        g = novo
    return g


def main() -> None:
    img = Image.open(FONTE).convert("RGB")
    W, H = img.size
    px = img.load()
    cw, ch = W // 4, H // 2

    # 1. Máscara e caixa de cada direção.
    recortes = []
    for i, nome in enumerate(DIRECOES):
        x0, y0 = (i % 4) * cw, (i // 4) * ch
        m = mascara(px, x0, y0, cw, ch)
        xs = [x for x, _ in m]
        ys = [y for _, y in m]
        recortes.append((nome, x0, y0, m, min(xs), min(ys), max(xs), max(ys)))
        print(f"{nome:11s} {max(xs) - min(xs) + 1:4d}×{max(ys) - min(ys) + 1:4d} px na fonte")

    # 2. Uma escala só para as 8 direções, pela maior altura.
    alt = max(r[7] - r[5] + 1 for r in recortes)
    escala = alt / ALTURA
    print(f"escala: {escala:.2f} px da fonte por pixel")

    # 3. Paleta fixa por região (PALETA), em vez de reduzir as cores da fonte.
    paleta = [hex_rgb(c) for c in PALETA.values()]
    n_cores = len(paleta)

    cache: dict[tuple, int] = {}

    def idx(p) -> int:
        """Índice na paleta, ou -1 para o contorno."""
        if p not in cache:
            cache[p] = -1 if escuro(p) else min(
                range(n_cores), key=lambda k: sum((a - b) ** 2 for a, b in zip(p, paleta[k]))
            )
        return cache[p]

    quadros = []
    for nome, x0, y0, m, xmin, ymin, xmax, ymax in recortes:
        tw = round((xmax - xmin + 1) / escala)
        th = round((ymax - ymin + 1) / escala)
        # Origem na fonte: centrado na horizontal, a base dos pés na última linha.
        bx = xmin + (xmax - xmin + 1) / 2 - tw * escala / 2
        by = ymax + 1 - th * escala
        grade = {}
        for ty in range(th):
            for tx in range(tw):
                sx0, sx1 = bx + tx * escala, bx + (tx + 1) * escala
                sy0, sy1 = by + ty * escala, by + (ty + 1) * escala
                votos, todos = Counter(), Counter()
                total = dentro = 0
                for sy in range(round(sy0), round(sy1)):
                    for sx in range(round(sx0), round(sx1)):
                        total += 1
                        if (sx, sy) in m:
                            dentro += 1
                            k = idx(px[x0 + sx, y0 + sy])
                            todos[k] += 1
                            # Só vota o pixel "puro", da mesma cor que os vizinhos a PUREZA px:
                            # assim as cores de transição da fonte (antisserrilhado) não entram.
                            if all(
                                idx(px[x0 + sx + dx, y0 + sy + dy]) == k
                                for dx, dy in ((PUREZA, 0), (-PUREZA, 0), (0, PUREZA), (0, -PUREZA))
                            ):
                                votos[k] += 1
                if total and dentro / total >= 0.5:
                    if sum(votos.values()) < 0.15 * dentro:
                        votos = todos  # bloco todo de transição: vale a maioria simples
                    claros = [k for k in votos if k >= 0]
                    # Linha interna (olho, braço, entre as pernas): escuro só quando domina o bloco.
                    if votos[-1] > LIMIAR_LINHA * sum(votos.values()) or not claros:
                        grade[(tx, ty)] = -1
                    else:
                        grade[(tx, ty)] = max(claros, key=lambda k: votos[k])
        quadros.append((nome, tw, th, limpar(grade)))

    # 4. Quadro de 64×64: contorno de 1 px na borda da silhueta, pés na linha PE_Y.
    saida = AQUI / "saida"
    (saida / "rotations").mkdir(parents=True, exist_ok=True)
    prev = AQUI / "preview"
    prev.mkdir(exist_ok=True)
    faixa = Image.new("RGBA", (QUADRO * 8, QUADRO), (0, 0, 0, 0))
    faixa48 = Image.new("RGBA", (CELULA * 8, CELULA), (0, 0, 0, 0))
    for i, (nome, tw, th, grade) in enumerate(quadros):
        q = Image.new("RGBA", (QUADRO, QUADRO), (0, 0, 0, 0))
        ox = round(CENTRO_X - tw / 2 + 0.01)
        oy = PE_Y + 1 - th
        for (tx, ty), k in grade.items():
            borda = any((tx + dx, ty + dy) not in grade for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            cor = CONTORNO if borda or k < 0 else paleta[k]
            q.putpixel((ox + tx, oy + ty), cor + (255,))
        q.save(saida / "rotations" / f"{nome}.png")
        faixa.paste(q, (i * QUADRO, 0))
        faixa48.paste(q.crop((OX, OY, OX + CELULA, OY + CELULA)), (i * CELULA, 0))
        print(f"{nome:11s} {tw}×{th} px, caixa no quadro {q.getbbox()}")
    faixa.save(saida / "rotations.png")
    faixa48.save(saida / "rotacao-48.png")

    # Prévia: 6× sobre o asfalto da cena, com o jpgma embaixo para comparar o tamanho.
    linhas = [faixa48]
    jp = AQUI.parent / "jpgma" / "saida" / "jpgma.png"
    if jp.exists():
        linhas.append(Image.open(jp).convert("RGBA").crop((0, 0, CELULA * 8, CELULA)))
    p = Image.new("RGBA", (CELULA * 8, CELULA * len(linhas)), (0x22, 0x26, 0x33, 255))
    for k, linha in enumerate(linhas):
        p.alpha_composite(linha, (0, k * CELULA))
    p.resize((p.width * 6, p.height * 6), Image.NEAREST).save(prev / "rotacao-6x.png")
    usadas = {c for c in faixa.get_flattened_data() if c[3]}
    print(f"{len(usadas)} cores; saida/rotations.png, saida/rotacao-48.png, preview/rotacao-6x.png")


if __name__ == "__main__":
    main()
