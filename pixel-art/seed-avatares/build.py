"""Folhas de avatar tingidas para o seed de testes (scripts/seed/README.md).

Troca só a paleta da folha padrão (Wrexercise/webapp/public/avatar-base-masculino.png): os pixels, a grade
e a âncora continuam os mesmos, então cada folha segue o formato de docs/avatar-formato.md §6.
Cada cor da paleta cai numa classe pelo matiz, saturação e luminosidade, e a classe recebe a cor da pessoa
mantendo a luminosidade original, para preservar o sombreamento.

    python pixel-art/seed-avatares/build.py

Grava scripts/seed/avatares/<nome>.png (usado pelo seed.bat) e saida/preview.png.
"""
import colorsys
import pathlib
import sys

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
RAIZ = AQUI.parents[1]
BASE = RAIZ / "Wrexercise/webapp/public/avatar-base-masculino.png"
DESTINO = RAIZ / "scripts/seed/avatares"

# Nome do arquivo no seed: 32 dígitos hexadecimais, como os que o app gera (ArmazenamentoAvatares).
# blusa e calca: (matiz em graus, saturação). cabelo: (matiz, saturação, fator de luminosidade).
# pele: fator de luminosidade.
PESSOAS = {
    "ana": dict(arquivo="5eed0000000000000000000000000001", blusa=(350, 0.62), calca=(222, 0.38),
                cabelo=(265, 0.12, 0.75), pele=1.08),
    "bruno": dict(arquivo="5eed0000000000000000000000000002", blusa=(172, 0.55), calca=(80, 0.18),
                  cabelo=(40, 0.60, 2.6), pele=0.78),
    "carla": dict(arquivo="5eed0000000000000000000000000003", blusa=(275, 0.48), calca=(208, 0.45),
                  cabelo=(14, 0.62, 1.7), pele=1.0),
    "diego": dict(arquivo="5eed0000000000000000000000000004", blusa=(48, 0.75), calca=(0, 0.0),
                  cabelo=(3, 0.25, 1.0), pele=0.6),
}


def classe(r, g, b):
    h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
    h *= 360
    if l < 0.15:
        return "contorno"
    if s > 0.6:
        return "detalhe"
    if s < 0.16 and l > 0.76:
        return "branco"  # olhos e tênis continuam brancos
    if s < 0.16:
        return "blusa" if l >= 0.45 else "calca"
    quente = h <= 30 or h >= 345
    if quente and l < 0.30:
        return "cabelo"
    if quente:
        return "pele"
    return "outro"


def tingir(cor, pessoa):
    r, g, b = cor
    h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
    c = classe(r, g, b)
    if c in ("blusa", "calca"):
        matiz, sat = pessoa[c]
        h, s = matiz / 360, sat
    elif c == "cabelo":
        matiz, sat, fator = pessoa["cabelo"]
        h, s, l = matiz / 360, sat, min(l * fator, 0.85)
    elif c == "pele":
        l = min(l * pessoa["pele"], 0.92)
    else:
        return cor
    return tuple(round(v * 255) for v in colorsys.hls_to_rgb(h, l, s))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    base = Image.open(BASE)
    if base.mode != "P":
        sys.exit(f"{BASE}: esperava PNG indexado, veio {base.mode}")
    paleta = base.getpalette()
    cores = [tuple(paleta[i:i + 3]) for i in range(0, len(paleta), 3)]
    DESTINO.mkdir(parents=True, exist_ok=True)

    folhas = [base]
    for nome, pessoa in PESSOAS.items():
        # O índice 0 é o transparente: fica como está.
        nova = [cores[0]] + [tingir(c, pessoa) for c in cores[1:]]
        folha = base.copy()
        folha.putpalette([v for c in nova for v in c])
        png = DESTINO / f"{pessoa['arquivo']}.png"
        folha.save(png, optimize=True, transparency=0)
        verificar(png)
        print(f"{nome}: {png.relative_to(RAIZ)} ({png.stat().st_size} bytes)")
        folhas.append(folha)

    # Pré-visualização: a linha de rotação (8 direções, 48 px) de cada folha, ampliada 4x.
    escala = 4
    prev = Image.new("RGBA", (8 * 48 * escala, len(folhas) * 48 * escala), (40, 44, 52, 255))
    for i, folha in enumerate(folhas):
        linha = folha.convert("RGBA").crop((0, 0, 8 * 48, 48)).resize((8 * 48 * escala, 48 * escala), Image.NEAREST)
        prev.alpha_composite(linha, (0, i * 48 * escala))
    (AQUI / "saida").mkdir(exist_ok=True)
    prev.save(AQUI / "saida/preview.png")


def verificar(png):
    folha = Image.open(png)
    if folha.size != (432, 4656) or png.stat().st_size > 1000 * 1024:
        sys.exit(f"{png}: fora do formato ({folha.size}, {png.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
