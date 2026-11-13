"""Quadros de animação do base masculino a partir das folhas geradas no Gemini.

Cada folha fica em fonte/gemini/<animacao>-<direcao>.png (por exemplo treinando-south.png):
uma grade de quadros sobre magenta, gerada com os prompts de prompts-gemini.md §4. A grade sai
da quantidade de quadros da animação (QUADROS e GRADES). Este script, para cada folha:
- separa cada quadro do magenta e descarta as manchas soltas;
- reduz todos com a mesma escala, a do 1º quadro, que é a pose parada: a altura dele vira a
  altura do personagem em fonte/<direcao>.png;
- calibra PALETA (a mesma do rotacoes.py) pelos tons da folha e leva cada pixel para a cor dela;
- tira as manchas de cor de transição, separa a gola da sombra da calça e redesenha o
  contorno de 1 px;
- põe a linha do chão do 1º quadro na linha 45 de um quadro de 64×64, centrado; os outros
  quadros ficam com as pernas no mesmo lugar e a mesma altura do chão em relação ao 1º
  (pulos e inclinações do tronco continuam).

Uso: python gemini.py [arquivo ...]   (sem argumentos: todas as folhas de fonte/gemini/)
Gera saida/gemini/<animacao>-<direcao>.png (os quadros lado a lado, 64×64 cada, para importar
no Aseprite) e preview/gemini-<animacao>-<direcao>-6x.png (a pose parada e os quadros).
"""

from __future__ import annotations

import pathlib
import sys
from collections import Counter, deque

from PIL import Image

from rotacoes import CELULA, CENTRO_X, CONTORNO, OX, OY, PALETA, PE_Y, QUADRO, hex_rgb

AQUI = pathlib.Path(__file__).resolve().parent
FONTE = AQUI / "fonte" / "gemini"
SAIDA = AQUI / "saida" / "gemini"

# Quantidade de quadros de cada animação (prompts-pixellab.md §4).
QUADROS = {
    "parado": 4, "andar": 8, "correr": 8, "treinando": 6, "alongar": 4, "olhar": 4,
    "arremessar": 6, "receber": 6, "aceitar": 4, "recusar": 3, "provocar": 4, "comemorar": 8,
    "dormindo": 4, "adormecer": 8, "acordar": 6, "levantar": 8,
}
# Quadros que ficam no chão do 1º, mesmo que o Gemini os desenhe 1 ou 2 px acima: as poses
# sentadas de dormir (prompts-gemini.md §4, "Dormir"). O 3º de `acordar` é o susto, no ar.
NO_CHAO = {"dormindo": range(4), "adormecer": range(8), "acordar": (0, 1, 3, 4, 5), "levantar": range(8)}
# Quadro de pé, cópia da referência, de onde saem a escala e o chão, quando não é o 1º.
CALIBRACAO = {"levantar": 7}
# Colunas e linhas da grade por quantidade de quadros (prompts-gemini.md §4).
GRADES = {3: (3, 1), 4: (2, 2), 6: (3, 2), 8: (4, 2)}

MANCHA = 0.5  # componente com menos que esta fração de um bloco é mancha solta
MARGEM = 0.2  # fração do bloco ignorada em cada borda ao votar a cor (fica só o miolo)
PERNAS = 8  # linhas de baixo (em pixels do Jogo) usadas para alinhar os quadros pelas pernas
AJUSTE = 3  # deslocamento máximo (px) do ajuste fino pelos tênis
MANCHA_PX = 2  # grupo de até tantos pixels da mesma cor, cercado por outra cor, é mancha
# Cores que podem ficar isoladas (1 px sem vizinho igual): olhos e contorno.
ISOLADAS_OK = {hex_rgb(PALETA["branco"]), CONTORNO}


def magenta(p) -> bool:
    r, g, b = p
    return r > 150 and b > 150 and g < 110


def marrom(p) -> bool:
    """Escuro avermelhado (cabelo e sobrancelhas). A gola e a calça são cinzas: o verde e o azul
    ficam perto do vermelho (no Gemini, vermelho até 1,36× o verde)."""
    r, g, b = p
    return r < 95 and r >= 1.4 * g and b < 0.85 * r


def permitidas(p, paleta) -> list[int]:
    """Índices de PALETA (mais o contorno no fim) que o pixel pode receber: marrom só vira
    cabelo ou contorno, e o resto nunca vira cabelo. Sem isso, num cabelo desenhado mais claro
    a gola calibrada toma o cabelo, e separar_gola o leva para a sombra da calça."""
    cabelo = {i for i, nome in enumerate(PALETA) if nome.startswith("cabelo")}
    if marrom(p):
        return sorted(cabelo | {len(paleta) - 1})
    return [i for i in range(len(paleta)) if i not in cabelo]


def mais_proxima_permitida(p, centros) -> int:
    return min(
        permitidas(p, centros), key=lambda k: sum((a - b) ** 2 for a, b in zip(p, centros[k]))
    )


def calibrar(px, w, h, paleta, passo=4, rodadas=6) -> list[tuple]:
    """A paleta como o Gemini a desenhou: os tons da folha costumam sair um pouco mais claros,
    e aí os escuros (cabelo, gola, calça) caem na cor errada. Parte de cada cor da paleta e a
    leva para a média dos pixels da folha mais próximos dela, algumas vezes (k-médias)."""
    amostra = Counter(
        px[x, y] for y in range(0, h, passo) for x in range(0, w, passo) if not magenta(px[x, y])
    )
    centros = [tuple(map(float, c)) for c in paleta]
    for _ in range(rodadas):
        soma = [[0.0, 0.0, 0.0, 0] for _ in centros]
        for p, n in amostra.items():
            k = mais_proxima_permitida(p, centros)
            s = soma[k]
            s[0] += p[0] * n
            s[1] += p[1] * n
            s[2] += p[2] * n
            s[3] += n
        centros = [tuple(v / s[3] for v in s[:3]) if s[3] else c for c, s in zip(centros, soma)]
    return centros


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


def reduzir(px, x0, y0, m, bx, by, tw, th, escala, cor) -> dict[tuple[int, int], tuple]:
    """Cor de cada bloco pela maioria do miolo; vazio quando o bloco é mais magenta."""
    grade = {}
    borda = escala * MARGEM
    for ty in range(th):
        for tx in range(tw):
            votos, total = Counter(), 0
            for sy in range(round(by + ty * escala + borda), round(by + (ty + 1) * escala - borda)):
                sx0, sx1 = round(bx + tx * escala + borda), round(bx + (tx + 1) * escala - borda)
                for sx in range(sx0, sx1):
                    total += 1
                    if (sx, sy) in m:
                        votos[cor(px[x0 + sx, y0 + sy])] += 1
            if total and sum(votos.values()) >= total / 2:
                grade[(tx, ty)] = votos.most_common(1)[0][0]
    return grade


def limpar(grade: dict[tuple[int, int], tuple]) -> dict[tuple[int, int], tuple]:
    """Troca cada grupo de até MANCHA_PX pixels da mesma cor pela cor que domina em volta dele
    (as manchas da geração), quando essa cor é ao menos 3/4 da volta."""
    g = dict(grade)
    visto: set[tuple[int, int]] = set()
    for inicio, c in grade.items():
        if inicio in visto or c in ISOLADAS_OK:
            continue
        grupo, fila = {inicio}, deque([inicio])
        while fila and len(grupo) <= MANCHA_PX:
            x, y = fila.popleft()
            for v in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if v not in grupo and grade.get(v) == c:
                    grupo.add(v)
                    fila.append(v)
        visto |= grupo
        if len(grupo) > MANCHA_PX:
            continue
        volta = Counter(
            grade.get(v)
            for x, y in grupo
            for v in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))
            if v not in grupo
        )
        cor, n = volta.most_common(1)[0]
        if cor is not None and n * 4 >= 3 * sum(volta.values()):
            for p in grupo:
                g[p] = cor
    return g


def separar_gola(grade: dict[tuple[int, int], tuple]) -> dict[tuple[int, int], tuple]:
    """A gola e a sombra da calça são cinzas escuros quase iguais. Na rotations, a gola só
    aparece dentro da camiseta: fora dela (ao menos 2 vizinhos de camiseta), vira sombra da
    calça."""
    gola, camiseta = hex_rgb(PALETA["camiseta_gola"]), hex_rgb(PALETA["camiseta"])
    sombra = hex_rgb(PALETA["calca_sombra"])
    g = dict(grade)
    for (x, y), c in grade.items():
        viz = [grade.get(v) for v in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))]
        if c == gola and viz.count(camiseta) < 2:
            g[(x, y)] = sombra
    return g


def processar(arq: pathlib.Path) -> None:
    animacao, _, direcao = arq.stem.partition("-")
    if animacao not in QUADROS or not (AQUI / "fonte" / f"{direcao}.png").exists():
        raise SystemExit(f"{arq.name}: o nome deve ser <animacao>-<direcao>.png")
    n = QUADROS[animacao]
    cols, linhas = GRADES[n]

    ref = Image.open(AQUI / "fonte" / f"{direcao}.png").convert("RGBA")
    caixa_ref = ref.getbbox()
    alt_ref = caixa_ref[3] - caixa_ref[1]

    img = Image.open(arq).convert("RGB")
    px = img.load()
    cw, ch = img.width // cols, img.height // linhas

    paleta = [hex_rgb(c) for c in PALETA.values()] + [CONTORNO]
    calibrada = calibrar(px, img.width, img.height, paleta)
    cache: dict[tuple, tuple] = {}

    def cor(p):
        if p not in cache:
            cache[p] = paleta[mais_proxima_permitida(p, calibrada)]
        return cache[p]

    # 1. Caixa de cada quadro; a escala sai do 1º (pose parada), com uma estimativa da área
    #    de um bloco para descartar manchas.
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
    k = CALIBRACAO.get(animacao, 0)
    _, _, _, x1min, y1min, x1max, y1max = celulas[k]
    escala = (y1max - y1min) / alt_ref

    def centro_pernas(m, ymax) -> float:
        """Centro horizontal das pernas (as PERNAS linhas de baixo), que não saem do lugar."""
        xs = [x for x, y in m if y >= ymax - PERNAS * escala]
        return (min(xs) + max(xs) + 1) / 2

    # 2. Cada quadro numa grade de blocos alinhada à borda do próprio personagem. No quadro de
    #    64, as pernas ficam onde estão no 1º (o Gemini não põe o personagem no mesmo lugar de
    #    cada célula) e a altura acompanha o 1º pela linha do chão (pulos continuam).
    tira = Image.new("RGBA", (QUADRO * n, QUADRO), (0, 0, 0, 0))
    largura1 = round((x1max - x1min) / escala)
    ox1 = round(CENTRO_X - largura1 / 2 + 0.01)
    pernas1 = ox1 + (centro_pernas(celulas[k][2], y1max) - x1min) / escala
    for i in sorted(range(n), key=lambda i: i != k):  # o de calibração primeiro
        x0, y0, m, xmin, ymin, xmax, ymax = celulas[i]
        tw = round((xmax - xmin) / escala)
        th = round((ymax - ymin) / escala)
        grade = separar_gola(limpar(reduzir(px, x0, y0, m, xmin, ymin, tw, th, escala, cor)))
        dy = 0 if i in NO_CHAO.get(animacao, ()) else round((ymax - y1max) / escala)
        ox = round(pernas1 - (centro_pernas(m, ymax) - xmin) / escala)
        # Com os pés no chão, o ajuste fino vem dos tênis: o deslocamento (até AJUSTE px) que
        # mais sobrepõe as linhas de baixo às do 1º quadro, com o pé de apoio no mesmo lugar.
        chao = {(ox + tx, th - ty) for tx, ty in grade if ty >= th - 2}
        if i == k:
            chao1 = chao
        elif dy == 0:
            ox += max(
                range(-AJUSTE, AJUSTE + 1),
                key=lambda s: (len({(x + s, y) for x, y in chao} & chao1), -abs(s)),
            )
        oy = PE_Y + 1 - th + dy
        q = Image.new("RGBA", (QUADRO, QUADRO), (0, 0, 0, 0))
        for (tx, ty), c in grade.items():
            viz = ((tx + 1, ty), (tx - 1, ty), (tx, ty + 1), (tx, ty - 1))
            borda = any(v not in grade for v in viz)
            q.putpixel((ox + tx, oy + ty), (CONTORNO if borda else c) + (255,))
        if i in NO_CHAO.get(animacao, ()):
            # A limpeza pode tirar a linha de baixo: desce pelo desenho final.
            q = q.transform(q.size, Image.AFFINE, (1, 0, 0, 0, 1, q.getbbox()[3] - (PE_Y + 1)))
        tira.paste(q, (i * QUADRO, 0))
        print(f"{arq.name} quadro {i + 1}: {tw}×{th} px, caixa no quadro {q.getbbox()}")

    SAIDA.mkdir(parents=True, exist_ok=True)
    tira.save(SAIDA / f"{animacao}-{direcao}.png")

    # Prévia: a pose parada e os quadros, 6× sobre o asfalto da cena.
    p = Image.new("RGBA", (CELULA * (n + 1), CELULA), (0x22, 0x26, 0x33, 255))
    p.alpha_composite(ref.crop((OX, OY, OX + CELULA, OY + CELULA)), (0, 0))
    for i in range(n):
        q = tira.crop((i * QUADRO + OX, OY, i * QUADRO + OX + CELULA, OY + CELULA))
        p.alpha_composite(q, ((i + 1) * CELULA, 0))
    prev = AQUI / "preview"
    prev.mkdir(exist_ok=True)
    p = p.resize((p.width * 6, p.height * 6), Image.NEAREST)
    p.save(prev / f"gemini-{animacao}-{direcao}-6x.png")
    print(f"escala {escala:.2f}; saida/gemini/{animacao}-{direcao}.png")


def main() -> None:
    arquivos = [pathlib.Path(a) for a in sys.argv[1:]] or sorted(FONTE.glob("*.png"))
    if not arquivos:
        raise SystemExit("nenhuma folha em fonte/gemini/")
    for arq in arquivos:
        processar(arq)


if __name__ == "__main__":
    main()
