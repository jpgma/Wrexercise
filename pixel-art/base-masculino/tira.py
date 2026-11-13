"""Tira das 8 direções do base masculino, a partir de fonte/<direcao>.png (64×64 cada).

Gera:
- saida/rotations-tira.png: as 8 direções lado a lado (512×64), em tamanho real, fundo
  transparente, na ordem do PixelLab (DIRECOES do rotacoes.py);
- saida/referencia-gemini.png: as mesmas 8 direções numa grade de 4 colunas e 2 linhas, na
  mesma ordem e no mesmo formato que prompts-gemini.md §3 pede (1536×1024, fundo magenta),
  ampliada 8× sem suavização. É a imagem de referência da `rotations` no Gemini;
- saida/referencia-gemini/<direcao>.png: cada direção sozinha, no mesmo recorte de 48×64,
  ampliada 8× sobre magenta (384×512). É a imagem para anexar nas animações
  (prompts-gemini.md §4);
- saida/referencia-gemini/dormindo-<direcao>.png: a pose sentada de `dormindo` (o 2º quadro de
  saida/gemini/dormindo-<direcao>.png, com o assento na linha do chão), no mesmo formato. É a
  segunda imagem para anexar em `adormecer`, `acordar` e `levantar`. Só sai depois do
  gemini.py, para as direções que já têm `dormindo`.

Uso: python tira.py   (exige Pillow)
"""

from __future__ import annotations

import pathlib

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
FONTE = AQUI / "fonte"
SAIDA = AQUI / "saida"

# Mesma ordem do rotacoes.py (DIRECOES), com o nome do arquivo sem hífen.
DIRECOES = ["south", "southeast", "east", "northeast", "north", "northwest", "west", "southwest"]

QUADRO = 64
# Na grade do Gemini, cada célula é um recorte de 48×64 do quadro (o personagem tem cerca de
# 18 px de largura, centrado), para que 4×2 células deem 3:2.
CELULA_X0, CELULA_L = 8, 48
ESCALA = 8  # 4 × 48 × 8 = 1536; 2 × 64 × 8 = 1024
MAGENTA = (255, 0, 255, 255)


def main() -> None:
    quadros = []
    for nome in DIRECOES:
        q = Image.open(FONTE / f"{nome}.png").convert("RGBA")
        if q.size != (QUADRO, QUADRO):
            raise SystemExit(f"fonte/{nome}.png tem {q.size}, esperado {QUADRO}×{QUADRO}")
        caixa = q.getbbox()
        if caixa and (caixa[0] < CELULA_X0 or caixa[2] > CELULA_X0 + CELULA_L):
            raise SystemExit(f"fonte/{nome}.png sai do recorte de {CELULA_L} px: {caixa}")
        quadros.append(q)

    SAIDA.mkdir(exist_ok=True)
    tira = Image.new("RGBA", (QUADRO * len(quadros), QUADRO), (0, 0, 0, 0))
    for i, q in enumerate(quadros):
        tira.paste(q, (i * QUADRO, 0))
    tira.save(SAIDA / "rotations-tira.png")

    grade = Image.new("RGBA", (CELULA_L * 4, QUADRO * 2), MAGENTA)
    for i, q in enumerate(quadros):
        celula = q.crop((CELULA_X0, 0, CELULA_X0 + CELULA_L, QUADRO))
        grade.alpha_composite(celula, ((i % 4) * CELULA_L, (i // 4) * QUADRO))
    grade = grade.resize((grade.width * ESCALA, grade.height * ESCALA), Image.NEAREST)
    grade.convert("RGB").save(SAIDA / "referencia-gemini.png")

    pasta = SAIDA / "referencia-gemini"
    pasta.mkdir(exist_ok=True)
    for nome, q in zip(DIRECOES, quadros):
        celula = Image.new("RGBA", (CELULA_L, QUADRO), MAGENTA)
        celula.alpha_composite(q.crop((CELULA_X0, 0, CELULA_X0 + CELULA_L, QUADRO)))
        celula = celula.resize((CELULA_L * ESCALA, QUADRO * ESCALA), Image.NEAREST)
        celula.convert("RGB").save(pasta / f"{nome}.png")

    # A pose sentada de dormindo: o 2º quadro, descido até o chão do 1º (o Gemini às vezes deixa
    # o assento 1 ou 2 px acima).
    for nome in DIRECOES:
        arq = SAIDA / "gemini" / f"dormindo-{nome}.png"
        if not arq.exists():
            continue
        t = Image.open(arq).convert("RGBA")
        chao = t.crop((0, 0, QUADRO, QUADRO)).getbbox()[3]
        sentado = t.crop((QUADRO, 0, 2 * QUADRO, QUADRO))
        q = Image.new("RGBA", (QUADRO, QUADRO), (0, 0, 0, 0))
        q.paste(sentado, (0, chao - sentado.getbbox()[3]))
        celula = Image.new("RGBA", (CELULA_L, QUADRO), MAGENTA)
        celula.alpha_composite(q.crop((CELULA_X0, 0, CELULA_X0 + CELULA_L, QUADRO)))
        celula = celula.resize((CELULA_L * ESCALA, QUADRO * ESCALA), Image.NEAREST)
        celula.convert("RGB").save(pasta / f"dormindo-{nome}.png")

    print(f"saida/rotations-tira.png {tira.size}, saida/referencia-gemini.png {grade.size}, "
          f"saida/referencia-gemini/<direcao>.png {celula.size}")


if __name__ == "__main__":
    main()
