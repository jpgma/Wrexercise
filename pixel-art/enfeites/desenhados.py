"""Enfeites desenhados por código (ADR 0084): as conquistas de distância do Percurso.

Cada função devolve os 4 quadros (40×40, RGBA) de um objeto apoiado na base da célula, com o contorno escuro
e as cores da folha do ChatGPT, para combinar com os outros Enfeites. Escala: o poste de luz tem ~33 px; a
bicicleta, ~22 px.

- fundista: marco de quilômetro de concreto ("42"), com uma bandeirinha coral tremulando.
- peregrino: placa de trilha de madeira com duas setas e a marca da trilha; a plaquinha pendurada balança.
- estradeiro: bicicletário de dois arcos, com um cadeado coral balançando.
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2] / ".claude/skills/pixel-art-studio/scripts"))
from pixelstudio import Sprite  # noqa: E402

LADO = 40
CONTORNO = "#0e071b"
CINZA = ["#363c52", "#474e68", "#5f6783", "#8b92ab", "#b3b8cb", "#d4d8e4"]
CREME, CREME_SOMBRA = "#f5f1e8", "#d6d0c0"
CORAL_ESCURO, CORAL, CORAL_CLARO = "#c4432c", "#ff684a", "#ff9c80"
MADEIRA = ["#6e4125", "#8d5a36", "#a8693f", "#c98a5a"]

# O vaivém dos 4 quadros: parado, um lado, parado, o outro.
BALANCO = [0, 1, 0, -1]


def _quadro(desenhar, i):
    s = Sprite(LADO, LADO)
    desenhar(s, i)
    s.outline(CONTORNO, where="outside")
    return s.composite(1)


def fundista():
    def desenhar(s, i):
        # O pé de concreto.
        s.rect(13, 36, 25, 38, CINZA[3])
        s.rect(13, 38, 25, 38, CINZA[2])
        # O marco: corpo creme com o topo arredondado e a faixa coral.
        s.rect(15, 21, 23, 35, CREME)
        s.rect(16, 20, 22, 20, CREME)
        s.rect(17, 19, 21, 19, CREME)
        s.rect(23, 21, 23, 35, CREME_SOMBRA)
        s.rect(15, 23, 23, 25, CORAL)
        s.rect(23, 23, 23, 25, CORAL_ESCURO)
        s.rect(15, 23, 15, 25, CORAL_CLARO)
        # "42" em 3×5.
        quatro = ["x.x", "x.x", "xxx", "..x", "..x"]
        dois = ["xxx", "..x", "xxx", "x..", "xxx"]
        for dx0, digito in ((16, quatro), (20, dois)):
            for y, linha in enumerate(digito):
                for x, c in enumerate(linha):
                    if c == "x":
                        s.px(dx0 + x, 28 + y, CINZA[1])
        # A bandeirinha: o mastro e o triângulo que tremula.
        s.rect(28, 13, 28, 37, CINZA[4])
        ponta = [(34, 16), (35, 17), (34, 18), (33, 17)][i]
        s.polygon([(29, 13), (29, 19), ponta], CORAL)
        s.line(29, 19, ponta[0], ponta[1], CORAL_ESCURO)
        s.px(29, 13, CORAL_CLARO)

    return [_quadro(desenhar, i) for i in range(4)]


def peregrino():
    def desenhar(s, i):
        # Pedrinhas no pé da placa.
        s.rect(13, 37, 16, 38, CINZA[2])
        s.rect(14, 37, 15, 37, CINZA[3])
        s.rect(23, 37, 25, 38, CINZA[2])
        s.px(24, 37, CINZA[3])
        # O poste.
        s.rect(18, 10, 20, 38, MADEIRA[2])
        s.rect(20, 10, 20, 38, MADEIRA[0])
        s.rect(18, 10, 18, 38, MADEIRA[3])
        # A marca da trilha: branco em cima, coral embaixo.
        s.rect(18, 28, 20, 28, CREME)
        s.rect(18, 29, 20, 30, CORAL)
        # A seta de cima, para a direita.
        s.rect(13, 13, 29, 17, MADEIRA[3])
        s.rect(13, 17, 29, 17, MADEIRA[1])
        s.rect(30, 14, 30, 16, MADEIRA[3])
        s.px(31, 15, MADEIRA[3])
        s.rect(30, 16, 30, 16, MADEIRA[1])
        s.line(15, 15, 26, 15, MADEIRA[1])
        # A seta de baixo, para a esquerda.
        s.rect(9, 20, 25, 24, MADEIRA[3])
        s.rect(9, 24, 25, 24, MADEIRA[1])
        s.rect(8, 21, 8, 23, MADEIRA[3])
        s.px(7, 22, MADEIRA[3])
        s.px(8, 23, MADEIRA[1])
        s.line(12, 22, 23, 22, MADEIRA[1])
        # A plaquinha pendurada na seta de baixo, que balança.
        dx = BALANCO[i]
        s.line(12, 25, 12 + dx, 27, CINZA[3])
        s.rect(10 + dx, 28, 14 + dx, 31, CREME)
        s.rect(10 + dx, 31, 14 + dx, 31, CREME_SOMBRA)
        s.px(12 + dx, 29, CORAL)

    return [_quadro(desenhar, i) for i in range(4)]


def estradeiro():
    def arco(s, x0, x1, topo):
        # Tubo de 2 px em U invertido, com os cantos de cima arredondados na diagonal. A perna da esquerda
        # pega a luz; a da direita fica na sombra.
        s.rect(x0 + 2, topo, x1 - 2, topo, CINZA[5])
        s.rect(x0 + 1, topo + 1, x1 - 1, topo + 1, CINZA[4])
        s.rect(x0, topo + 2, x0 + 1, 36, CINZA[4])
        s.rect(x0, topo + 2, x0, 36, CINZA[5])
        s.rect(x1 - 1, topo + 2, x1, 36, CINZA[2])
        s.px(x1 - 1, topo + 1, CINZA[3])

    def desenhar(s, i):
        # A base chumbada no chão.
        s.rect(6, 37, 33, 38, CINZA[1])
        s.rect(6, 37, 33, 37, CINZA[2])
        arco(s, 8, 18, 19)
        arco(s, 21, 31, 19)
        # O cadeado em U, pendurado no segundo arco, que balança.
        dx = BALANCO[i]
        s.rect(25 + dx, 21, 25 + dx, 24, CINZA[4])
        s.rect(28 + dx, 21, 28 + dx, 24, CINZA[4])
        s.rect(24 + dx, 25, 29 + dx, 28, CORAL)
        s.rect(24 + dx, 28, 29 + dx, 28, CORAL_ESCURO)
        s.rect(24 + dx, 25, 24 + dx, 27, CORAL_CLARO)
        s.px(27 + dx, 26, CONTORNO)

    return [_quadro(desenhar, i) for i in range(4)]


DESENHOS = {"fundista": fundista, "peregrino": peregrino, "estradeiro": estradeiro}
