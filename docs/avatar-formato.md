# Formato da folha de avatar — Wrexercise

> Especificação pública da folha de sprites usada pelos avatares do modo Jogo (`gamificacao.md` §4). Qualquer pessoa pode desenhar uma folha neste formato e importá-la no aplicativo.
>
> **Em revisão:** a troca por uma folha vista de cima, em 8 direções, está proposta em `decisoes/0039-avatar-em-8-direcoes.md`. Até a decisão, este formato continua valendo.

## 1. Arquivo

| Requisito | Valor |
|---|---|
| Tipo | PNG, RGBA de 8 bits por canal (tipo de cor 6 no `IHDR`) |
| Dimensões | exatamente 256 × 384 px |
| Grade | 8 colunas × 12 linhas de células de 32 × 32 px |
| Tamanho máximo | 64 KB |
| Animação | proibida dentro do PNG (sem `acTL`, ou seja, sem APNG) |
| Fundo | transparente (alfa 0) fora do desenho |

O aplicativo re-codifica a folha no navegador antes de enviar e confere tudo de novo no servidor. Um arquivo fora do formato é recusado.

## 2. Convenções de desenho

- Cada célula mostra o personagem **de lado, virado para a direita**. O aplicativo espelha a célula quando o avatar olha para a esquerda.
- **Âncora:** o ponto entre os pés fica no pixel (16, 31) de cada célula, com a linha 31 como chão. O aplicativo usa esse ponto para posicionar o avatar e decidir quem fica na frente.
- O personagem deve caber na célula em todos os quadros. Ele não se desloca dentro da célula: o aplicativo cuida do deslocamento ao andar.
- Recomendação de proporção: a do template de referência ampliado 2×, com a cabeça de cerca de 8 × 8 px, o corpo de cerca de 8 px de largura e o personagem inteiro com cerca de 28 px de altura, incluindo o contorno.
- Pixel-art sem antisserrilhado: o aplicativo amplia em escala inteira com `image-rendering: pixelated`.

## 3. Linhas

As linhas ficam nesta ordem, de cima para baixo. Os quadros ocupam as colunas da esquerda para a direita, e as colunas que sobram ficam transparentes.

| Linha | Nome | Quadros | Tempo por quadro | Repetição | Obrigatória |
|---:|---|---:|---:|---|---|
| 0 | `retrato` | 1 | — | estático | sim |
| 1 | `parado` | 4 | 200 ms | sim | sim |
| 2 | `andar` | 8 | 100 ms | sim | sim |
| 3 | `treinando` | 6 | 90 ms | sim | não |
| 4 | `comemorando` | 6 | 120 ms | sim | não |
| 5 | `alongar` | 4 | 150 ms | uma vez | não |
| 6 | `olhar` | 4 | 200 ms | uma vez | não |
| 7 | `arremessar` | 5 | 80 ms | uma vez | não |
| 8 | `receber` | 4 | 100 ms | uma vez | não |
| 9 | `aceitar` | 4 | 120 ms | uma vez | não |
| 10 | `recusar` | 3 | 150 ms | uma vez | não |
| 11 | `provocar` | 4 | 120 ms | uma vez | não |

- **`retrato`:** a cabeça e os ombros, de frente ou de três quartos, centrados na célula. O aplicativo a mostra num círculo no lugar das iniciais.
- **`arremessar`:** a carta sai da mão no terceiro quadro. Desenhe a mão à frente do corpo nesse quadro.
- **`receber`:** a carta chega no segundo quadro. Desenhe as mãos à frente do peito.
- Uma linha totalmente transparente conta como ausente. Nesse caso, o aplicativo usa `parado`.

O aplicativo nunca muda os tempos nem a ordem dos quadros. Assim, todas as pessoas veem a mesma animação.

## 4. Licença

- Este formato é público e pode ser usado livremente.
- As folhas padrão que acompanham o aplicativo são **CC0** e ficam no repositório com o arquivo `LICENSE`.
- Uma folha importada continua sendo de quem a enviou. Ela só é mostrada aos membros dos grupos em comum.

## 5. Referência

As animações da arte padrão seguem a estrutura do *2D Platformer Character Template* (CC0), cujos quadros têm 16 × 16 px. A correspondência entre as linhas e as animações do template está em `gamificacao.md` §4.2. As folhas padrão são geradas pelo rig descrito em `avatar-kit.md`.
