"""Recorte de folhas geradas por IA (Gemini, ChatGPT), usado pelos build.py de enfeites/ e cartas/.

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


def recortar_grade(fonte: pathlib.Path, colunas: int, linhas: int, quantidade: int, lado: int, cores, na_base=True,
                   altura_do_primeiro=None, em_leitura=False):
    """Devolve `quantidade` imagens lado×lado, lidas da esquerda para a direita e de cima para baixo.

    Cada objeto fica centrado na horizontal e, se `na_base`, apoiado na base; senão, centrado.
    Com `altura_do_primeiro`, a escala é a que deixa o primeiro objeto com essa altura em px (os quadros
    de uma tira de animação seguem a escala do objeto na folha-base); sem ela, o maior ocupa o lado.
    Com `em_leitura`, a grade é ignorada: os objetos são lidos em ordem de leitura (linhas pela altura, e
    da esquerda para a direita), porque o gerador nem sempre respeita a quantidade de colunas pedida.
    """
    tabela = Image.new("P", (1, 1))
    # O resto da tabela repete a primeira cor: com zeros, um preto puro da fonte cairia num índice
    # fora de `cores`.
    tabela.putpalette([c for cor in cores + [cores[0]] * (256 - len(cores)) for c in cor])
    rgb = Image.open(fonte).convert("RGB")
    im = rgb.quantize(palette=tabela, dither=Image.Dither.NONE)
    im = im.point(lambda i: i if i < len(cores) else 0)
    fundo = cores.index(MAGENTA)
    px, fonte_px = im.load(), rgb.load()
    largura, altura = im.size

    # O Gemini mistura a borda dos objetos com o magenta. Um rosa claro (vermelho e azul altos, verde
    # baixo) também é fundo: a paleta não tem nenhum, e os violetas têm vermelho bem mais baixo.
    def e_fundo(x, y):
        r, g, b = fonte_px[x, y]
        return px[x, y] == fundo or (r > 180 and b > 180 and g < 90)

    # Manchas conexas. As grandes vão para a célula onde fica o seu centro, e as pequenas (alça, pino,
    # botão soltos) vão para o objeto grande mais próximo. Assim um objeto que passa da borda da
    # célula não é cortado nem invade o vizinho. Manchas mínimas são resto da borda e somem.
    dono = [[-1] * largura for _ in range(altura)]
    manchas = []
    for y_ini in range(altura):
        for x_ini in range(largura):
            if dono[y_ini][x_ini] != -1 or e_fundo(x_ini, y_ini):
                continue
            mancha, pilha = [], [(x_ini, y_ini)]
            dono[y_ini][x_ini] = -2
            while pilha:
                x, y = pilha.pop()
                mancha.append((x, y))
                for vx in (x - 1, x, x + 1):
                    for vy in (y - 1, y, y + 1):
                        if 0 <= vx < largura and 0 <= vy < altura and dono[vy][vx] == -1 and not e_fundo(vx, vy):
                            dono[vy][vx] = -2
                            pilha.append((vx, vy))
            if len(mancha) >= 30:
                xs, ys = [p[0] for p in mancha], [p[1] for p in mancha]
                manchas.append((mancha, (min(xs), min(ys), max(xs) + 1, max(ys) + 1)))

    def juntar(a, b):
        return b if a is None else (min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3]))

    def distancia(a, b):
        dx = max(a[0] - b[2], b[0] - a[2], 0)
        dy = max(a[1] - b[3], b[1] - a[3], 0)
        return dx * dx + dy * dy

    cw, ch = largura / colunas, altura / linhas
    maior = max(len(m) for m, _ in manchas)
    caixas = [None] * quantidade
    pequenas = [(m, c) for m, c in manchas if len(m) < maior / 10]
    grandes = [(m, c) for m, c in manchas if len(m) >= maior / 10]
    if em_leitura:
        # Manchas grandes quase encostadas são o mesmo objeto (o bloco de largada e os tênis).
        limiar = (largura / 60) ** 2
        grupos = []
        for mancha, caixa in grandes:
            perto_de = [g for g in grupos if distancia(caixa, g[1]) < limiar]
            novo = ([mancha], caixa)
            for g in perto_de:
                grupos.remove(g)
                novo = (novo[0] + g[0], juntar(novo[1], g[1]))
            grupos.append(novo)
        if len(grupos) != quantidade:
            sys.exit(f"{fonte.name} tem {len(grupos)} objetos, e não {quantidade}")
        # Uma linha nova começa quando o objeto fica todo abaixo do centro da linha atual.
        grupos.sort(key=lambda g: (g[1][1] + g[1][3]) / 2)
        linhas_lidas = []
        for g in grupos:
            atual = linhas_lidas[-1] if linhas_lidas else None
            if atual and g[1][1] < sum((o[1][1] + o[1][3]) / 2 for o in atual) / len(atual):
                atual.append(g)
            else:
                linhas_lidas.append([g])
        ordenados = [g for linha in linhas_lidas for g in sorted(linha, key=lambda g: g[1][0])]
        for i, (partes, caixa) in enumerate(ordenados):
            caixas[i] = caixa
            for mancha in partes:
                for x, y in mancha:
                    dono[y][x] = i
    else:
        for mancha, caixa in grandes:
            col = min(int((caixa[0] + caixa[2]) / 2 / cw), colunas - 1)
            lin = min(int((caixa[1] + caixa[3]) / 2 / ch), linhas - 1)
            if lin * colunas + col < quantidade:
                caixas[lin * colunas + col] = juntar(caixas[lin * colunas + col], caixa)
                for x, y in mancha:
                    dono[y][x] = lin * colunas + col
    for i, caixa in enumerate(caixas):
        if caixa is None:
            sys.exit(f"célula {i + 1} de {fonte.name} está vazia")

    perto = []
    for mancha, caixa in pequenas:
        celula = min(range(quantidade), key=lambda i: distancia(caixa, caixas[i]))
        perto.append((celula, caixa))
        for x, y in mancha:
            dono[y][x] = celula
    for celula, caixa in perto:
        caixas[celula] = juntar(caixas[celula], caixa)

    # A mesma escala para todos: o maior objeto ocupa o lado inteiro, e os outros mantêm a proporção.
    if altura_do_primeiro:
        bloco = (caixas[0][3] - caixas[0][1]) / altura_do_primeiro
    else:
        bloco = max(max(x1 - x0, y1 - y0) for x0, y0, x1, y1 in caixas) / lado
    saidas = []
    for celula, (x0, y0, x1, y1) in enumerate(caixas):
        w = min(lado, max(1, round((x1 - x0) / bloco)))
        h = min(lado, max(1, round((y1 - y0) / bloco)))
        sx, sy = (x1 - x0) / w, (y1 - y0) / h
        out = Image.new("RGBA", (lado, lado))
        ox, oy = (lado - w) // 2, (lado - h if na_base else (lado - h) // 2)
        if altura_do_primeiro and celula > 0:
            # Quadros de uma tira: a posição dentro da célula é a do quadro 1, e não o centro da caixa de
            # cada um. Assim um rabo que balança não arrasta o corpo para o lado.
            cx, cy = celula % colunas * cw, celula // colunas * ch
            px0, py1 = caixas[0][0], caixas[0][3]
            ox = ox_primeiro + round(((x0 - cx) - px0) / bloco)
            oy = oy_primeiro + round(((y1 - cy) - py1) / bloco) - (h - h_primeiro)
        elif celula == 0:
            ox_primeiro, oy_primeiro, h_primeiro = ox, oy, h
        for ty in range(h):
            ya = round(y0 + ty * sy)
            yb = max(round(y0 + (ty + 1) * sy), ya + 1)
            for tx in range(w):
                xa = round(x0 + tx * sx)
                xb = max(round(x0 + (tx + 1) * sx), xa + 1)
                votos = Counter(px[x, y] if dono[y][x] == celula else fundo for y in range(ya, yb) for x in range(xa, xb))
                indice = votos.most_common(1)[0][0]
                if indice != fundo and 0 <= ox + tx < lado and 0 <= oy + ty < lado:
                    out.putpixel((ox + tx, oy + ty), (*cores[indice], 255))
        saidas.append(out)
    return saidas
