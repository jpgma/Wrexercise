"""Enfeites do modo Jogo (docs/gamificacao.md §9): 19 objetos apoiados no chão da praça, animados.

Gera um atlas de 4 quadros × 19 linhas, em células de 40×40 (escala do mestre da praça, mostrada em 2×),
com cada objeto apoiado na base da célula: saida/enfeites.png, copiado para webapp/public/. A ordem das
linhas é a de ENFEITES. O quadro 1 também é o ícone da vitrine.

Fontes (geradas no ChatGPT, ver prompts-chatgpt.md):
- fonte/base.png: os 19 objetos parados, pedidos numa grade de 5×4 e lidos em ordem de leitura (o ChatGPT
  nem sempre respeita as colunas). Uma escala só para a folha toda, então os
  objetos mantêm as proporções reais entre si (a árvore e o poste do tamanho de um avatar).
- fonte/tira-NN-<conquista>.png: os 4 quadros de um objeto, numa grade de 2×2, reduzidos com a escala do
  objeto na base. Sem a tira, o quadro da base se repete e o objeto fica parado.
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
    ("treinos10", 160),  # kettlebell de bronze
    ("treinos50", 160),  # kettlebell de prata
    ("treinos100", 160),  # kettlebell de ouro
    ("desafiocompleto", 180),  # troféu no pedestal
    ("ratodeacademia", 180),  # suporte de halteres
    ("corredor", 220),  # bloco de largada
    ("andarilho", 200),  # cachorro
    ("ciclista", 160),  # bicicleta
    ("peixe", 260),  # piscininha com boia
    ("boleiro", 240),  # golzinho
    ("zen", 360),  # tapete de ioga
    ("pesodocorpo", 180),  # paralelas
    ("madrugador", 240),  # galo
    ("noturno", 140),  # poste: quadro 1 apagado, 2 a 4 aceso
    ("fimdesemana", 150),  # caixa de som
    ("ecletico", 200),  # bolsa de academia
]

# Linhas de baixo (px da célula) que não se mexem: em todos os quadros, são as do quadro 1. O ChatGPT às
# vezes redesenha a parte parada com 1 px de diferença, e o vaso tremeria junto com a planta.
PARADO_EMBAIXO = {
    "sequencia8": 10,  # o vaso
    "sequencia12": 13,  # o vaso e o tronco
}

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
        bases = recortar_grade(BASE, COLUNAS, LINHAS, len(ENFEITES), LADO, cores, em_leitura=True)
        print("fonte:", BASE.name)
    elif ANTIGA.exists():
        bases = [na_celula(i) for i in recortar_grade(ANTIGA, COLUNAS, LINHAS, len(ENFEITES), 32, cores)]
        print("fonte:", ANTIGA.name, "(ícones antigos, parados: falta fonte/base.png)")
    else:
        sys.exit("falta fonte/base.png: gere a folha-base com prompts-chatgpt.md")

    atlas = Image.new("RGBA", (LADO * QUADROS, LADO * len(ENFEITES)))
    linhas_ts = []
    for linha, ((conquista, ms), base) in enumerate(zip(ENFEITES, bases)):
        tira = AQUI / "fonte" / f"tira-{linha + 1:02d}-{conquista}.png"
        if BASE.exists() and tira.exists():
            altura = base.getbbox()[3] - base.getbbox()[1]
            quadros = recortar_grade(tira, 2, 2, QUADROS, LADO, cores, altura_do_primeiro=altura)
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

    atlas.save(AQUI / "saida" / "enfeites.png", optimize=True)
    shutil.copyfile(AQUI / "saida" / "enfeites.png", PUBLICO)

    fundo = Image.new("RGBA", atlas.size, ASFALTO)
    fundo.alpha_composite(atlas)
    fundo.resize((atlas.width * 4, atlas.height * 4), Image.NEAREST).save(AQUI / "preview" / "folha-4x.png")

    # Os 19 animados lado a lado, numa grade de 5×4, sobre o asfalto.
    gif = []
    for coluna in range(QUADROS):
        quadro = Image.new("RGBA", (LADO * COLUNAS, LADO * LINHAS), ASFALTO)
        for i in range(len(ENFEITES)):
            celula = atlas.crop((coluna * LADO, i * LADO, (coluna + 1) * LADO, (i + 1) * LADO))
            quadro.alpha_composite(celula, (i % COLUNAS * LADO, i // COLUNAS * LADO))
        gif.append(quadro.resize((quadro.width * 4, quadro.height * 4), Image.NEAREST).convert("RGB"))
    gif[0].save(AQUI / "preview" / "animado.gif", save_all=True, append_images=gif[1:], duration=200, loop=0)

    print("\n// Cole em enfeitesNaPraca.ts (px do mestre 1×, dentro da célula de 40×40):")
    print("\n".join(linhas_ts))
    print(f"\nsaida/enfeites.png {atlas.size}, {len(ENFEITES)} enfeites")


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
