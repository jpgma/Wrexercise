# Formato da folha de avatar — Wrexercise

> Especificação pública da folha de sprites usada pelos avatares do modo Jogo (`gamificacao.md` §4). Qualquer pessoa pode desenhar uma folha neste formato e importá-la no aplicativo.
>
> **Em revisão:** a folha vista de cima, em 8 direções, está proposta em `decisoes/0039-avatar-em-8-direcoes.md`. A cena e a importação usam só o formato de §6. As §1 a §5 descrevem o formato de lado antigo, que não é mais aceito; ficam como referência.

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

## 6. Folha em 8 direções (em teste)

É o formato da folha padrão do bundle, `avatar-base-masculino.png`, gerada por `pixel-art/base-masculino/build.py`, e o único aceito na importação (ADR 0039, Andamento).

| Requisito | Valor |
|---|---|
| Tipo | PNG RGBA de 8 bits ou com paleta indexada (a folha padrão é indexada, com o índice 0 transparente) |
| Tamanho máximo | 1000 KB |
| Animação | proibida dentro do PNG (sem `acTL`) |
| Dimensões | 432 × 4656 px: 9 colunas × 97 linhas de células de 48 × 48 px |
| Vista | de cima, em 8 direções, sem espelho |
| Âncora | o ponto entre os pés fica em (24, 44), em todos os quadros |

- **Direções**, nesta ordem: `sul` (de frente para quem vê a tela), `sudeste`, `leste`, `nordeste`, `norte`, `noroeste`, `oeste`, `sudoeste`.
- **Linhas:** a linha 0 é `rotacao`, com uma direção por coluna. Depois, cada animação ocupa 8 linhas, uma por direção, com um quadro por coluna.

| Primeira linha | Nome | Quadros | Tempo por quadro | Usada no Jogo como |
|---:|---|---:|---:|---|
| 0 | `rotacao` | 1 por direção | — | `retrato` (recorte de 24 × 24 px em (12, 10) da direção `sul`) |
| 1 | `andar` | 8 | 100 ms | `andar` |
| 9 | `correr` | 8 | 100 ms | sem uso por enquanto |
| 17 | `arremessar` | 6 | 80 ms | `arremessar`; a carta sai no quarto quadro |
| 25 | `comemorar` | 9 | 120 ms | `comemorando` |
| 33 | `receber` | 6 | 100 ms | `receber`; a carta atinge o peito no terceiro quadro |
| 41 | `parado` | 4 | 200 ms | `parado` |
| 49 | `treinando` | 6 | 90 ms | `treinando` |
| 57 | `alongar` | 4 | 150 ms | `alongar`, de vez em quando, para quem está parado; uma vez |
| 65 | `olhar` | 4 | 200 ms | `olhar`, de vez em quando, para quem está parado; uma vez |
| 73 | `aceitar` | 4 | 120 ms | `aceitar` uma carta; uma vez |
| 81 | `recusar` | 3 | 150 ms | `recusar` uma carta; uma vez |
| 89 | `provocar` | 4 | 120 ms | `provocar`; uma vez |

- As linhas 0 a 48 são as da primeira versão, de 49 linhas (432 × 2352), que não é mais aceita (migração `023`). Para adaptar uma folha dessa versão, copie as linhas de `correr` para `treinando` e as de `parado` para as outras animações novas, como faz `pixel-art/jpgma/build.py`.
- As colunas além da quantidade de quadros de cada linha ficam sem uso.

- **Importação:** só o tamanho é conferido; o desenho é livre, e linhas vazias ficam transparentes na cena. Em `/perfil/avatar`, "Exportar folha" baixa a folha atual, e "Exportar guia" baixa uma camada do mesmo tamanho com a grade, o nome de cada linha, as células sem uso sombreadas e a âncora. O navegador envia como veio um PNG RGBA de 8 bits ou indexado; qualquer outra imagem do tamanho certo é re-codificada como PNG RGBA. O servidor confere tudo de novo.
- **Direção na cena:** ao andar, o avatar olha para onde vai. Ao arremessar, provocar ou receber, olha para o alvo. Parado, olha para o centro da elipse, na direção mais próxima (quem fica embaixo aparece de costas).
