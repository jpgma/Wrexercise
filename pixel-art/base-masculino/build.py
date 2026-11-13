"""Folha de avatar padrão do Jogo: o base masculino em 8 direções (docs/avatar-formato.md §6, ADR 0039).

A fonte da verdade é saida/base-masculino.aseprite, montado por montar.py e editado à mão no
Aseprite: quadros de 64×64, uma tag por animação e direção (por exemplo, andar-north-west) e a tag
`rotacao` com um quadro por direção. O build só lê esse arquivo e nunca escreve nele.

Cada quadro é recortado de 64×64 para a célula de 48×48 em (8, 1), sem reamostrar, como no `jpgma`:
os pés ficam na linha 44 da célula. O build recusa um quadro com pixels fora do recorte, com alfa
parcial ou com quantidade de quadros diferente da tabela ANIMACOES.

Uso: python build.py   (exige Pillow e o pixel-plugin configurado; ver docs/avatar-kit.md §6)
Gera saida/base-masculino.png, saida/base-masculino.json, preview/ e a folha padrão do frontend,
Wrexercise/webapp/public/avatar-base-masculino.png (PNG indexado).
"""

from __future__ import annotations

import hashlib
import json
import pathlib
import sys
import tempfile

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parent / "avatar-base"))
from aseprite_mcp import Aseprite  # noqa: E402

FONTE = AQUI / "saida" / "base-masculino.aseprite"
QUADRO = 64  # quadro do .aseprite
N = 48  # célula da folha
OX, OY = 8, 1  # recorte do quadro de 64 para a célula: pés na linha 44

# Direções da folha (pt-BR) e da tag do .aseprite (PixelLab), na mesma ordem.
DIRECOES = ["sul", "sudeste", "leste", "nordeste", "norte", "noroeste", "oeste", "sudoeste"]
TAGS = ["south", "south-east", "east", "north-east", "north", "north-west", "west", "south-west"]

# Animações da folha, na ordem das linhas (docs/avatar-formato.md §6): (nome, quadros, ms por quadro).
# A linha 0 é `rotacao`; cada animação tem 8 linhas, uma por direção. As 6 primeiras mantêm as linhas
# da primeira versão da folha; as 6 seguintes vêm depois, e as 4 de dormir, no fim (a folha de 97
# linhas, sem elas, continua aceita na importação). Os tempos são os do .aseprite.
ANIMACOES = [
    ("andar", 8, 100),
    ("correr", 8, 100),
    ("arremessar", 6, 80),
    ("comemorar", 9, 120),
    ("receber", 6, 100),
    ("parado", 4, 200),
    ("treinando", 6, 90),
    ("alongar", 4, 150),
    ("olhar", 4, 200),
    ("aceitar", 4, 120),
    ("recusar", 3, 150),
    ("provocar", 4, 120),
    ("dormindo", 4, 350),
    ("adormecer", 8, 140),
    ("acordar", 5, 110),
    ("levantar", 8, 120),
]
COLUNAS = max(qt for _, qt, _ in ANIMACOES)  # 9
LINHAS = 1 + 8 * len(ANIMACOES)  # 129


def ler_tags() -> dict[str, list[Image.Image]]:
    """Os quadros de 64×64 de cada tag do .aseprite, com a duração conferida."""
    with Aseprite() as a, tempfile.TemporaryDirectory() as t:
        png = pathlib.Path(t) / "folha.png"
        a("export_spritesheet", sprite_path=str(FONTE), output_path=str(png), layout="horizontal", padding=0,
          include_json=True)
        meta = json.loads(png.with_suffix(".json").read_text(encoding="utf-8"))
        tira = Image.open(png).convert("RGBA")
    quadros = meta["frames"] if isinstance(meta["frames"], list) else list(meta["frames"].values())
    duracoes = [q["duration"] for q in quadros]
    imagens = [tira.crop((i * QUADRO, 0, (i + 1) * QUADRO, QUADRO)) for i in range(tira.width // QUADRO)]
    tags = {}
    for tag in meta["meta"]["frameTags"]:
        tags[tag["name"]] = imagens[tag["from"]:tag["to"] + 1]
        tags[tag["name"] + ":ms"] = sorted(set(duracoes[tag["from"]:tag["to"] + 1]))
    return tags


def celula(q: Image.Image, onde: str) -> Image.Image:
    """Recorta o quadro de 64 para a célula de 48, sem perder pixel nem aceitar alfa parcial."""
    caixa = q.getbbox()
    if caixa and caixa[3] == OY + N + 1 and caixa[1] > OY:
        # O `comemorar` leste e oeste afunda e passa 1 px do chão da célula em 3 quadros: o quadro
        # sobe 1 px, rente aos vizinhos (que já tocam a linha 47 da célula). Mais que isso é erro.
        print(f"aviso: {onde} sobe 1 px para caber na célula")
        q = q.crop((0, 1, QUADRO, QUADRO + 1))
        caixa = q.getbbox()
    if caixa and (caixa[0] < OX or caixa[1] < OY or caixa[2] > OX + N or caixa[3] > OY + N):
        sys.exit(f"{onde}: desenho em {caixa}, fora do recorte de {N}×{N} em ({OX}, {OY})")
    if any(0 < p[3] < 255 for p in q.get_flattened_data()):
        sys.exit(f"{onde}: pixel semitransparente")
    return q.crop((OX, OY, OX + N, OY + N))


def montar(tags: dict) -> tuple[list[list[Image.Image]], dict]:
    """Linhas da folha (células de 48×48) e o JSON da folha."""
    if len(tags.get("rotacao", [])) != 8:
        sys.exit("a tag rotacao precisa de 8 quadros, um por direção")
    linhas = [[celula(q, f"rotacao {d}") for q, d in zip(tags["rotacao"], DIRECOES)]]
    meta = {
        "celula": N,
        "ancora": {"x": N // 2, "y": None},
        "direcoes": DIRECOES,
        "animacoes": {"rotacao": {"linha": 0, "quadros": 1, "ms": None}},
    }
    for nome, qt, ms in ANIMACOES:
        meta["animacoes"][nome] = {"linha": len(linhas), "quadros": qt, "ms": ms}
        for d, tag in zip(DIRECOES, TAGS):
            chave = f"{nome}-{tag}"
            quadros = tags.get(chave)
            if quadros is None or len(quadros) != qt:
                sys.exit(f"tag {chave}: esperado {qt} quadros; veio {len(quadros or [])}")
            if tags[chave + ":ms"] != [ms]:
                sys.exit(f"tag {chave}: esperado {ms} ms por quadro; veio {tags[chave + ':ms']}")
            linhas.append([celula(q, f"{chave} quadro {i + 1}") for i, q in enumerate(quadros)])
    # Ponto entre os pés: centro da célula, na linha mais baixa da rotação de frente.
    meta["ancora"]["y"] = linhas[0][0].getbbox()[3] - 1
    return linhas, meta


def escrever_folha(linhas: list[list[Image.Image]]) -> Image.Image:
    folha = Image.new("RGBA", (COLUNAS * N, LINHAS * N), (0, 0, 0, 0))
    for j, linha in enumerate(linhas):
        for i, q in enumerate(linha):
            folha.paste(q, (i * N, j * N))
    return folha


def escrever_previews(folha: Image.Image, linhas: list[list[Image.Image]], meta: dict):
    prev = AQUI / "preview"
    prev.mkdir(exist_ok=True)
    fundo = (0x2B, 0x30, 0x42, 255)
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
                tela.alpha_composite(linhas[info["linha"] + k][i], (k * N, 0))
            quadros.append(tela.resize((tela.width * 3, tela.height * 3), Image.NEAREST))
        quadros[0].save(prev / f"folha-{nome}.gif", save_all=True, append_images=quadros[1:],
                        duration=info["ms"], loop=0, disposal=2)


def escrever_bundle(folha: Image.Image) -> pathlib.Path:
    """A folha padrão do frontend: o mesmo desenho em PNG indexado (índice 0 transparente)."""
    cores = sorted({p for p in folha.get_flattened_data() if p[3]})
    if len(cores) > 255:
        sys.exit(f"{len(cores)} cores: o PNG indexado aceita até 255 mais o transparente")
    indice = {c: i + 1 for i, c in enumerate(cores)}
    ind = Image.new("P", folha.size, 0)
    ind.putpalette([0, 0, 0] + [v for c in cores for v in c[:3]])
    ind.putdata([indice[p] if p[3] else 0 for p in folha.get_flattened_data()])
    destino = AQUI.parents[1] / "Wrexercise/webapp/public/avatar-base-masculino.png"
    ind.save(destino, optimize=True, transparency=0)
    if Image.open(destino).convert("RGBA").tobytes() != folha.tobytes():
        sys.exit(f"{destino}: a versão indexada difere da folha")
    return destino


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    linhas, meta = montar(ler_tags())
    folha = escrever_folha(linhas)
    saida = AQUI / "saida"
    png = saida / "base-masculino.png"
    folha.save(png, optimize=True)
    meta = {"largura": folha.width, "altura": folha.height, **meta,
            "sha256": hashlib.sha256(folha.tobytes()).hexdigest()}
    (saida / "base-masculino.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2) + "\n",
                                               encoding="utf-8", newline="\n")
    escrever_previews(folha, linhas, meta)
    bundle = escrever_bundle(folha)
    print(f"{png.relative_to(AQUI).as_posix()}: {folha.width}×{folha.height}, {png.stat().st_size / 1024:.1f} KB; "
          f"{bundle.name}: {bundle.stat().st_size / 1024:.1f} KB; âncora {meta['ancora']}")


if __name__ == "__main__":
    main()
