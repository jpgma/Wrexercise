"""Enfeites do modo Jogo (docs/gamificacao.md §9): 22 objetos apoiados no chão da praça, animados.

Gera um atlas de 4 quadros × 24 linhas, em células de 40×40 (escala do mestre da praça, mostrada em 2×),
com cada objeto apoiado na base da célula: saida/enfeites.png, copiado para webapp/public/. A ordem das
linhas é a de ENFEITES e, depois, a de EXTRAS. O quadro 1 também é o ícone da vitrine.

Fontes (geradas no ChatGPT, ver prompts-chatgpt.md):
- fonte/base.png: os 19 objetos parados, pedidos numa grade de 5×4 e lidos em ordem de leitura (o ChatGPT
  nem sempre respeita as colunas). Uma escala só para a folha toda, então os
  objetos mantêm as proporções reais entre si (a árvore e o poste do tamanho de um avatar).
- fonte/base-2.png: os Enfeites de Futebol, Vôlei e Luta (ADR 0082), com o poste de luz à esquerda como
  régua: a escala é a que deixa o poste com a altura dele na base, e o poste é descartado.
- fonte/tira-NN-<conquista>.png: os 4 quadros de um objeto, numa grade de 2×2, reduzidos com a escala do
  objeto na base. Sem a tira, o quadro da base se repete e o objeto fica parado.
- EXTRAS: linhas que não são Enfeites do catálogo, mas outra forma de um deles durante o treino ao vivo
  (o cachorro andando, o tapete se abrindo), cada uma de uma tira de 2×2.
Sem fonte/base.png, os ícones antigos de 32×32 (fonte/folha.png, da folha do Gemini) entram parados no
lugar, para o aplicativo funcionar até a arte nova chegar.

Uso: python build.py              (gera o atlas, os previews e a lista para enfeitesNaPraca.ts)
     python build.py --recortes   (também grava preview/recorte-NN-<conquista>.png, para anexar ao prompt
                                   de cada tira)
"""

import pathlib
import shutil
import sys

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parents[1] / ".claude/skills/pixel-art-studio/scripts"))
sys.path.insert(0, str(AQUI.parent))
from fonte_gerada import cores_do_script, recortar_grade  # noqa: E402
from pixelstudio import Sprite  # noqa: E402

BASE = AQUI / "fonte" / "base.png"
BASE_2 = AQUI / "fonte" / "base-2.png"
ANTIGA = AQUI / "fonte" / "folha.png"
PUBLICO = AQUI.parents[1] / "Wrexercise/webapp/public/enfeites.png"
LADO = 40  # célula do atlas: o maior Enfeite tem a altura de um avatar
QUADROS = 4
COLUNAS, LINHAS = 5, 4  # grade da folha-base, lida da esquerda para a direita e de cima para baixo
ASFALTO = "#222633"

# Cores que o ChatGPT recebe no prompt e que a paleta wrexercise não tem.
PRATA = "#d4d8e4"
AGUA = "#4aa8ff"
BRONZE, BRONZE_ESCURO = "#c98a5a", "#8d5a36"
MARROM, MARROM_ESCURO = "#a8693f", "#6e4125"

# (conquista em minúsculas, ms por quadro), na ordem da folha-base e das linhas do atlas.
ENFEITES = [
    ("sequencia4", 320),  # muda num vaso
    ("sequencia8", 320),  # planta com flor
    ("sequencia12", 320),  # árvore em vaso
    ("treinos10", 260),  # kettlebell de bronze
    ("treinos50", 260),  # kettlebell de prata
    ("treinos100", 260),  # kettlebell de ouro
    ("desafiocompleto", 260),  # troféu no pedestal
    ("ratodeacademia", 260),  # suporte de halteres
    ("corredor", 220),  # bloco de largada
    ("andarilho", 200),  # cachorro
    ("ciclista", 160),  # bicicleta
    ("peixe", 260),  # piscininha com boia
    ("boleiro", 240),  # golzinho
    ("zen", 360),  # tapete de ioga
    ("pesodocorpo", 260),  # paralelas
    ("madrugador", 240),  # galo
    ("noturno", 140),  # poste: quadro 1 apagado, 2 a 4 aceso
    ("fimdesemana", 150),  # caixa de som
    ("ecletico", 200),  # bolsa de academia
    ("craque", 240),  # cones com bola (folha-base 2)
    ("levantador", 260),  # redinha de vôlei (folha-base 2)
    ("faixapreta", 260),  # saco de pancada (folha-base 2)
]
NA_BASE_1 = 19  # os primeiros de ENFEITES vêm de fonte/base.png; os outros, de fonte/base-2.png
REGUA = "noturno"  # o poste, que a folha-base 2 repete à esquerda

# Linhas extras, depois dos Enfeites: (nome no atlas, ms, tira, Enfeite de origem, altura do quadro 1 em
# relação ao Enfeite na base). O cachorro em pé, de lado, é mais baixo que sentado.
EXTRAS = [
    ("andarilho-andando", 140, "tira-cachorro-andando.png", "andarilho", 0.8),
    ("zen-aberto", 160, "tira-zen-aberto.png", "zen", 1.0),
]

# Linhas derivadas por código, depois das extras: (nome no atlas, Enfeite de origem, x da célula onde o
# objeto começa). O quadro 1 do Enfeite sem as colunas à esquerda de x, nos 4 quadros. Os cones sem a
# bola: no Futebol ao vivo, a bola da cena sai dali e passa entre os cones (ADR 0082).
DERIVADOS = [
    ("craque-sem-bola", "craque", 13),
]

# Linhas de baixo (px da célula) que não se mexem: em todos os quadros, são as do quadro 1. O ChatGPT às
# vezes redesenha a parte parada com 1 px de diferença, e o vaso tremeria junto com a planta.
PARADO_EMBAIXO = {
    "sequencia8": 10,  # o vaso
    "sequencia12": 13,  # o vaso e o tronco
    "noturno": 22,  # o poste, abaixo da lanterna
}

# Enfeites cujo único movimento é um brilho que passa: os quadros saem da base, por código, e não de uma
# tira. Num objeto de 13 px, o brilho do ChatGPT some na redução. O valor é a faixa da altura do objeto
# (0 no topo, 1 na base) por onde o brilho passa: no troféu, só a taça.
BRILHO = {
    "treinos10": (0, 1),
    "treinos50": (0, 1),
    "treinos100": (0, 1),
    "desafiocompleto": (0, 0.6),
    "ratodeacademia": (0, 1),
    "pesodocorpo": (0, 1),
}
GIZ, GIZ_SOMBRA = "#f5f1e8", "#d6d0c0"

# A ordem dos quadros da tira (0 a 3), quando o quadro 1 do ChatGPT não combina com os outros: por
# exemplo, [1, 2, 3, 2] para usar os quadros 2, 3, 4, 3.
ORDEM_DA_TIRA: dict[str, list[int]] = {}

(AQUI / "saida").mkdir(exist_ok=True)
(AQUI / "preview").mkdir(exist_ok=True)


def na_celula(imagem):
    """Um ícone menor que a célula, centrado e apoiado na base."""
    celula = Image.new("RGBA", (LADO, LADO))
    celula.alpha_composite(imagem, ((LADO - imagem.width) // 2, LADO - imagem.height))
    return celula


def caixa(quadros):
    """A caixa que cobre o objeto em todos os quadros."""
    caixas = [q.getbbox() for q in quadros if q.getbbox()]
    return (min(c[0] for c in caixas), min(c[1] for c in caixas),
            max(c[2] for c in caixas), max(c[3] for c in caixas))


def recorte_para_prompt(quadro, destino):
    """O objeto ampliado 6× sobre magenta, com margem: a imagem que se anexa ao prompt da tira."""
    x0, y0, x1, y1 = quadro.getbbox()
    margem = 6
    objeto = quadro.crop((x0, y0, x1, y1))
    fundo = Image.new("RGBA", (objeto.width + 2 * margem, objeto.height + 2 * margem), (255, 0, 255, 255))
    fundo.alpha_composite(objeto, (margem, margem))
    fundo.resize((fundo.width * 6, fundo.height * 6), Image.NEAREST).convert("RGB").save(destino)


def main():
    cores = cores_do_script(globals(), Sprite(1, 1, palette="wrexercise").palette)
    if BASE.exists():
        bases = recortar_grade(BASE, COLUNAS, LINHAS, NA_BASE_1, LADO, cores, em_leitura=True)
        print("fonte:", BASE.name)
    elif ANTIGA.exists():
        bases = [na_celula(i) for i in recortar_grade(ANTIGA, COLUNAS, LINHAS, NA_BASE_1, 32, cores)]
        print("fonte:", ANTIGA.name, "(ícones antigos, parados: falta fonte/base.png)")
    else:
        sys.exit("falta fonte/base.png: gere a folha-base com prompts-chatgpt.md")

    if BASE.exists() and BASE_2.exists():
        poste = bases[[c for c, _ in ENFEITES].index(REGUA)].getbbox()
        lidos = recortar_grade(BASE_2, 4, 1, 1 + len(ENFEITES) - NA_BASE_1, LADO, cores,
                               altura_do_primeiro=poste[3] - poste[1], soltos=True)
        bases = bases[:NA_BASE_1] + lidos[1:]
        print("fonte:", BASE_2.name)
    else:
        print("sem", BASE_2.name, ": os Enfeites de Futebol, Vôlei e Luta ficam de fora")
    enfeites = ENFEITES[:len(bases)]
    extras = [e for e in EXTRAS if (AQUI / "fonte" / e[2]).exists() and BASE.exists()]

    derivados = [d for d in DERIVADOS if d[1] in [c for c, _ in enfeites]]

    atlas = Image.new("RGBA", (LADO * QUADROS, LADO * (len(enfeites) + len(extras) + len(derivados))))
    linhas_ts = []
    for linha, ((conquista, ms), base) in enumerate(zip(enfeites, bases)):
        tira = AQUI / "fonte" / f"tira-{linha + 1:02d}-{conquista}.png"
        if BASE.exists() and conquista in BRILHO:
            quadros = _brilhar(base, *BRILHO[conquista])
        elif BASE.exists() and tira.exists():
            altura = base.getbbox()[3] - base.getbbox()[1]
            quadros = recortar_grade(tira, 2, 2, QUADROS, LADO, cores, altura_do_primeiro=altura)
            if conquista in ORDEM_DA_TIRA:
                quadros = [quadros[i] for i in ORDEM_DA_TIRA[conquista]]
            # O quadro 1 da tira fica onde o objeto está na base: a tira só acrescenta o movimento.
            dx = base.getbbox()[0] - quadros[0].getbbox()[0]
            dy = base.getbbox()[3] - quadros[0].getbbox()[3]
            quadros = [_deslocar(q, dx, dy) for q in quadros]
            if conquista in PARADO_EMBAIXO:
                quadros = _fixar_embaixo(quadros, PARADO_EMBAIXO[conquista])
        else:
            quadros = [base] * QUADROS
        for coluna, quadro in enumerate(quadros):
            atlas.alpha_composite(quadro, (coluna * LADO, linha * LADO))
        x0, y0, x1, y1 = caixa(quadros)
        linhas_ts.append(
            f'  {conquista}: {{ linha: {linha}, esquerda: {x0}, largura: {x1 - x0}, '
            f'altura: {y1 - y0}, ms: {ms} }},')
        if "--recortes" in sys.argv:
            recorte_para_prompt(base, AQUI / "preview" / f"recorte-{linha + 1:02d}-{conquista}.png")

    for linha, (nome, ms, arquivo, origem, proporcao) in enumerate(extras, start=len(enfeites)):
        base = bases[[c for c, _ in enfeites].index(origem)]
        altura = round((base.getbbox()[3] - base.getbbox()[1]) * proporcao)
        quadros = recortar_grade(AQUI / "fonte" / arquivo, 2, 2, QUADROS, LADO, cores, altura_do_primeiro=altura)
        if proporcao == 1.0:
            # Começa onde o Enfeite está no chão: o quadro 1 é o objeto da base.
            dx = base.getbbox()[0] - quadros[0].getbbox()[0]
            dy = base.getbbox()[3] - quadros[0].getbbox()[3]
            quadros = [_deslocar(q, dx, dy) for q in quadros]
        for coluna, quadro in enumerate(quadros):
            atlas.alpha_composite(quadro, (coluna * LADO, linha * LADO))
        x0, y0, x1, y1 = caixa(quadros)
        linhas_ts.append(
            f'  "{nome}": {{ linha: {linha}, esquerda: {x0}, largura: {x1 - x0}, '
            f'altura: {y1 - y0}, ms: {ms} }},')

    for linha, (nome, origem, corte) in enumerate(derivados, start=len(enfeites) + len(extras)):
        i = [c for c, _ in enfeites].index(origem)
        quadro = atlas.crop((0, i * LADO, LADO, (i + 1) * LADO))
        quadro.paste((0, 0, 0, 0), (0, 0, corte, LADO))
        for coluna in range(QUADROS):
            atlas.alpha_composite(quadro, (coluna * LADO, linha * LADO))
        x0, y0, x1, y1 = quadro.getbbox()
        linhas_ts.append(
            f'  "{nome}": {{ linha: {linha}, esquerda: {x0}, largura: {x1 - x0}, '
            f'altura: {y1 - y0}, ms: {dict(enfeites)[origem]} }},')

    atlas.save(AQUI / "saida" / "enfeites.png", optimize=True)
    shutil.copyfile(AQUI / "saida" / "enfeites.png", PUBLICO)

    fundo = Image.new("RGBA", atlas.size, ASFALTO)
    fundo.alpha_composite(atlas)
    fundo.resize((atlas.width * 4, atlas.height * 4), Image.NEAREST).save(AQUI / "preview" / "folha-4x.png")

    # Todas as linhas animadas lado a lado, 5 por linha, sobre o asfalto.
    total = len(enfeites) + len(extras) + len(derivados)
    gif = []
    for coluna in range(QUADROS):
        quadro = Image.new("RGBA", (LADO * COLUNAS, LADO * -(-total // COLUNAS)), ASFALTO)
        for i in range(total):
            celula = atlas.crop((coluna * LADO, i * LADO, (coluna + 1) * LADO, (i + 1) * LADO))
            quadro.alpha_composite(celula, (i % COLUNAS * LADO, i // COLUNAS * LADO))
        gif.append(quadro.resize((quadro.width * 4, quadro.height * 4), Image.NEAREST).convert("RGB"))
    gif[0].save(AQUI / "preview" / "animado.gif", save_all=True, append_images=gif[1:], duration=200, loop=0)

    print("\n// Cole em enfeitesNaPraca.ts (px do mestre 1×, dentro da célula de 40×40):")
    print("\n".join(linhas_ts))
    print(f"\nsaida/enfeites.png {atlas.size}, {len(enfeites)} enfeites, {len(extras)} extras e "
          f"{len(derivados)} derivados")


def _brilhar(base, de, ate):
    """Quadro 1 parado; nos quadros 2 a 4, um risco de giz na diagonal (/) passa da esquerda para a
    direita, só sobre o objeto (nunca no contorno nem fora dele), e esmaece no último."""
    x0, y0, x1, y1 = base.getbbox()
    ya, yb = y0 + round((y1 - y0) * de), y0 + round((y1 - y0) * ate)
    px = base.load()

    def no_objeto(x, y):
        r, g, b, a = px[x, y]
        return a and r + g + b > 150  # o contorno é quase preto

    quadros = [base]
    for fracao, cor in ((0.3, GIZ), (0.5, GIZ), (0.7, GIZ_SOMBRA)):
        k = round(x0 + ya + (x1 - x0 + yb - ya) * fracao)
        risco = [(x, k - x) for x in range(x0, x1) if ya <= k - x < yb and no_objeto(x, k - x)]
        meio = len(risco) // 2
        quadro = base.copy()
        for x, y in risco[max(0, meio - 2):meio + 2]:  # no máximo 4 px
            quadro.putpixel((x, y), tuple(int(cor[i:i + 2], 16) for i in (1, 3, 5)) + (255,))
        quadros.append(quadro)
    return quadros


def _fixar_embaixo(quadros, linhas):
    """Troca as `linhas` de baixo de cada quadro pelas do quadro 1."""
    faixa = (0, LADO - linhas, LADO, LADO)
    fixa = quadros[0].crop(faixa)
    saida = [quadros[0]]
    for quadro in quadros[1:]:
        novo = quadro.copy()
        novo.paste(fixa, faixa[:2])
        saida.append(novo)
    return saida


def _deslocar(quadro, dx, dy):
    novo = Image.new("RGBA", quadro.size)
    novo.alpha_composite(quadro.crop((max(0, -dx), max(0, -dy), quadro.width, quadro.height)),
                         (max(0, dx), max(0, dy)))
    return novo


if __name__ == "__main__":
    main()
