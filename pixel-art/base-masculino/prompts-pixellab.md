# Prompts do PixelLab — personagem base masculino

> Prompts para gerar no PixelLab um personagem masculino **neutro**, sem traços marcantes, que serve de modelo para as folhas padrão em 8 direções (`docs/decisoes/0039-avatar-em-8-direcoes.md`, ponto "Folhas padrão"). Os personagens padrão saem dele por edição (cabelo, pele, roupa, acessórios), e todos mantêm o mesmo corpo, o mesmo tamanho e as mesmas animações. O formato segue o do `jpgma` (`../jpgma/prompts-pixellab.md`), para que o mesmo recorte de 48×48 e a mesma âncora valham.

## 1. Identidade visual

O Jogo é um **fliperama urbano onde a turma se encontra** (`docs/gamificacao.md` §1.2 e §1.3): pixel art contemporânea, estilo de vida urbano, roupa de academia e streetwear, numa quadra de asfalto vista de cima. O tom é leve e bem-humorado; as cartas são bilhetes de provocação entre amigos.

O que isso exige do personagem:

- **Pessoa comum, de hoje.** Nada de fantasia, medieval, armadura, capa, arma, magia ou pose de luta. Toda reação é cômica, nunca violenta.
- **Leitura sobre asfalto escuro.** O chão da cena é Asphalt `#222633`, com uma rampa de cinzas-azulados. Roupa preta ou azul-marinho some no chão: o base usa uma camiseta clara e uma calça cinza média, com contraste claro contra o contorno.
- **Cores contidas.** Os acentos da marca (Coral `#FF684A`, Electric Violet `#7957E8`, Signal Yellow `#FFD34E`, Lime `#A8D978`) ficam para os personagens derivados, um ou dois por personagem (`docs/avatar-kit.md` §2). O base não usa nenhum.
- **Simetria.** Nada de um lado só (franja de lado, fone, relógio, garrafa numa mão). Assim as 8 direções ficam coerentes e o base serve de ponto de partida para qualquer variação.
- **Regiões de cor separadas.** Pele, cabelo, camiseta, calça e tênis com rampas próprias e sem estampa. Isso facilita trocar uma região por outra cor ao derivar os personagens.

Cole este bloco no campo de estilo (ou no fim da descrição) em toda geração, inclusive nas animações:

```text
Style: contemporary urban pixel art for a cozy, playful fitness game. Everyday modern people
in gym clothes and streetwear, seen in low top-down view on a dark blue-grey asphalt court.
Clean readable silhouette, single-color near-black outline, simple soft shading with 3 tones
per material, medium detail, muted natural colors with good contrast against a dark floor.
No fantasy, no medieval, no armor, no weapons, no magic, no combat poses. Friendly and comic tone.
```

## 2. Personagem

### Prompt

```text
Base male character for a fitness game, a neutral template with no distinguishing features.
Young adult man of average height and average build, slightly athletic but not muscular.
Chibi proportions: large head about one third of the body height, short torso, short legs,
compact rounded hands, simple shoes. Symmetrical design, nothing on one side only.

Face: friendly neutral expression, small dark eyes with light grey whites, short simple
eyebrows, tiny nose shown by shading only, mouth closed or a 1-pixel neutral line.
Clean-shaven, no beard, no mustache, no glasses, no freckles, no scars, no makeup.

Hair: short, neat, close-cropped dark brown hair, even length all around, natural hairline,
no fringe, no part, no spikes, no tuft. The hair hugs the head shape so other hairstyles
can be drawn over it later.

Skin: medium warm skin tone, a mid value between light and dark, with one shadow tone and
one highlight tone.

Clothes: plain light heather grey crew-neck t-shirt with short sleeves, fitted but not tight,
no logo, no print, no stripes. Plain mid slate grey jogger pants, straight and tapered at the
ankle, no stripes, no pockets drawn. Plain white low-top sneakers with light grey soles,
no colored details. No accessories: no cap, no headphones, no watch, no bag, no bottle.

Pose: relaxed neutral standing pose, facing the viewer, weight on both feet, feet slightly
apart under the hips, arms relaxed at the sides with a small gap from the torso so the
silhouette reads clearly.
```

Depois do prompt, acrescente o bloco de estilo de §1.

### Configuração

Os valores abaixo reproduzem as exportações do `jpgma`, para que o build use o mesmo recorte (x 8..55 e y 1..48 do quadro de 64) e a mesma âncora (24, 44) na célula de 48×48:

| Opção | Valor |
|---|---|
| Tela | 64×64 |
| Tamanho do personagem | cerca de 18×33 px parado, centrado na horizontal, pés perto da linha 45 |
| Vista | *low top-down* (de cima e um pouco de lado) |
| Direções | 8 |
| Proporções, se houver a opção | *chibi* |
| Contorno | *single color black outline* (quase preto, como o `#0e071b` do `jpgma`) |
| Sombreamento | *basic shading* |
| Detalhe | *medium detail* |
| Fundo | transparente, sem sombra no chão, sem antisserrilhado |

### Paleta sugerida

Se o PixelLab aceitar uma paleta ou cores de referência, use estas (até 16, como nas exportações). Os valores são um ponto de partida; o que importa é a separação das regiões e o contraste com o chão.

| Região | Sombra | Base | Luz |
|---|---|---|---|
| Contorno | `#0e071b` | | |
| Pele | `#8a523e` | `#c26c4d` | `#e0855a` |
| Cabelo | `#1c1215` | `#392627` | `#493334` |
| Camiseta | `#8e93a3` | `#b9bdc8` | `#dcdfe6` |
| Calça | `#363c52` | `#4c5470` | `#646d8c` |
| Tênis | `#b9bdc8` (sola) | `#f5f1e8` | |
| Olhos | `#0e071b` (pupila) | `#dcd1d9` (branco) | |

A pele e o cabelo vêm das rampas do `jpgma`. A camiseta e a calça seguem a rampa de cinzas-azulados do chão, mas de 2 a 3 tons acima de Asphalt `#222633`. O branco do tênis é o Chalk `#F5F1E8` das linhas da quadra.

### `rotations` prontas

`build.py` recorta as 8 direções de `fonte/base.png` (uma imagem gerada, em escala grande) e as reduz para o tamanho do `jpgma`: 33 px de altura, os pés na linha 45 e centrado num quadro de 64×64, com contorno de 1 px e a paleta fixa `PALETA` do script. Os acertos à mão de olhos e sobrancelhas ficam em `RETOQUES`, no próprio script. Use `saida/rotations/<direcao>.png` (nome sem hífen, por exemplo `northeast.png`) como referência no PixelLab, ou importe as 8 como as rotações do personagem. `tira.py` monta as 8 de `fonte/<direcao>.png` (iguais às de `saida/rotations/`) em `saida/rotations-tira.png`, numa linha em tamanho real, em `saida/referencia-gemini.png`, a referência da `rotations` no Gemini, e em `saida/referencia-gemini/<direcao>.png`, a referência de cada direção para as animações no Gemini.

### Conferência da `rotations`

Antes de gerar as animações, confira as 8 direções da `rotations`:

- O personagem tem a mesma altura e os pés na mesma linha em todas as direções.
- Norte mostra só cabelo e nuca, sem rosto; sul mostra o rosto inteiro.
- Leste e oeste são o espelho um do outro (o personagem é simétrico).
- Nenhum detalhe extra apareceu (logo, listra, barba, acessório).
- A camiseta e a calça se distinguem do chão: veja a imagem sobre `#222633`.

Se algo falhar, gere de novo antes de seguir. Toda animação parte desta `rotations`.

## 3. Como gerar as animações

Toda animação parte **só** das imagens de `saida/rotations/` que o `build.py` já gerou. Nada de geração do zero, de folha com várias direções ou de referência em outro tamanho: sem um quadro de partida do mesmo tamanho, o modelo redesenha o personagem a cada quadro, e os quadros saem com altura, largura e proporções diferentes.

- **Uma direção por vez:** *animate with text* (ou um modelo pronto, ver abaixo) com `saida/rotations/<direcao>.png` como imagem de referência, na tela de 64×64, igual à da imagem. Ordem: `south`, `south-east`, `east`, `north-east`, `north`, `north-west`, `west`, `south-west`. Arquivos: `south.png`, `southeast.png`, `east.png`, `northeast.png`, `north.png`, `northwest.png`, `west.png`, `southwest.png`.
- **Prompt:** o bloco de âncora abaixo, com `<direction>` trocado pela direção, seguido do prompt da ação em §4. Os prompts de §4 descrevem cada quadro como uma mudança de poucos pixels em relação à pose parada, para que o modelo mexa só nas partes que a ação move.
- **Modelo do PixelLab:** quando houver um modelo pronto equivalente (andar, correr, respirar), prefira o modelo, sempre com a mesma imagem de referência. Se o modelo sair estranho, volte ao texto.
- **No lugar:** todas as animações ficam no centro do quadro, inclusive `andar` e `correr`: o deslocamento na cena é feito pelo Jogo. Só `andar`, `correr`, `treinando` e `comemorar` tiram os pés da linha 45, e só pelos pixels que o prompt diz.
- **Nada de objetos:** a carta voa em CSS na cena (`docs/gamificacao.md` §3.3). Não desenhe carta, bola, peso ou garrafa.
- **Saída:** 200 ms por quadro (o padrão do PixelLab), fundo transparente, sem sombra no chão.
- **Paleta:** a mesma de `rotations` (`PALETA` no `build.py`). Cores novas vão para a mais próxima no build; evite cores que o personagem não tem.
- **Quadros:** os da tabela de §4. Se o PixelLab gerar outra quantidade, tudo bem: o build lê a quantidade das tags.
- **Exportar:** `.aseprite` com uma camada e tags `Acao-direcao` (por exemplo, `Idle_Breathing-south`), como em `../jpgma/fonte/`. Salve em `fonte/base-masculino-a.aseprite`, `-b` e assim por diante.

### Bloco de âncora

Cole antes do prompt da ação, em toda animação:

```text
Animate the character in the reference image, facing <direction>. The reference image is the
standing pose that the animation starts from and returns to. Keep the facing direction of the
reference in every frame. Keep the 64x64 canvas, the same pixel grid and the same scale in
every frame: the character is 33 pixels tall in the standing pose, centered horizontally,
with the feet on row 45, exactly as in the reference. Do not redraw, resize, restyle or
recolor the character: same head size, same body proportions, same outline, same 3-tone
shading and the same colors as the reference. Change only the body parts that the action
moves, by the number of pixels given for each frame. Everything not mentioned stays exactly
as in the reference. The animation stays in place, no travel across the canvas.
Transparent background, no floor shadow, no anti-aliasing, no new colors.
```

Depois do prompt da ação, acrescente o bloco de estilo de §1.

### Conferência de cada animação

Antes de exportar, compare cada quadro com a imagem de `rotations` da mesma direção, um sobre o outro:

- A cabeça, o tronco e as pernas têm o mesmo tamanho da referência; só mudam de lugar.
- Fora dos movimentos que o prompt descreve, a altura é 33 px e os pés ficam na linha 45.
- O personagem não andou no quadro e não virou para outra direção.
- Nenhuma cor nova, nenhum objeto, nenhuma sombra no chão.

Se um quadro falhar, gere a direção de novo. Não corrija tamanho ou proporção à mão: isso só esconde o problema nas outras direções.

## 4. Animações

| Nome na folha | Tag sugerida | Uso no Jogo | Quadros | Repetição |
|---|---|---|---:|---|
| `rotacao` | `rotations` | pose parada em cada direção | 1 | estático |
| `parado` | `Idle_Breathing` | pose padrão | 4 | sim |
| `andar` | `Walking` | ir ao centro e voltar | 8 | sim |
| `correr` | `Running` | deslocamento rápido | 8 | sim |
| `treinando` | `Jogging_In_Place` | atividade aguardando verificação | 6 | sim |
| `alongar` | `Stretching` | variação de `parado` | 4 | uma vez |
| `olhar` | `Looking_Around` | variação de `parado` | 4 | uma vez |
| `arremessar` | `Throw_Card` | jogar uma carta | 6 | uma vez; a carta sai no 4º quadro |
| `receber` | `Hit_By_Card` | ser atingido pela carta | 6 | uma vez; impacto no 3º quadro |
| `aceitar` | `Fist_Pump` | aceitar a carta | 4 | uma vez |
| `recusar` | `Shrug` | recusar a carta | 3 | uma vez |
| `provocar` | `Taunting` | encontro dos rivais | 4 | uma vez |
| `comemorar` | `Cheering` | meta cumprida, Jogada concluída, Missão, Enfeite | 8 | sim |

`rotacao` não é gerada: é a própria imagem de `saida/rotations/`.

### `parado` — Idle_Breathing

```text
Action: calm idle breathing loop. 4 frames:
1. Exactly the reference pose.
2. The head, the shoulders and the chest move 1 pixel up. The arms follow the shoulders.
   The legs and the feet do not change.
3. Same as frame 2.
4. Exactly the reference pose.
Seamless loop: frame 4 leads back to frame 1.
```

### `andar` — Walking

```text
Action: casual relaxed walk in place, at a normal pace. Small steps: a foot moves at most
3 pixels from its standing position. Each arm swings at most 2 pixels, opposite to the leg
on the same side. 8 frames:
1. The reference pose: legs together, arms at the sides.
2. The right leg moves forward, the left arm swings forward, the right arm swings back.
   The body moves 1 pixel up.
3. Widest step: the right foot is forward, the left foot is back, both feet on row 45.
   The arms are at the end of their swing. The body is at the reference height.
4. The weight moves onto the right foot, the legs start to come together, the arms move
   back toward the sides. The body moves 1 pixel down.
5. Legs together and arms at the sides, as in frame 1.
6. Same as frame 2, but with the left leg forward, the right arm forward, the left arm back.
7. Same as frame 3, but with the left foot forward.
8. Same as frame 4, but with the weight on the left foot.
Seamless loop: frame 8 leads back to frame 1.
```

### `correr` — Running

```text
Action: light athletic run in place, like a morning jog in the park, not a sprint. In every
frame the elbows are bent at about 90 degrees and the torso leans 1 pixel forward. A knee
lifts at most 3 pixels, and in the air the feet are at most 2 pixels above row 45. 8 frames:
1. The reference pose with the elbows bent and the torso leaning 1 pixel forward.
2. The right knee lifts forward, the left leg pushes back, the left arm swings forward,
   the right arm swings back. The body moves 1 pixel up.
3. Both feet off the ground, at most 2 pixels above row 45, widest stride.
   The body moves 2 pixels up from the reference.
4. The right foot lands on row 45, the left leg starts to come forward.
   The body moves 1 pixel down from the reference.
5. Same as frame 1.
6. Same as frame 2, but with the left knee forward, the right arm forward, the left arm back.
7. Same as frame 3, but with the left leg forward.
8. Same as frame 4, but landing on the left foot.
Seamless loop: frame 8 leads back to frame 1.
```

### `treinando` — Jogging_In_Place

```text
Action: light jogging in place, warming up before a workout. In every frame the elbows are
bent at about 90 degrees. The feet stay over the same spot, no forward travel at all.
6 frames:
1. The reference pose with the elbows bent. Both feet on row 45.
2. The right knee starts to lift: the right foot is 1 pixel above row 45. The left arm
   swings 1 pixel forward. The body moves 1 pixel up.
3. The right knee is at its highest: the right foot is 3 pixels above row 45. The left arm
   is 2 pixels forward. The body stays 1 pixel up.
4. Same as frame 1.
5. Same as frame 2, but with the left knee and the right arm.
6. Same as frame 3, but with the left knee and the right arm.
Seamless loop: frame 6 leads back to frame 1.
```

### `alongar` — Stretching

```text
Action: quick casual stretch while standing. The feet do not move. 4 frames:
1. Both arms rise to shoulder height. The rest is the reference pose.
2. Both arms reach straight up over the head, with the hands together. The body stretches
   1 pixel taller.
3. Same arms as frame 2. The upper body leans 1 to 2 pixels to one side, from the waist.
4. The upper body is straight again and the arms come down to shoulder height.
After frame 4 the character returns to the reference pose.
```

### `olhar` — Looking_Around

```text
Action: looks around while standing, a little curious. Only the head moves; the body, the
arms and the feet stay exactly as in the reference. The head keeps its size. 4 frames:
1. Exactly the reference pose.
2. The head turns to one side: the face features move 1 pixel to that side.
3. The head turns to the other side: the face features move 1 pixel to the other side
   of the reference position.
4. Exactly the reference pose.
```

### `arremessar` — Throw_Card

```text
Action: tosses a small playing card overhand toward someone in front, playful and confident,
like flicking a note to a friend. Only the right arm and the upper body move. The feet stay
on row 45 and do not move. Do not draw the card. 6 frames:
1. Exactly the reference pose.
2. Wind-up: the right arm bends and lifts, the hand goes up next to the right shoulder.
   The upper body turns a little toward the right arm.
3. Full wind-up: the right hand is behind and above the right shoulder. The upper body leans
   1 pixel back, the weight is on the back foot.
4. Release: the right arm snaps forward, straight, at shoulder height, pointing at the
   person in front. The upper body leans 1 pixel forward.
5. Follow-through: the right arm comes down across the front of the body.
   The upper body is upright again.
6. The right arm returns toward the side, almost the reference pose.
```

### `receber` — Hit_By_Card

```text
Action: gets hit in the chest by a small card thrown at them, comic surprise, not pain.
The feet stay on row 45 and do not move. Do not draw the card. 6 frames:
1. Exactly the reference pose.
2. Notices it coming: the head moves 1 pixel up, the eyes open wide, the arms lift 1 pixel
   away from the body.
3. Impact: the upper body jolts 1 to 2 pixels back, the head tilts back, both arms flick
   outward 2 pixels from the torso.
4. Recovers: the upper body comes back to its place, the arms come back to the sides.
5. A small shake of the head: the head moves 1 pixel to one side. The body is as in
   the reference.
6. Exactly the reference pose.
```

### `aceitar` — Fist_Pump

```text
Action: accepts a challenge with confidence. The feet stay on row 45 and do not move.
Only the raised arm may go above the 33-pixel standing height. 4 frames:
1. Exactly the reference pose.
2. Short crouch: the knees bend and the body moves 2 pixels down. Both hands close into
   fists at the waist.
3. The body comes back to the reference height. The right arm goes straight up above the
   head with the fist closed.
4. Same as frame 3, with a small nod: the head moves 1 pixel down.
The animation ends on frame 4, with the fist up.
```

### `recusar` — Shrug

```text
Action: shrugs to decline, relaxed and good-humored, no hard feelings. The feet stay on
row 45 and do not move. 3 frames:
1. Exactly the reference pose.
2. Both shoulders lift 1 pixel. The forearms come halfway up, with the elbows next to
   the torso.
3. The shoulders stay 1 pixel up. The forearms point forward at waist height, with the open
   palms facing up. The head tilts 1 pixel to one side.
The animation ends on frame 3, at the top of the shrug.
```

### `provocar` — Taunting

```text
Action: friendly taunt to a rival standing in front of them, like trash talk between friends
before a game. Not aggressive, no fists, no fighting stance. The feet stay on row 45 and do
not move. 4 frames:
1. Exactly the reference pose.
2. Points forward: the right arm extends straight at shoulder height, toward the front.
   The upper body leans 1 pixel forward.
3. Quick bounce: the right hand comes back to the chest, the knees bend and the body moves
   1 pixel down.
4. Points again as in frame 2, with a confident grin.
```

### `comemorar` — Cheering

```text
Action: celebrates a goal reached, joyful, like finishing a workout streak. The jump is
small: the feet go at most 3 pixels above row 45 and land back on the same spot.
8 frames:
1. Exactly the reference pose.
2. Crouch: the knees bend, the body moves 2 pixels down, both fists come up to the chest.
3. Jump: both feet leave the ground, 2 pixels above row 45. Both fists go straight up
   above the head.
4. Top of the jump: the feet are 3 pixels above row 45, the fists are up, the mouth is a
   happy open smile.
5. Landing: both feet back on row 45, the body 1 pixel down, the fists still up.
6. The body returns to the reference height. Both fists pump down to the level of the head.
7. Both fists pump up again, straight above the head.
8. The fists come down to the shoulders. The legs are as in the reference.
Seamless loop: frame 8 leads back to frame 1.
```

## 5. Derivar personagens

Os personagens padrão saem da `rotations` do base, com a edição de personagem do PixelLab (ou uma geração com a `rotations` como referência), trocando só uma ou duas regiões por vez. Depois, as animações são geradas de novo para cada personagem, com os mesmos prompts de §4. Modelo de prompt:

```text
Same character, same body, same size, same pose and same proportions as the reference.
Change only: <short curly black hair> and <a coral #FF684A tank top>. Keep everything else
identical: face, skin, pants, sneakers, outline and shading style.
```

Regras para os derivados, vindas de `docs/gamificacao.md` §1.3 e de `docs/avatar-kit.md` §2:

- Um ou dois acentos da marca por personagem, na roupa ou num acessório (boné, fone, munhequeira).
- Variedade de tons de pele, de cabelos e de corpos entre os personagens padrão.
- Acessórios de um lado só são permitidos nos derivados, porque a folha não usa espelho.
- As folhas padrão são dedicadas ao domínio público (CC0), então nada de marca, logo ou personagem de terceiros.

## 6. Retrato

O `retrato` (o `Avatar` fora da cena) não precisa de geração: ele sai de um recorte da cabeça e dos ombros de `rotations-south`, como no `jpgma` (ADR 0039, item 6).
