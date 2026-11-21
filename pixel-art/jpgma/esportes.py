"""Acrescenta as 12 animações dos esportes ao fim de fonte/jpgma.aseprite, pelo servidor MCP do
pixel-plugin (docs/decisoes/0082-esportes-e-animacoes-por-tipo.md; docs/avatar-formato.md §6).

Fonte: saida/gemini/<animacao>-<direcao>.png, as tiras de 48×48 que o gemini.py reduz das folhas do
ChatGPT (prompts-chatgpt.md), em 5 direções. As outras três saem espelhando a do outro lado (oeste do
leste, noroeste do nordeste e sudoeste do sudeste). A `barra` tem só o sul, que vale para todas as
direções (a barra da praça fica sempre de frente), e a `largada` só o leste (o oeste, o noroeste e o
sudoeste são o leste espelhado). Roda uma vez só: recusa um .aseprite que já tenha esses quadros.

Uso: python esportes.py   (exige Pillow e o pixel-plugin configurado; ver docs/avatar-kit.md §6)
Acrescenta um quadro por célula e uma tag `animacao-direcao` por trecho, na ordem de ESPORTES do
build.py.
"""

from __future__ import annotations

import pathlib
import sys
import tempfile

from PIL import Image, ImageOps

from build import ANIMACOES, DIRECOES, DORMIR, ESPORTES, FONTE, N, Aseprite

AQUI = pathlib.Path(__file__).resolve().parent
GEMINI = AQUI / "saida" / "gemini"

# Nome da direção nos arquivos, na ordem de DIRECOES, e de onde sai a que é espelhada.
ARQUIVO = dict(zip(DIRECOES, ["south", "southeast", "east", "northeast", "north", "northwest", "west", "southwest"]))
ESPELHO = {"oeste": "leste", "noroeste": "nordeste", "sudoeste": "sudeste"}
# Animações desenhadas numa direção só: a direção da folha que vale para cada direção (com espelho
# quando o lado é o oposto).
UMA_DIRECAO = {
    "barra": {d: ("sul", False) for d in DIRECOES},
    "largada": {d: ("leste", d in ("oeste", "noroeste", "sudoeste")) for d in DIRECOES},
}


def quadros(animacao: str, qt: int, direcao: str) -> list[Image.Image]:
    """Os quadros da tag: o meio da tira (o 1º e, nas de 6 e 8 células, o último são a pose parada)."""
    origem, espelhar = UMA_DIRECAO.get(animacao, {}).get(direcao) or (ESPELHO.get(direcao, direcao), direcao in ESPELHO)
    tira = Image.open(GEMINI / f"{animacao}-{ARQUIVO[origem]}.png").convert("RGBA")
    out = [tira.crop((i * N, 0, (i + 1) * N, N)) for i in range(1, qt + 1)]
    return [ImageOps.mirror(q) for q in out] if espelhar else out


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    antes = len(DIRECOES) * (1 + sum(qt for _, qt, _ in ANIMACOES) + sum(qt for _, qt, _ in DORMIR))
    tags = []  # (nome, duração, quadros)
    for nome, qt, ms in ESPORTES:
        for d in DIRECOES:
            tags.append((f"{nome}-{d}", ms, quadros(nome, qt, d)))

    s = str(FONTE)
    with Aseprite() as a, tempfile.TemporaryDirectory() as t:
        info = a("get_sprite_info", sprite_path=s)
        if info["frame_count"] != antes:
            sys.exit(f"{FONTE.name}: esperado {antes} quadros (sem os esportes); veio {info['frame_count']}")
        camada = info["layers"][0]
        # Uma tag que termina no último quadro cresce a cada add_frame: a última que já existe sai
        # antes e volta depois, e as novas só entram depois de todos os quadros.
        ultima, qt_ultima, _ = DORMIR[-1]
        ultima = f"{ultima}-{DIRECOES[-1]}"
        a("delete_tag", sprite_path=s, tag_name=ultima)
        tags.insert(0, (ultima, None, [None] * qt_ultima))
        total = sum(len(q) for _, ms, q in tags if ms)
        png = pathlib.Path(t) / "quadro.png"
        i = antes
        for _, ms, qs in tags[1:]:
            for q in qs:
                i += 1
                a("add_frame", sprite_path=s, duration_ms=ms)
                q.save(png)
                a("import_image", sprite_path=s, image_path=str(png), layer_name=camada, frame_number=i)
                print(f"quadro {i - antes}/{total}", end="\r", flush=True)
        print()
        inicio = antes + 1 - qt_ultima
        for nome, _, qs in tags:
            a("create_tag", sprite_path=s, tag_name=nome, from_frame=inicio, to_frame=inicio + len(qs) - 1,
              direction="forward")
            inicio += len(qs)
    print(f"{FONTE.name}: {total} quadros e {len(tags) - 1} tags acrescentados")


if __name__ == "__main__":
    main()
