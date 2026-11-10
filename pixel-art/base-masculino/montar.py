"""Monta saida/base-masculino.aseprite com todas as animações do base masculino, pelo servidor
MCP do pixel-plugin.

Fontes (quadros de 64×64, pés na mesma linha):
- fonte/base-a.aseprite (PixelLab): rotações, andar (8 direções) e correr (sul e sudeste);
- fonte/base-b.aseprite (PixelLab): comemorar (8 direções);
- saida/gemini/<animacao>-<direcao>.png (gemini.py): as demais, em até 5 direções, mais o
  correr na direção leste.
As direções que faltam saem espelhando a do outro lado: oeste do leste, noroeste do nordeste e
sudoeste do sudeste. Uma direção sem fonte nem espelho fica de fora.

Uso: python montar.py   (exige Pillow e o pixel-plugin configurado)
Gera uma camada e uma tag por animação e direção, por exemplo andar-north-west.
"""

from __future__ import annotations

import json
import pathlib
import sys
import tempfile

from PIL import Image, ImageOps

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parent / "avatar-base"))
from aseprite_mcp import Aseprite  # noqa: E402

SAIDA = AQUI / "saida" / "base-masculino.aseprite"
GEMINI = AQUI / "saida" / "gemini"
QUADRO = 64
CAMADA = "base"

DIRECOES = ["south", "south-east", "east", "north-east", "north", "north-west", "west", "south-west"]
ESPELHO = {"west": "east", "north-west": "north-east", "south-west": "south-east"}

# (animação, fonte, duração do quadro em ms). Fonte: "gemini", ou (arquivo .aseprite, prefixo da
# tag). Durações de docs/avatar-formato.md §2; correr e rotações não estão lá.
ANIMACOES = [
    ("rotacoes", ("base-a", "rotations"), 200),
    ("parado", "gemini", 200),
    ("andar", ("base-a", "Walking"), 100),
    ("correr", ("base-a", "Running"), 100),
    ("treinando", "gemini", 90),
    ("alongar", "gemini", 150),
    ("olhar", "gemini", 200),
    ("arremessar", "gemini", 80),
    ("receber", "gemini", 100),
    ("aceitar", "gemini", 120),
    ("recusar", "gemini", 150),
    ("provocar", "gemini", 120),
    ("comemorar", ("base-b", "Celebrates_a_goa"), 120),
]


def fatiar(folha: Image.Image) -> list[Image.Image]:
    return [folha.crop((i * QUADRO, 0, (i + 1) * QUADRO, QUADRO)) for i in range(folha.width // QUADRO)]


def ler_aseprite(a: Aseprite, nome: str, tmp: pathlib.Path) -> dict[str, list[Image.Image]]:
    """Quadros de fonte/<nome>.aseprite por tag."""
    png = tmp / f"{nome}.png"
    a("export_spritesheet", sprite_path=str(AQUI / "fonte" / f"{nome}.aseprite"), output_path=str(png),
      layout="horizontal", padding=0, include_json=True)
    quadros = fatiar(Image.open(png).convert("RGBA"))
    tags = json.loads(png.with_suffix(".json").read_text(encoding="utf-8"))["meta"]["frameTags"]
    return {t["name"]: quadros[t["from"]:t["to"] + 1] for t in tags}


def direcoes_da_fonte(animacao: str, fonte, aseprites: dict) -> dict[str, list[Image.Image]]:
    out = {}
    if fonte == "gemini" or animacao == "correr":  # o correr leste vem do Gemini
        for d in DIRECOES:
            png = GEMINI / f"{animacao}-{d.replace('-', '')}.png"
            if png.exists():
                out[d] = fatiar(Image.open(png).convert("RGBA"))
    if fonte != "gemini":
        arquivo, prefixo = fonte
        tags = aseprites[arquivo]
        if prefixo in tags:  # rotações: um quadro por direção
            out.update({d: [q] for d, q in zip(DIRECOES, tags[prefixo])})
        else:
            out.update({d: tags[f"{prefixo}-{d}"] for d in DIRECOES if f"{prefixo}-{d}" in tags})
    for d, origem in ESPELHO.items():
        if d not in out and origem in out:
            out[d] = [ImageOps.mirror(q) for q in out[origem]]
    return {d: out[d] for d in DIRECOES if d in out}


def main():
    with Aseprite() as a, tempfile.TemporaryDirectory() as t:
        tmp = pathlib.Path(t)
        aseprites = {nome: ler_aseprite(a, nome, tmp) for nome in ("base-a", "base-b")}
        tags = []  # (nome, duração, quadros)
        for animacao, fonte, ms in ANIMACOES:
            for d, quadros in direcoes_da_fonte(animacao, fonte, aseprites).items():
                tags.append((f"{animacao}-{d}", ms, quadros))

        r = a("create_canvas", width=QUADRO, height=QUADRO, color_mode="rgb")
        SAIDA.parent.mkdir(exist_ok=True)
        s = str(SAIDA)
        a("save_as", sprite_path=r["file_path"], output_path=s)
        padrao = a("get_sprite_info", sprite_path=s)["layers"]
        a("add_layer", sprite_path=s, layer_name=CAMADA)
        for camada in padrao:
            a("delete_layer", sprite_path=s, layer_name=camada)

        total = sum(len(q) for _, _, q in tags)
        i = 0
        for nome, ms, quadros in tags:
            inicio = i + 1
            for q in quadros:
                i += 1
                if i == 1:
                    a("set_frame_duration", sprite_path=s, frame_number=1, duration_ms=ms)
                else:
                    a("add_frame", sprite_path=s, duration_ms=ms)
                png = tmp / "quadro.png"
                q.save(png)
                a("import_image", sprite_path=s, image_path=str(png), layer_name=CAMADA, frame_number=i)
                print(f"quadro {i}/{total}", end="\r", flush=True)
            a("create_tag", sprite_path=s, tag_name=nome, from_frame=inicio, to_frame=i, direction="forward")
        print()
    for nome, ms, quadros in tags:
        print(f"{nome}: {len(quadros)} quadros, {ms} ms")
    print(f"gerado: {SAIDA.relative_to(AQUI)}")


if __name__ == "__main__":
    main()
