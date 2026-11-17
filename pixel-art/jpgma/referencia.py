"""Imagens de referência do `jpgma` para gerar animações no Gemini, uma por direção.

Lê a linha `rotacao` de saida/jpgma.png (48×48 por direção) e grava
saida/referencia-gemini/<direcao>.png: a pose parada numa célula de 48×64, ampliada 8× sobre
magenta (384×512), o mesmo formato de pixel-art/base-masculino/tira.py. A célula de 48×48 fica
na linha 1 da de 64, como no quadro original do PixelLab (recorte em (8, 1)).

Uso: python referencia.py   (exige Pillow)
"""

from __future__ import annotations

import pathlib

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
FOLHA = AQUI / "saida" / "jpgma.png"
SAIDA = AQUI / "saida" / "referencia-gemini"

# Ordem das colunas da `rotacao` (DIRECOES do build.py), com o nome que os arquivos do Gemini usam.
DIRECOES = ["south", "southeast", "east", "northeast", "north", "northwest", "west", "southwest"]

N = 48  # célula da folha
ALTURA, OY = 64, 1  # célula da referência e a linha em que a célula da folha entra nela
ESCALA = 8
MAGENTA = (255, 0, 255, 255)


def ampliar(q: Image.Image) -> Image.Image:
    """Uma célula de 48×48 da folha como referência: 48×64 sobre magenta, ampliada sem suavização."""
    celula = Image.new("RGBA", (N, ALTURA), MAGENTA)
    celula.alpha_composite(q, (0, OY))
    return celula.resize((N * ESCALA, ALTURA * ESCALA), Image.NEAREST).convert("RGB")


def main() -> None:
    folha = Image.open(FOLHA).convert("RGBA")
    SAIDA.mkdir(parents=True, exist_ok=True)
    for i, nome in enumerate(DIRECOES):
        ampliar(folha.crop((i * N, 0, (i + 1) * N, N))).save(SAIDA / f"{nome}.png")
        # A pose sentada de `dormindo` (a 2ª célula da tira do gemini.py): a segunda imagem para
        # anexar em `adormecer`, `acordar` e `levantar`. Só existe depois do gemini.py.
        tira = AQUI / "saida" / "gemini" / f"dormindo-{nome}.png"
        if tira.exists():
            sentado = Image.open(tira).convert("RGBA").crop((N, 0, 2 * N, N))
            ampliar(sentado).save(SAIDA / f"dormindo-{nome}.png")
    print(f"saida/referencia-gemini/<direcao>.png: {len(DIRECOES)} direções, {N * ESCALA}×{ALTURA * ESCALA}")


if __name__ == "__main__":
    main()
