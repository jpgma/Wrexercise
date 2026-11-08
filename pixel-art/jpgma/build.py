"""Avatar `jpgma` em 8 direções (docs/decisoes/0039-avatar-em-8-direcoes.md, proposta).

Junta as duas exportações do PixelLab de fonte/ numa folha só, sem redesenhar nada:
- cada animação × direção vem de um dos arquivos (tabela FONTES);
- o ruído de cor do Cheering vai para a cor mais próxima das 16 da paleta, e os vermelhos
  da boca viram duas cores novas (`BOCA`, `BOCA_ESCURA`);
- cada quadro de 64×64 é recortado para 48×48 com o mesmo deslocamento, sem reamostrar.

Uso: python build.py   (exige Pillow e o pixel-plugin configurado; ver docs/avatar-kit.md §6)
Gera saida/jpgma.png, saida/jpgma.json, saida/jpgma.aseprite e preview/.
"""

from __future__ import annotations

import colorsys
import hashlib
import json
import pathlib
import sys
from collections import Counter

from PIL import Image

AQUI = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI.parent / "avatar-base"))
from aseprite_mcp import Aseprite  # noqa: E402

FONTE_N = 64  # quadro das exportações do PixelLab
N = 48  # célula da folha
OX, OY = 8, 1  # recorte fixo: x 8..55 e y 1..48 do quadro de 64
MS = 200  # o PixelLab usa 200 ms por quadro em todas as animações
CAMADA = "Idle"  # camada única das exportações

# Ordem das direções nas tags do PixelLab e nas linhas da folha.
DIRECOES = ["sul", "sudeste", "leste", "nordeste", "norte", "noroeste", "oeste", "sudoeste"]
DIRECOES_PIXELLAB = ["south", "south-east", "east", "north-east", "north", "north-west", "west", "south-west"]

# Tags de cada arquivo, na ordem dos quadros: (tag, direções ou None, quadros por direção).
# O pixel-mcp não lê tags; por isso elas ficam aqui e o build confere o total de quadros.
FONTES = {
    "a": ("fonte/jpgma-a.aseprite", 114, [
        ("rotations", None, 8),
        ("Walking", DIRECOES_PIXELLAB, 8),
        ("Throw_Object", ["south-east", "east", "north-east", "north-west", "west", "south-west"], 7),
    ]),
    "b": ("fonte/jpgma-b.aseprite", 206, [
        ("rotations", None, 8),
        ("Taking_Punch", DIRECOES_PIXELLAB, 6),
        ("Throw_Object", ["south", "north"], 7),
        ("Cheering", DIRECOES_PIXELLAB, 9),
        ("Running", DIRECOES_PIXELLAB, 8),
    ]),
}

# Animações da folha, na ordem das linhas: (nome, tag do PixelLab). A linha 0 é `rotacao`,
# com uma direção por coluna; cada animação tem 8 linhas, uma por direção.
ANIMACOES = [
    ("andar", "Walking"),
    ("correr", "Running"),
    ("arremessar", "Throw_Object"),
    ("comemorar", "Cheering"),
    ("receber", "Taking_Punch"),  # reação ao ser atingido pela carta
]

# As 16 cores das exportações do PixelLab (o arquivo "a" usa só estas).
PALETA_BASE = [
    "#0e071b", "#150b0d", "#1c1215", "#1f1a21", "#282128", "#291a1b", "#2f2930", "#322c33",
    "#363037", "#392627", "#493334", "#8a523e", "#c26c4d", "#dcd1d9", "#e0855a", "#f7a972",
]
BOCA, BOCA_ESCURA = "#e74647", "#702623"
PALETA = PALETA_BASE + [BOCA, BOCA_ESCURA]


def rgb(c: str) -> tuple[int, int, int]:
    return int(c[1:3], 16), int(c[3:5], 16), int(c[5:7], 16)


def lab(c: str) -> tuple[float, float, float]:
    def lin(v):
        v /= 255
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4

    def f(t):
        return t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116

    r, g, b = (lin(v) for v in rgb(c))
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883
    return 116 * f(y) - 16, 500 * (f(x) - f(y)), 200 * (f(y) - f(z))


def destino(c: str) -> str:
    """Cor da paleta para um pixel: a própria, um vermelho da boca ou a mais próxima das 16 (CIELAB)."""
    if c in PALETA:
        return c
    h, s, v = colorsys.rgb_to_hsv(*(x / 255 for x in rgb(c)))
    if s >= 0.5 and (h * 360 <= 8 or h * 360 >= 345):
        return BOCA if v >= 0.8 else BOCA_ESCURA
    l0 = lab(c)
    return min(PALETA_BASE, key=lambda o: sum((p - q) ** 2 for p, q in zip(l0, lab(o))))


def hash_fontes() -> str:
    h = hashlib.sha256()
    for caminho, _, _ in FONTES.values():
        h.update((AQUI / caminho).read_bytes())
    return h.hexdigest()


def ler_fontes() -> dict[str, list[dict]]:
    """Todos os quadros de cada fonte: {fonte: [{(x, y): '#rrggbb[aa]'}]}. Cache em preview/fontes.json."""
    cache = AQUI / "preview/fontes.json"
    chave = hash_fontes()
    if cache.exists():
        dados = json.loads(cache.read_text(encoding="utf-8"))
        if dados.get("sha256") == chave:
            return {f: [{tuple(map(int, k.split(","))): v for k, v in q.items()} for q in qs]
                    for f, qs in dados["quadros"].items()}
    out = {}
    with Aseprite() as a:
        for f, (caminho, total, tags) in FONTES.items():
            info = a("get_sprite_info", sprite_path=str(AQUI / caminho))
            if info["frame_count"] != total or info["width"] != FONTE_N or info["layers"] != [CAMADA]:
                sys.exit(f"{caminho}: esperado {total} quadros {FONTE_N}×{FONTE_N} na camada {CAMADA}; veio {info}")
            if sum(qt * (len(d) if d else 1) for _, d, qt in tags) != total:
                sys.exit(f"{caminho}: as tags de FONTES não somam {total} quadros")
            out[f] = []
            for i in range(1, total + 1):
                out[f].append(ler_quadro(a, AQUI / caminho, i))
                print(f"{caminho}: quadro {i}/{total}", end="\r", flush=True)
            print()
    cache.parent.mkdir(exist_ok=True)
    cache.write_text(json.dumps({"sha256": chave, "quadros": {
        f: [{f"{x},{y}": v for (x, y), v in q.items()} for q in qs] for f, qs in out.items()}}), encoding="utf-8")
    return out


def ler_quadro(a: Aseprite, sprite: pathlib.Path, quadro: int) -> dict:
    """Pixels não transparentes do quadro, guardando o alfa quando não é 255 (o build recusa)."""
    try:
        r = a("get_pixels", sprite_path=str(sprite), layer_name=CAMADA, frame_number=quadro,
              x=0, y=0, width=FONTE_N, height=FONTE_N, page_size=FONTE_N * FONTE_N)
    except RuntimeError as e:
        if "No cel found" in str(e):
            return {}
        raise
    out = {}
    for p in r["pixels"]:
        c = p["color"].lower()
        alfa = c[7:9] if len(c) == 9 else "ff"
        if alfa != "00":
            out[(p["x"], p["y"])] = c[:7] if alfa == "ff" else c
    return out


def indexar(quadros: dict[str, list[dict]]) -> dict[tuple[str, str | None], list[dict]]:
    """{(tag, direção do PixelLab ou None): [quadros]}. Uma tag nas duas fontes tem de ser idêntica."""
    out: dict[tuple[str, str | None], list[dict]] = {}
    for f, (_, _, tags) in FONTES.items():
        i = 0
        for tag, direcoes, qt in tags:
            for d in direcoes or [None]:
                trecho = quadros[f][i:i + qt]
                i += qt
                if (tag, d) in out and out[(tag, d)] != trecho:
                    sys.exit(f"{tag} {d or ''} difere entre as fontes")
                out[(tag, d)] = trecho
    return out


def montar(fontes: dict) -> tuple[list[list[dict]], dict, Counter]:
    """Linhas da folha, já na paleta e recortadas, o JSON da folha e a contagem de trocas de cor."""
    trocas: Counter = Counter()

    def converter(q: dict, onde: str) -> dict:
        out = {}
        for (x, y), c in q.items():
            if len(c) != 7:
                sys.exit(f"{onde}: pixel semitransparente em ({x}, {y})")
            nx, ny = x - OX, y - OY
            if not (0 <= nx < N and 0 <= ny < N):
                sys.exit(f"{onde}: pixel ({x}, {y}) fora do recorte {N}×{N} em ({OX}, {OY})")
            d = destino(c)
            if d != c:
                trocas[(c, d)] += 1
            out[(nx, ny)] = d
        return out

    rotacoes = fontes[("rotations", None)]
    linhas = [[converter(q, f"rotations-{DIRECOES_PIXELLAB[k]}") for k, q in enumerate(rotacoes)]]
    meta = {
        "celula": N,
        # Ponto entre os pés: centro da célula, na linha mais baixa da rotação de frente.
        "ancora": {"x": N // 2, "y": max(y for _, y in rotacoes[0]) - OY},
        "direcoes": DIRECOES,
        "animacoes": {"rotacao": {"linha": 0, "quadros": 1, "ms": None, "origem": "rotations"}},
    }
    for nome, tag in ANIMACOES:
        primeira = len(linhas)
        qt = None
        for d in DIRECOES_PIXELLAB:
            if (tag, d) not in fontes:
                sys.exit(f"falta {tag}-{d} nas fontes")
            trecho = fontes[(tag, d)]
            qt = qt or len(trecho)
            if len(trecho) != qt:
                sys.exit(f"{tag}: quantidade de quadros diferente em {d}")
            linhas.append([converter(q, f"{tag}-{d} quadro {i + 1}") for i, q in enumerate(trecho)])
        meta["animacoes"][nome] = {"linha": primeira, "quadros": qt, "ms": MS, "origem": tag}
    return linhas, meta, trocas


def imagem(q: dict, dx: int = 0, dy: int = 0) -> Image.Image:
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    for (x, y), c in q.items():
        im.putpixel((x + dx, y + dy), (*rgb(c), 255))
    return im


def escrever_folha(linhas: list[list[dict]]) -> Image.Image:
    folha = Image.new("RGBA", (max(len(linha) for linha in linhas) * N, len(linhas) * N), (0, 0, 0, 0))
    for j, linha in enumerate(linhas):
        for i, q in enumerate(linha):
            folha.paste(imagem(q), (i * N, j * N))
    return folha


def escrever_aseprite(linhas: list[list[dict]], meta: dict, arquivo: pathlib.Path):
    """Um quadro por célula e uma tag `animacao-direcao`, para tocar a folha no Aseprite."""
    seq: list[tuple[str, dict]] = [("rotacao", q) for q in linhas[0]]
    for nome, _ in ANIMACOES:
        primeira = meta["animacoes"][nome]["linha"]
        for k, d in enumerate(DIRECOES):
            seq += [(f"{nome}-{d}", q) for q in linhas[primeira + k]]
    with Aseprite() as a:
        tmp = a("create_canvas", width=N, height=N, color_mode="rgb")["file_path"]
        arquivo.unlink(missing_ok=True)
        a("save_as", sprite_path=tmp, output_path=str(arquivo))
        s = str(arquivo)
        camada = a("get_sprite_info", sprite_path=s)["layers"][0]
        a("set_frame_duration", sprite_path=s, frame_number=1, duration_ms=MS)
        for _ in seq[1:]:
            a("add_frame", sprite_path=s, duration_ms=MS)
        for i, (_, q) in enumerate(seq, start=1):
            if q:
                a("draw_pixels", sprite_path=s, layer_name=camada, frame_number=i,
                  pixels=[{"x": x, "y": y, "color": c} for (x, y), c in sorted(q.items())])
            print(f"aseprite: quadro {i}/{len(seq)}", end="\r", flush=True)
        print()
        inicio = 1
        while inicio <= len(seq):
            tag, fim = seq[inicio - 1][0], inicio
            while fim < len(seq) and seq[fim][0] == tag:
                fim += 1
            a("create_tag", sprite_path=s, tag_name=tag, from_frame=inicio, to_frame=fim, direction="forward")
            inicio = fim + 1
        a("set_palette", sprite_path=s, colors=PALETA)


def escrever_previews(folha: Image.Image, linhas: list[list[dict]], meta: dict, fontes: dict):
    prev = AQUI / "preview"
    prev.mkdir(exist_ok=True)
    fundo = (0x2B, 0x30, 0x42, 255)
    folha.resize((folha.width * 4, folha.height * 4), Image.NEAREST).save(prev / "jpgma-4x.png")
    rot = Image.new("RGBA", (8 * N, N), fundo)
    for k, q in enumerate(linhas[0]):
        rot.alpha_composite(imagem(q), (k * N, 0))
    rot.resize((rot.width * 3, rot.height * 3), Image.NEAREST).save(prev / "rotacao.png")
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
        quadros[0].save(prev / f"{nome}.gif", save_all=True, append_images=quadros[1:], duration=MS, loop=0)
    # Cheering de frente antes (em cima) e depois (embaixo) da troca de cores, em 6×.
    antes = fontes[("Cheering", "south")]
    depois = linhas[meta["animacoes"]["comemorar"]["linha"]]
    tira = Image.new("RGBA", (len(antes) * N, 2 * N), fundo)
    for i, q in enumerate(antes):
        tira.alpha_composite(imagem(q, -OX, -OY), (i * N, 0))
        tira.alpha_composite(imagem(depois[i]), (i * N, N))
    tira.resize((tira.width * 6, tira.height * 6), Image.NEAREST).save(prev / "comemorar-antes-depois.png")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    fontes = indexar(ler_fontes())
    linhas, meta, trocas = montar(fontes)

    saida = AQUI / "saida"
    saida.mkdir(exist_ok=True)
    folha = escrever_folha(linhas)
    png = saida / "jpgma.png"
    folha.save(png, optimize=True)
    meta = {"largura": folha.width, "altura": folha.height, **meta, "paleta": PALETA,
            "sha256": hashlib.sha256(folha.tobytes()).hexdigest()}

    arq_meta = saida / "jpgma.json"
    anterior = json.loads(arq_meta.read_text(encoding="utf-8")) if arq_meta.exists() else {}
    ase = saida / "jpgma.aseprite"
    mudou = any(anterior.get(k) != meta[k] for k in ("sha256", "animacoes", "direcoes"))
    if mudou or not ase.exists():
        escrever_aseprite(linhas, meta, ase)
    arq_meta.write_text(json.dumps(meta, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    escrever_previews(folha, linhas, meta, fontes)

    print("trocas de cor (destino ← origem×pixels):")
    por_destino: dict[str, list[str]] = {}
    for (c, d), n in sorted(trocas.items(), key=lambda t: -t[1]):
        por_destino.setdefault(d, []).append(f"{c}×{n}")
    for d, origens in por_destino.items():
        print(f"  {d} ← {', '.join(origens)}")
    print(f"{png.relative_to(AQUI).as_posix()}: {folha.width}×{folha.height}, "
          f"{png.stat().st_size / 1024:.1f} KB, {sum(len(linha) for linha in linhas)} quadros")


if __name__ == "__main__":
    main()
