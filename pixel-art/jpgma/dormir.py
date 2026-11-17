"""Acrescenta as 4 animações de dormir ao fim de fonte/jpgma.aseprite, pelo servidor MCP do
pixel-plugin (docs/decisoes/0057-avatar-dormindo.md; docs/avatar-formato.md §6, linhas 97 a 128).

Fonte: saida/gemini/<animacao>-<direcao>.png, as tiras de 48×48 do gemini.py, em 5 direções. As
outras três saem espelhando a do outro lado (oeste do leste, noroeste do nordeste e sudoeste do
sudeste). Roda uma vez só: recusa um .aseprite que já tenha esses quadros. Depois dele, os
quadros se editam à mão no Aseprite, como os outros.

Uso: python dormir.py   (exige Pillow e o pixel-plugin configurado; ver docs/avatar-kit.md §6)
Acrescenta um quadro por célula e uma tag `animacao-direcao` por trecho, na ordem de DORMIR do
build.py.
"""

from __future__ import annotations

import pathlib
import sys
import tempfile

from PIL import Image, ImageOps

from build import ANIMACOES, DIRECOES, DORMIR, FONTE, N, Aseprite

AQUI = pathlib.Path(__file__).resolve().parent

GEMINI = AQUI / "saida" / "gemini"
FOLHA = AQUI / "saida" / "jpgma.png"

# Nome da direção nos arquivos do Gemini, na ordem de DIRECOES, e de onde sai a que é espelhada.
ARQUIVO = dict(zip(DIRECOES, ["south", "southeast", "east", "northeast", "north", "northwest", "west", "southwest"]))
ESPELHO = {"oeste": "leste", "noroeste": "nordeste", "sudoeste": "sudeste"}

# Células da tira do gemini.py que entram na tag, em ordem: um índice da própria tira, (animação,
# índice) de outra tira da mesma direção ou "rotacao", a pose parada da folha (as tiras trazem só
# uma cópia aproximada dela, a célula de calibração). A respiração de `dormindo` vai e volta. O
# `adormecer` termina, e o `acordar` e o `levantar` começam, no quadro em que o `dormindo` começa,
# para a troca não pular.
SEQUENCIA = {
    "dormindo": [1, 2, 1, 3],
    "adormecer": ["rotacao", 1, 2, 3, 4, 5, 6, ("dormindo", 1)],
    "acordar": [("dormindo", 1), 2, 3, 4, 5],
    "levantar": [("dormindo", 1), 1, 2, 3, 4, 5, 6, "rotacao"],
}


def fatiar(animacao: str, direcao: str) -> list[Image.Image]:
    tira = Image.open(GEMINI / f"{animacao}-{ARQUIVO[direcao]}.png").convert("RGBA")
    return [tira.crop((i * N, 0, (i + 1) * N, N)) for i in range(tira.width // N)]


def quadros(animacao: str, direcao: str, folha: Image.Image) -> list[Image.Image]:
    origem = ESPELHO.get(direcao, direcao)
    out = []
    for item in SEQUENCIA[animacao]:
        if item == "rotacao":  # a pose parada da própria direção, sem espelho
            i = DIRECOES.index(direcao)
            out.append(folha.crop((i * N, 0, (i + 1) * N, N)))
            continue
        outra, i = item if isinstance(item, tuple) else (animacao, item)
        q = fatiar(outra, origem)[i]
        out.append(ImageOps.mirror(q) if origem != direcao else q)
    return out


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    antes = len(DIRECOES) * (1 + sum(qt for _, qt, _ in ANIMACOES))
    folha = Image.open(FOLHA).convert("RGBA")
    tags = []  # (nome, duração, quadros)
    for nome, qt, ms in DORMIR:
        if len(SEQUENCIA[nome]) != qt:
            sys.exit(f"{nome}: SEQUENCIA tem {len(SEQUENCIA[nome])} quadros, DORMIR tem {qt}")
        for d in DIRECOES:
            tags.append((f"{nome}-{d}", ms, quadros(nome, d, folha)))

    s = str(FONTE)
    with Aseprite() as a, tempfile.TemporaryDirectory() as t:
        info = a("get_sprite_info", sprite_path=s)
        if info["frame_count"] != antes:
            sys.exit(f"{FONTE.name}: esperado {antes} quadros (sem as animações de dormir); veio {info['frame_count']}")
        camada = info["layers"][0]
        # Uma tag que termina no último quadro cresce a cada add_frame: a última que já existe sai
        # antes e volta depois, e as novas só entram depois de todos os quadros.
        ultima, qt_ultima, _ = ANIMACOES[-1]
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
