"""Avatar `jpgma` em 8 direções (docs/decisoes/0039-avatar-em-8-direcoes.md, proposta).

A fonte da verdade é fonte/jpgma.aseprite, editado à mão no Aseprite: um quadro de 48×48 por célula
da folha, na ordem da tabela ANIMACOES (a `rotacao`, depois cada animação × 8 direções, uma tag
`animacao-direcao` por trecho). O build só lê esse arquivo, confere o formato e a paleta e monta a
folha; ele nunca escreve no .aseprite.

Origem do desenho: duas exportações do PixelLab (`rotations`, Walking, Running, Throw_Object, Cheering,
Taking_Punch), com o ruído de cor levado para a paleta e recortadas de 64×64 para 48×48 sem reamostrar.
O `parado` (respiração) foi desenhado sobre a `rotacao`. Desde 2026-11-09, tudo se edita no .aseprite.

Desde 2026-11-10, a folha padrão do aplicativo é o base masculino (pixel-art/base-masculino/). A folha
do `jpgma` fica fora do bundle: saida/jpgma-importar.png é a mesma folha em PNG indexado, no formato de
97 linhas, pronta para importar em /perfil/avatar. As animações que o `jpgma` não tem repetem as linhas
de outra (EMPRESTADAS), como o aplicativo fazia quando ela era a padrão.

Uso: python build.py   (exige Pillow e o pixel-plugin configurado; ver docs/avatar-kit.md §6)
Gera saida/jpgma.png, saida/jpgma.json, saida/jpgma-importar.png e preview/.
"""

from __future__ import annotations

import hashlib
import json
import pathlib
import sys

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parent / "avatar-base"))
from aseprite_mcp import Aseprite  # noqa: E402

FONTE = AQUI / "fonte/jpgma.aseprite"
N = 48  # célula da folha e quadro do .aseprite
MS = 200

DIRECOES = ["sul", "sudeste", "leste", "nordeste", "norte", "noroeste", "oeste", "sudoeste"]

# Animações da folha, na ordem das linhas e dos quadros do .aseprite: (nome, quadros por direção, origem).
# A linha 0 é `rotacao`, com uma direção por coluna; cada animação tem 8 linhas, uma por direção.
# O pixel-mcp não lê tags; por isso a ordem fica aqui e o build confere o total de quadros.
ANIMACOES = [
    ("andar", 8, "Walking"),
    ("correr", 8, "Running"),
    ("arremessar", 7, "Throw_Object"),
    ("comemorar", 9, "Cheering"),
    ("receber", 6, "Taking_Punch"),  # reação ao ser atingido pela carta
    ("parado", 4, "desenho"),  # respiração, desenhada sobre a `rotacao`
]
# Animações do formato (docs/avatar-formato.md §6) que o `jpgma` não tem, depois das de ANIMACOES, na
# ordem das linhas: (nome, animação cujas linhas são copiadas).
EMPRESTADAS = [
    ("treinando", "correr"),
    ("alongar", "parado"),
    ("olhar", "parado"),
    ("aceitar", "parado"),
    ("recusar", "parado"),
    ("provocar", "parado"),
]
TOTAL = len(DIRECOES) * (1 + sum(qt for _, qt, _ in ANIMACOES))

# As 16 cores do PixelLab e os dois vermelhos da boca.
PALETA = [
    "#0e071b", "#150b0d", "#1c1215", "#1f1a21", "#282128", "#291a1b", "#2f2930", "#322c33",
    "#363037", "#392627", "#493334", "#8a523e", "#c26c4d", "#dcd1d9", "#e0855a", "#f7a972",
    "#e74647", "#702623",
]


def rgb(c: str) -> tuple[int, int, int]:
    return int(c[1:3], 16), int(c[3:5], 16), int(c[5:7], 16)


def ler_quadros() -> list[dict]:
    """Os pixels de cada quadro do .aseprite: [{(x, y): '#rrggbb'}]. Recusa alfa parcial e cor fora da paleta."""
    out = []
    with Aseprite() as a:
        info = a("get_sprite_info", sprite_path=str(FONTE))
        if info["frame_count"] != TOTAL or (info["width"], info["height"]) != (N, N) or info["layer_count"] != 1:
            sys.exit(f"{FONTE.name}: esperado {TOTAL} quadros {N}×{N} numa camada só; veio {info}")
        camada = info["layers"][0]
        for i in range(1, TOTAL + 1):
            try:
                r = a("get_pixels", sprite_path=str(FONTE), layer_name=camada, frame_number=i,
                      x=0, y=0, width=N, height=N, page_size=N * N)
            except RuntimeError as e:
                if "No cel found" not in str(e):
                    raise
                r = {"pixels": []}
            q = {}
            for p in r["pixels"]:
                c = p["color"].lower()
                alfa = c[7:9] if len(c) == 9 else "ff"
                if alfa == "00":
                    continue
                onde = f"quadro {i}, pixel ({p['x']}, {p['y']})"
                if alfa != "ff":
                    sys.exit(f"{onde}: semitransparente")
                if c[:7] not in PALETA:
                    sys.exit(f"{onde}: cor {c[:7]} fora da paleta")
                q[(p["x"], p["y"])] = c[:7]
            out.append(q)
            print(f"{FONTE.name}: quadro {i}/{TOTAL}", end="\r", flush=True)
        print()
    return out


def montar(quadros: list[dict]) -> tuple[list[list[dict]], dict]:
    """Linhas da folha e o JSON da folha."""
    rotacoes = quadros[:len(DIRECOES)]
    linhas = [rotacoes]
    meta = {
        "celula": N,
        # Ponto entre os pés: centro da célula, na linha mais baixa da rotação de frente.
        "ancora": {"x": N // 2, "y": max(y for _, y in rotacoes[0])},
        "direcoes": DIRECOES,
        "animacoes": {"rotacao": {"linha": 0, "quadros": 1, "ms": None, "origem": "rotations"}},
    }
    i = len(DIRECOES)
    for nome, qt, origem in ANIMACOES:
        meta["animacoes"][nome] = {"linha": len(linhas), "quadros": qt, "ms": MS, "origem": origem}
        for _ in DIRECOES:
            linhas.append(quadros[i:i + qt])
            i += qt
    for nome, origem in EMPRESTADAS:
        info = meta["animacoes"][origem]
        meta["animacoes"][nome] = {**info, "linha": len(linhas), "origem": origem}
        linhas.extend(linhas[info["linha"]:info["linha"] + len(DIRECOES)])
    return linhas, meta


def imagem(q: dict) -> Image.Image:
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    for (x, y), c in q.items():
        im.putpixel((x, y), (*rgb(c), 255))
    return im


def escrever_folha(linhas: list[list[dict]]) -> Image.Image:
    folha = Image.new("RGBA", (max(len(linha) for linha in linhas) * N, len(linhas) * N), (0, 0, 0, 0))
    for j, linha in enumerate(linhas):
        for i, q in enumerate(linha):
            folha.paste(imagem(q), (i * N, j * N))
    return folha


def escrever_previews(folha: Image.Image, linhas: list[list[dict]], meta: dict):
    prev = AQUI / "preview"
    prev.mkdir(exist_ok=True)
    fundo = (0x2B, 0x30, 0x42, 255)
    folha.resize((folha.width * 4, folha.height * 4), Image.NEAREST).save(prev / "jpgma-4x.png")
    rot = Image.new("RGBA", (8 * N, N), fundo)
    for k, q in enumerate(linhas[0]):
        rot.alpha_composite(imagem(q), (k * N, 0))
    rot.resize((rot.width * 3, rot.height * 3), Image.NEAREST).save(prev / "rotacao.png")
    # O `retrato` do `Avatar`: recorte de 24×24 em (12, 10) da `rotacao` `sul` (formatoFolha.retrato), em 8×.
    folha.crop((12, 10, 36, 34)).resize((24 * 8, 24 * 8), Image.NEAREST).save(prev / "retrato.png")
    # Um GIF por animação, com as 8 direções lado a lado, em 3×.
    for nome, info in meta["animacoes"].items():
        if nome == "rotacao":
            continue
        quadros = []
        for i in range(info["quadros"]):
            tela = Image.new("RGBA", (8 * N, N), fundo)
            for k in range(8):
                tela.alpha_composite(imagem(linhas[info["linha"] + k][i]), (k * N, 0))
            quadros.append(tela.resize((tela.width * 3, tela.height * 3), Image.NEAREST))
        quadros[0].save(prev / f"{nome}.gif", save_all=True, append_images=quadros[1:], duration=info["ms"],
                        loop=0, disposal=2)


def escrever_importavel(folha: Image.Image):
    """A folha para importar: o mesmo desenho em PNG indexado (índice 0 transparente), bem menor que o RGBA."""
    indice = {(*rgb(c), 255): i + 1 for i, c in enumerate(PALETA)}
    ind = Image.new("P", folha.size, 0)
    ind.putpalette([0, 0, 0] + [v for c in PALETA for v in rgb(c)])
    ind.putdata([indice[p] if p[3] else 0 for p in folha.getdata()])
    destino_png = AQUI / "saida/jpgma-importar.png"
    ind.save(destino_png, optimize=True, transparency=0)
    if Image.open(destino_png).convert("RGBA").tobytes() != folha.tobytes():
        sys.exit(f"{destino_png}: a versão indexada difere da folha")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    linhas, meta = montar(ler_quadros())

    saida = AQUI / "saida"
    saida.mkdir(exist_ok=True)
    folha = escrever_folha(linhas)
    png = saida / "jpgma.png"
    folha.save(png, optimize=True)
    meta = {"largura": folha.width, "altura": folha.height, **meta, "paleta": PALETA,
            "sha256": hashlib.sha256(folha.tobytes()).hexdigest()}
    (saida / "jpgma.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    escrever_previews(folha, linhas, meta)
    escrever_importavel(folha)
    print(f"{png.relative_to(AQUI).as_posix()}: {folha.width}×{folha.height}, "
          f"{png.stat().st_size / 1024:.1f} KB, {sum(len(linha) for linha in linhas)} quadros")


if __name__ == "__main__":
    main()
