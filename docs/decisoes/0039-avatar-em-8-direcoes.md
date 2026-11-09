# 0039 — Avatar em 8 direções

**Status:** PROPOSTA. Revisa `0024-cartas-e-avatar-importado.md` e `0037-avatar-importado-e-enfeites.md` (formato da folha). Questão em `../questoes-abertas.md` Q034. Primeira folha em `../../pixel-art/jpgma/`.

## Contexto

Hoje a folha de avatar é de lado: 256×384, células de 32×32, 12 linhas, sempre virada para a direita e espelhada para a esquerda (`avatar-formato.md`). As 6 folhas padrão saem de um rig de 16 px feito a partir de um template de plataforma (`avatar-kit.md`).

A cena já é um chão visto de cima (`gamificacao.md` §3.1), e os avatares andam também na vertical, com o `andar` de lado "deslizando". A ideia é trocar a folha por personagens **vistos de cima, em 8 direções**, gerados no PixelLab. O primeiro é o `jpgma`:

- O PixelLab exporta quadros de 64×64, com 200 ms por quadro, uma tag por animação e direção, e uma paleta de 16 cores.
- O personagem tem cerca de 18×33 px parado. Juntando todos os quadros (arremessar, receber, correr), ele ocupa 34×38 px. Ele não cabe em 32×32 sem reamostrar a arte.
- `pixel-art/jpgma/build.py` junta duas exportações numa folha de células de 48×48 (recorte fixo, sem reamostrar): `rotacao` (1 linha) e `andar`, `correr`, `arremessar`, `comemorar` e `receber` (8 linhas cada). O `receber` vem do `Taking_Punch`: quem recebe a carta é atingido por ela, em vez de pegá-la. O resultado tem 432×1968 px (312 quadros, 18 cores) e 139 KB como PNG RGBA; o mesmo PNG com paleta indexada tem 45 KB.

## Alternativas consideradas

- **Manter a folha de lado** e só melhorar o deslizamento vertical. Nada muda no código, mas a arte de cima não serve.
- **8 direções em 32×32**, reduzindo a arte do PixelLab. A cena e o limite de importação mudam pouco, mas a redução estraga a arte e exige limpeza à mão.
- **8 direções em 48×48**, sem reamostrar. A arte fica intacta; a folha fica bem maior.
- **5 direções e espelho** (sul, sudeste, leste, nordeste e norte; o lado oeste espelha o leste), como na folha atual. A folha fica quase 40% menor, mas personagens assimétricos (cabelo de lado, fone, garrafa numa mão) ficam errados.

## Decisão proposta

1. **Célula de 48×48, 8 direções, sem espelho.** A ordem das direções é `sul`, `sudeste`, `leste`, `nordeste`, `norte`, `noroeste`, `oeste`, `sudoeste`.
2. **Âncora:** o ponto entre os pés fica em (24, y) e vale em todos os quadros. Cada quadro é recortado com o mesmo deslocamento, para manter o registro do PixelLab. No `jpgma`, y = 44.
3. **Layout:** linha 0 `rotacao` (uma direção por coluna); depois cada animação com 8 linhas, uma por direção, e um quadro por coluna. Os quadros, os tempos e a ordem das linhas ficam fixos na especificação, como hoje.
4. **Animações:** as do Jogo continuam as mesmas (`gamificacao.md` §4.2). Enquanto não houver arte própria, `treinando` usa `correr`, `comemorando` usa `comemorar`, e as que faltam (`parado`, `alongar`, `olhar`, `aceitar`, `recusar`, `provocar`) caem para `rotacao`. Os prompts para gerá-las ficam em `pixel-art/jpgma/prompts-pixellab.md`.
5. **Direção na cena:** o avatar escolhe a direção mais próxima do vetor do movimento (ao andar) ou do alvo (ao arremessar, provocar, receber). Parado na elipse, ele olha para o centro. O `scaleX(-1)` sai.
6. **Retrato:** um recorte da cabeça e dos ombros de `rotacao` `sul`, no lugar da linha `retrato`.

## Pontos em aberto

- **Limite de importação:** 256×384 e 64 KB não servem. A folha do `jpgma` tem 139 KB (RGBA) só com 6 animações; com as do Jogo, ela deve passar de 250 KB. Opções: aceitar PNG com paleta indexada (45 KB hoje, o navegador regrava assim antes de enviar), subir o limite, reduzir as linhas obrigatórias, ou aceitar 5 direções com espelho para folhas importadas.
- **Folhas padrão:** as 6 folhas do rig precisam de personagens novos em 8 direções, gerados no PixelLab e dedicados ao domínio público (CC0). Eles saem de um personagem base neutro (prompts em `pixel-art/base-masculino/prompts-pixellab.md`). O rig de 16 px (`pixel-art/avatar-base/`) e o editor por peças (`avatar-kit.md` §5) deixam de valer.
- **Folhas importadas antigas:** converter (impossível sem redesenho), aceitar os dois formatos por um tempo, ou redefinir para o padrão com aviso.
- **`receber` como impacto:** hoje a carta é pega com as mãos à frente do peito no segundo quadro (`avatar-formato.md` §3), e a encenação só começa o `receber` quando o voo termina (`useEncenacoes.ts`). Com o `Taking_Punch`, o impacto fica no terceiro quadro (400 ms depois do início, a 200 ms por quadro). O `receber` precisa começar 2 quadros antes do fim do voo, e a especificação passa a dizer "a carta atinge o peito no terceiro quadro". Quem recebe também precisa estar virado para quem arremessa.
- **Pose de guarda:** o `Taking_Punch` começa e termina com os punhos levantados, não na pose de `rotacao`. Na cena, há um salto de pose na entrada e na saída. Opções: aceitar o salto, ou gerar de novo a reação a partir da pose parada (prompt em `pixel-art/jpgma/prompts-pixellab.md`). O tom também muda: era "pegar" um convite, e agora é "levar uma cartada". Isso combina com o humor das Provocações, mas vale para toda carta, inclusive para as Respostas.
- **Tempos:** as animações do PixelLab têm 200 ms por quadro e outras quantidades de quadros (por exemplo, `receber` tem 6 × 200 ms = 1,2 s, contra 4 × 100 ms = 0,4 s hoje; `arremessar` tem 7 × 200 ms, e a carta sai em outro quadro). A especificação fixa os tempos, então eles precisam ser revistos linha por linha, junto com as encenações.
- **Primeiro quadro de `andar`:** nas direções nordeste, oeste e sudoeste, o primeiro quadro do `Walking` é diferente da `rotacao`. Começar a andar a partir da pose parada dá um pequeno salto nessas direções.
- **Tamanho na tela:** a cena mostra o quadro de 32 em 2× ou 3×. A célula de 48 em 2× ocupa 96 px; talvez a elipse precise de menos avatares, ou de escala 1× em telas estreitas.

## Andamento

**2026-11-08 — teste com a folha do `jpgma` para todos.** A decisão continua proposta. Para testar o formato de ponta a ponta na cena:

- A folha do `jpgma` é a única folha padrão (`Wrexercise/webapp/public/avatar-jpgma.png`, PNG indexado de 54 KB, gerado por `build.py`). As 6 folhas de lado saíram do bundle.
- **A importação está pausada.** Todos usam a folha padrão, inclusive quem importou uma folha. O servidor, a validação e os arquivos guardados não mudam, e `/perfil/avatar` mostra um aviso no lugar do envio.
- O `parado` (respiração, 4 quadros × 200 ms) é desenhado à mão no `build.py`, sobre a `rotacao`: os ombros sobem 1 px, depois a cabeça sobe junto, depois só os ombros. As outras animações que faltam (`alongar`, `olhar`, `aceitar`, `recusar`, `provocar`) usam `parado`, e as variações de `parado` ficam desligadas.
- O `retrato` é um recorte de 24 × 24 px, em (12, 10), da `rotacao` `sul`.
- A carta sai da mão no quarto quadro de `arremessar`, e o `receber` começa 2 quadros antes do fim do voo: a carta atinge o peito no terceiro quadro, e quem recebe fica virado para quem arremessou.
- **Direção de quem está parado:** virado para o centro só na horizontal, e sempre de frente para quem vê a tela. Se virasse para o centro de verdade, quem fica embaixo da elipse (a própria pessoa) apareceria de costas.
- O formato está em `avatar-formato.md` §6.

## Impacto, se aprovada

- `avatar-formato.md`, `gamificacao.md` §3.1, §3.3, §4 e `avatar-kit.md` reescritos para o formato novo.
- Frontend: `animacoes.ts` com direções, `SpriteAvatar` sem espelho, `CenaGrupo` e `useEncenacoes` escolhendo a direção, `Avatar` com o recorte do retrato, validação da folha no navegador.
- Backend: `ErrosJogo.cs` e a validação do PNG com as dimensões e o limite novos.
- Bundle: 6 folhas novas no pré-cache do Service Worker, maiores que as atuais (hoje cerca de 47 KB no total).
- `funcionalidades.md` atualizado na alteração que mudar o comportamento.
