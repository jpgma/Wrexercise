"""Recorte de folhas geradas por IA (Gemini), usado pelos build.py de enfeites/ e cartas/.

A folha tem objetos numa grade de células iguais, sobre magenta chapado (#FF00FF), desenhados em
pixel art ampliada. Cada objeto é reduzido por moda de cor (não por média), com a mesma escala para
todos, e as cores vão para a paleta dada. O magenta vira transparente.
"""

import pathlib
import sys
from collections import Counter

from PIL import Image

MAGENTA = (255, 0, 255)


def cores_do_script(constantes, paleta_sprite):
    """As cores `#rrggbb` das constantes em caixa alta de um script, as de uma paleta e o magenta."""
    cores = {MAGENTA}
    for nome, valor in constantes.items():
        if nome.isupper() and isinstance(valor, str) and valor.startswith("#") and len(valor) == 7:
            cores.add(tuple(int(valor[i:i + 2], 16) for i in (1, 3, 5)))
    for valor in paleta_sprite:
        cores.add(tuple(valor[:3]))
    return sorted(cores)


def recortar_grade(fonte: pathlib.Path, colunas: int, linhas: int, quantidade: int, lado: int, cores, na_base=True):
    """Devolve `quantidade` imagens lado×lado, lidas da esquerda para a direita e de cima para baixo.

    Cada objeto fica centrado na horizontal e, se `na_base`, apoiado na base; senão, centrado.
    """
    tabela = Image.new("P", (1, 1))
    # O resto da tabela repete a primeira cor: com zeros, um preto puro da fonte cairia num índice
    # fora de `cores`.
    tabela.putpalette([c for cor in cores + [cores[0]] * (256 - len(cores)) for c in cor])
    im = Image.open(fonte).convert("RGB").quantize(palette=tabela, dither=Image.Dither.NONE)
    im = im.point(lambda i: i if i < len(cores) else 0)
    fundo = cores.index(MAGENTA)
    px = im.load()

    cw, ch = im.width / colunas, im.height / linhas
    caixas = []
    for i in range(quantidade):
        x0, y0 = round(i % colunas * cw), round(i // colunas * ch)
        x1, y1 = round(x0 + cw), round(y0 + ch)
        pontos = [(x, y) for y in range(y0, y1) for x in range(x0, x1) if px[x, y] != fundo]
        if not pontos:
            sys.exit(f"célula {i + 1} de {fonte.name} está vazia")
        xs, ys = [p[0] for p in pontos], [p[1] for p in pontos]
        caixas.append((min(xs), min(ys), max(xs) + 1, max(ys) + 1))

    # A mesma escala para todos: o maior objeto ocupa o lado inteiro, e os outros mantêm a proporção.
    bloco = max(max(x1 - x0, y1 - y0) for x0, y0, x1, y1 in caixas) / lado
    saidas = []
    for x0, y0, x1, y1 in caixas:
        w = min(lado, max(1, round((x1 - x0) / bloco)))
        h = min(lado, max(1, round((y1 - y0) / bloco)))
        sx, sy = (x1 - x0) / w, (y1 - y0) / h
        out = Image.new("RGBA", (lado, lado))
        ox, oy = (lado - w) // 2, (lado - h if na_base else (lado - h) // 2)
        for ty in range(h):
            ya = round(y0 + ty * sy)
            yb = max(round(y0 + (ty + 1) * sy), ya + 1)
            for tx in range(w):
                xa = round(x0 + tx * sx)
                xb = max(round(x0 + (tx + 1) * sx), xa + 1)
                votos = Counter(px[x, y] for y in range(ya, yb) for x in range(xa, xb))
                indice = votos.most_common(1)[0][0]
                if indice != fundo:
                    out.putpixel((ox + tx, oy + ty), (*cores[indice], 255))
        saidas.append(out)
    return saidas
