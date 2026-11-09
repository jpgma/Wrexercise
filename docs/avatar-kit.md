# Avatar base e kit de camadas — Wrexercise

> **Fora do bundle:** as 6 folhas padrão de lado saíram de `Wrexercise/webapp/public/`. A folha padrão agora é a do `jpgma`, em 8 direções (`decisoes/0039-avatar-em-8-direcoes.md`, Andamento). O rig continua aqui como referência.
>
> Como são feitas as folhas padrão de avatar (`gamificacao.md` §4.4) e como trocar cor de pele, cabelo, roupa e acessórios. O formato da folha final está em `avatar-formato.md`. Este documento também descreve o editor de avatar por peças, ainda sem fase (§7).

## 1. Visão geral

A arte é a do personagem do *2D Platformer Character Template* (CC0), em 16 × 16 px, com as mesmas formas, o mesmo contorno grosso e os mesmos quadros de animação. A folha final amplia cada quadro 2× para as células de 32 × 32.

```text
pixel-art/avatar-base/
├── referencia/template-16px.aseprite  # personagem de origem (CC0)
├── rig.aseprite        # o rig: fonte da verdade, editado no Aseprite
├── rig.py              # regiões, camadas, animações e as regras template → rig
├── criar_rig.py        # cria rig.aseprite a partir do template (uma vez)
├── compor.py           # kit + personagem → folha 256×384
├── build.py            # rig.aseprite → kit/ → saida/
├── aseprite_mcp.py     # cliente do servidor MCP do pixel-plugin
├── personagens/*.json  # um arquivo por personagem padrão
├── kit/                # gerado: uma folha de IDs por camada + kit.json
├── saida/              # gerado: as folhas padrão
└── preview/            # gerado, fora do git: ampliações e GIFs
```

Todo acesso a `.aseprite` passa pelo servidor MCP do `pixel-plugin` (`AGENTS.md`), configurado pelo `/pixel-setup` em `~/.config/pixel-mcp/config.json`.

```bash
cd pixel-art/avatar-base
python build.py                    # exige Pillow e o pixel-plugin configurado
python criar_rig.py --substituir   # só para recriar o rig do zero a partir do template
```

O `build.py` só relê o rig quando `rig.aseprite` muda (cerca de 2 minutos). Sem mudança no rig, ele só recompõe as folhas. Nunca edite os PNGs gerados.

As folhas de `saida/` entram no bundle do frontend na G1, junto do `LICENSE` CC0. Elas não podem ir para uma subpasta de `webapp/public/`, porque o plugin do Service Worker em `vite.config.ts` lê só arquivos na raiz de `public/` e põe todos no pré-cache.

A cópia é manual e acontece sempre que a arte muda. Cada folha vira `webapp/public/avatar-<nome>.png` (`bia`, `caio`, `davi`, `jonas`, `lara`, `nina`). O chão da praça, gerado por `pixel-art/praca/build.py`, vira `webapp/public/praca.png` (mestre 1× de 180×230, mostrado em 2×). O texto CC0 das duas artes fica em `webapp/public/LICENSE-arte.txt`. O conjunto soma cerca de 47 KB no pré-cache. A paleta `wrexercise` (§1.3 de `gamificacao.md`) fica na skill `pixel-art-studio`, em `references/learned/palettes.json`.

## 2. O que mudar e onde

| Para mudar | Onde |
|---|---|
| Pele, cabelo, roupa, tênis, meia e acessórios de um personagem | `personagens/<nome>.json` |
| Personagem novo | um JSON novo em `personagens/` |
| Desenho de qualquer peça em qualquer quadro (corpo, cabelo, boné, fone...) | `rig.aseprite`, no Aseprite, com a paleta de IDs do arquivo |
| Tons de pele e cores de cabelo disponíveis | `PELES` e `CABELOS_COR` em `compor.py` |
| O que cada roupa cobre (camiseta, regata, short...) | `BLUSAS`, `CALCAS`, `ACESSORIOS` e `MEIA` em `compor.py` |
| De que quadros do template sai cada linha | `QUADROS` em `rig.py`, e depois `criar_rig.py --substituir` |

### 2.1. Personagem

```json
{
  "nome": "Caio",
  "corpo": "masculino",
  "pele": "clara",
  "cabelo": { "estilo": "curto", "cor": "castanho" },
  "blusa": { "estilo": "camiseta", "cor": "#3f73d9" },
  "calca": { "estilo": "short", "cor": "#2c2c38" },
  "tenis": { "cor": "#f0f0f4", "sola": "#8d8d9c" },
  "meia": "#f4f4f8",
  "acessorios": { "fone": "#2a2a33" }
}
```

| Campo | Valores |
|---|---|
| `corpo` | `masculino`, `feminino` |
| `pele` | `clara`, `morena`, `negra` |
| `cabelo.estilo` | `raspado`, `curto` (a faixa escura do template), `crespo`, `rabo`, `coque`, `careca` |
| `cabelo.cor` | `preto` (a cor do template), `castanho`, `loiro`, `ruivo`, `grisalho` |
| `blusa.estilo` | `camiseta`, `regata`, `moletom` (com capuz abaixado) |
| `calca.estilo` | `short`, `legging`, `jogger` |
| `tenis.cor`, `meia`, `blusa.cor`, `calca.cor` | qualquer cor `#rrggbb`; a sombra e a luz saem de uma rampa com variação de matiz |
| `acessorios` | `bone`, `fone`, `munhequeira`, `garrafa`, cada um com uma cor |

Nas folhas padrão, prefira os acentos da marca para roupa e acessórios (Coral `#FF684A`, Electric Violet `#7957E8`, Signal Yellow `#FFD34E`, Lime `#A8D978`, Asphalt `#222633`), um ou dois por personagem, como na identidade do Jogo (`gamificacao.md` §1.3). As folhas que já existem não precisam ser geradas de novo por causa disso.

`tenis.sola` fica no JSON para uma sola futura; em 16 px o pé tem uma linha só.

### 2.2. Personagens padrão

| Arquivo | Corpo | Pele | Cabelo | Roupa | Acessórios |
|---|---|---|---|---|---|
| `caio` | masculino | clara | curto, castanho | camiseta, short, meia | fone |
| `bia` | feminino | clara | rabo, loiro | regata, legging | munhequeira |
| `davi` | masculino | morena | raspado, preto | regata, short, meia | munhequeira, garrafa |
| `lara` | feminino | morena | coque, castanho | camiseta, jogger | fone |
| `jonas` | masculino | negra | crespo, preto | moletom, jogger | — |
| `nina` | feminino | negra | rabo, preto | camiseta, legging | boné |

## 3. O rig

`rig.aseprite` tem quadros de 16 × 16, os 53 quadros na ordem das linhas de `avatar-formato.md` §3 e uma tag por linha. Cada pixel tem uma **cor de identificação**, que representa uma **região** e um **tom** (0 sombra, 1 base, 2 luz):

- **Regiões:** `pele`, `olho`, `tronco`, `quadril`, `braco`, `antebraco`, `punho`, `mao`, `coxa`, `canela`, `tornozelo`, `pe`, `cabelo`, `bone`, `bone_aba`, `fone`, `fone_concha`, `capuz`, `garrafa`, `garrafa_tampa`.
- A paleta do arquivo tem só as cores de identificação. Pintar com outra cor faz o `build.py` parar e apontar o pixel.
- A tabela `cores_id` de `kit/kit.json` traduz cada cor para `[região, tom]`.

**Camadas**, de trás para a frente, na ordem do template:

| Camada | Conteúdo | Origem no template |
|---|---|---|
| `braco-tras` | braço de trás, em sombra | `BackArm` |
| `garrafa` | garrafa na mão de trás | nova |
| `perna-tras` | perna de trás, em sombra | `BackLeg` |
| `tronco-masculino`, `tronco-feminino` | tronco e quadril | `Body`; o feminino tem um pixel a mais no peito e um a menos na cintura |
| `capuz` | capuz abaixado do moletom | novo |
| `perna-frente` | perna da frente | `FrontLeg` |
| `braco-frente` | braço da frente | `FrontArm` |
| `cabeca` | cabeça | `Head` |
| `rosto-masculino`, `rosto-feminino` | olho e bochecha; o feminino tem um cílio | `Face` |
| `cabelo-{estilo}` | um estilo de cabelo | a faixa escura do contorno acima da cabeça |
| `bone`, `fone` | acessórios de cabeça | novos |

O contorno não é camada: a composição o desenha (§4).

**Roupa não é camada.** A camiseta, por exemplo, é só a regra "`tronco` e `braco` usam o material `blusa`". A regata deixa `braco` na pele, e o short deixa `canela` na pele. Por isso, todas as roupas valem em todos os quadros sem desenho extra.

### 3.1. Do template ao rig

`criar_rig.py` lê o template pelo pixel-plugin e monta cada quadro com as regras de `rig.py`:

- As peças do corpo vêm do quadro do template indicado em `QUADROS`. Uma peça pode vir de outro quadro (por exemplo, o braço levantado do WallSlide), alinhada pelo canto do tronco, e pode ser deslocada.
- Os membros são divididos em regiões ao longo do eixo do ombro (ou do quadril) até a ponta: no braço, `braco`, `antebraco`, `punho` e `mao`; na perna, `coxa`, `canela`, `tornozelo` (penúltima linha) e `pe` (última linha).
- Cabelo, boné, fone e capuz são desenhos fixos relativos ao canto da cabeça. A garrafa segue a mão de trás.

| Linha | Quadros do template |
|---|---|
| `retrato` | JumpTurn 1, de frente |
| `parado` | Idle; nos dois últimos quadros, a parte de cima desce 1 px (respiração) e o olho fecha no último |
| `andar` | Walk 1–8 |
| `treinando` | Jog 1, 2, 4, 5, 6 e 8 |
| `comemorando` | Jump 1–5 e Idle |
| `alongar` | Idle com o braço levantado do WallSlide |
| `olhar` | Idle com o olho para a frente e para trás |
| `arremessar` | Crouch 1–2 e Dash 1–2 (a carta sai no terceiro quadro), e Idle |
| `receber` | Idle com os braços do Jump 2 à frente do peito |
| `aceitar` | Crouch 1 e Idle com o braço levantado do WallSlide |
| `recusar` | Idle com os braços erguidos 1 px (dar de ombros) |
| `provocar` | Idle com o braço esticado do Dash, subindo e descendo |

Depois de criado, o rig é a fonte da verdade. Recriá-lo com `--substituir` apaga as edições feitas no Aseprite.

## 4. Composição

`compor.py` segue estes passos, que o editor no navegador (§7) repete. Tudo acontece em 16 px por quadro:

1. Escolher as camadas: o corpo do tipo escolhido, o cabelo, as camadas de `CAMADAS_BLUSA` (o capuz do moletom) e as dos acessórios. Empilhar na ordem do rig.
2. Ao empilhar `braco-frente`, passar para o tom 0 os pixels de baixo que encostam no braço, pelos 4 vizinhos. Assim o braço não some na frente de uma blusa da mesma cor.
3. Desenhar o contorno grosso do template em `#0e071b`: em volta do corpo, primeiro os 4 vizinhos e depois os 8 vizinhos. É a mesma regra do contorno desenhado à mão no template.
4. Trocar cada região pelo material: `regioes_base`, depois a blusa, a calça, a meia e os acessórios. Um valor `[material, tom]` fixa o tom. Depois trocar `[material, tom]` pela cor da rampa do personagem.
5. Desenhar um contorno fino (4 vizinhos) em volta do cabelo e dos acessórios, onde eles saem do contorno grosso.
6. Ampliar 2× na célula de 32. O `retrato` amplia 3× um recorte de 10 × 10 em volta da cabeça.

O resultado é uma folha comum, validada como qualquer outra (`avatar-formato.md` §1).

## 5. Editor de avatar por peças (adiado)

O kit foi feito para permitir, no futuro, um editor em `/perfil/avatar` em que a pessoa monta o avatar escolhendo peças, sem desenhar uma folha.

- O bundle leva `kit/*.png` (camadas pequenas, de 128 × 192) e `kit.json`.
- O navegador compõe a folha num canvas com os passos de §4, e a prévia usa a mesma folha.
- **Recomendação:** guardar só as escolhas, como um JSON do formato de §2.1, numa coluna da tabela `avatares`. Como as peças e as cores vêm de listas fixas, não há imagem para moderar nem arquivo para guardar. Quem quiser mais liberdade continua importando a própria folha (`gamificacao.md` §4.3).
- As cores livres do JSON seriam trocadas por paletas fixas no editor, para manter o contraste com o contorno.
- Os testes de `compor.py` e da versão TypeScript usam os mesmos casos: o mesmo JSON precisa gerar os mesmos pixels.
- Precisa de um ADR na fase em que for feito, porque muda o que a tabela `avatares` guarda.

## 6. Aseprite e `pixel-plugin`

Arquivos do Aseprite (`.aseprite`, `.ase`) são trabalhados sempre com o plugin `pixel-plugin`, habilitado em `.claude/settings.json`. Ele exige o Aseprite instalado e configurado com `/pixel-setup`. A configuração fica em `~/.config/pixel-mcp/config.json`, com o caminho do Aseprite usando `/`. Os scripts de `pixel-art/` falam com o mesmo servidor por `pixel-art/avatar-base/aseprite_mcp.py`. Arte feita com a skill `pixel-art-studio` (sem Aseprite) segue `docs/avatar-formato.md` e `docs/gamificacao.md` §4.4.
