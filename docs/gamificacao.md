# Gamificação — Wrexercise

> **Status:** proposta refinada; as fases G1 (modo Jogo e cena), G2 (Energia), G3 (cartas), G4 (Respostas), G6 (avatar importado e Enfeites) e G7 (Rival da semana) estão implementadas, e a G5 (Missões) ainda não. A direção geral está em `decisoes/0023-gamificacao.md`, com as mudanças de `decisoes/0024-cartas-e-avatar-importado.md`, as regras de Energia e Jogadas de `decisoes/0033-regras-de-energia-e-jogadas.md`, a ordem dos bônus de `decisoes/0034-ordem-dos-bonus-de-energia.md`, os detalhes das cartas de `decisoes/0035-cartas-e-jogadas.md` as Respostas de `decisoes/0036-respostas.md` o avatar importado e os Enfeites de `decisoes/0037-avatar-importado-e-enfeites.md` o Rival da semana de `decisoes/0038-rival-da-semana.md` e a meta diária de `decisoes/0040-meta-diaria.md`. O formato da folha de sprites está em `avatar-formato.md`, e o avatar base e o kit de camadas estão em `avatar-kit.md`. Cada fase atualiza `funcionalidades.md` quando for entregue.

## 1. Visão

O modo **Jogo** é uma tela inicial alternativa, leve e bem-humorada, para quem gosta de um toque de jogo. Cada pessoa escolhe entre **App** (a tela inicial de hoje) e **Jogo**. Nada muda para quem continua no App.

No Jogo, os membros do grupo aparecem como avatares em pixel-art, numa praça. Os treinos verificados rendem **Energia**. A Energia compra **cartas**, que viram provocações amistosas entre colegas: objetivos extras e opcionais que, quando cumpridos, rendem Energia para os dois. Missões semanais e o Rival da semana completam a experiência.

### 1.1. Princípios

1. **A meta oficial vem primeiro.** O Jogo nunca altera a verificação, as metas, a classificação nem as sequências.
2. **Recompensa constância.** O único critério de desempenho é a duração relativa à meta do grupo, e só em cartas opcionais (+10 MIN e +20 MIN).
3. **Ninguém prejudica ninguém.** Nenhuma interação remove Energia de outra pessoa, invalida atividades ou dificulta a meta de outra pessoa.
4. **Simples e discreto.** Nada de missões diárias nem lembretes. A única aposta é a carta APOSTA, que só arrisca a Energia de quem a joga.
5. **Toda carta faz algo que não dá para fazer de graça.** Recusar uma carta é sempre grátis e não tem consequência.
6. **Tudo calculado no servidor**, a partir dos dados que já existem sempre que possível.
7. **A classificação continua igual.** A Energia não aparece no ranking e não existe ranking de Energia.

### 1.2. Direção criativa

Pixel-art contemporâneo com estilo de vida urbano: streetwear, tênis, roupa de academia, fones de ouvido, café, academia, praça com barra de calistenia.

Evitar fantasia, medieval, monstros, magia, armas, combate, classes e atributos de RPG. As cartas são bilhetes de provocação entre amigos, não feitiços.

O avatar é uma versão estilizada de uma pessoa real.

### 1.3. Identidade visual do Jogo

O Jogo é a parte mais expressiva da identidade Wrexercise (`decisoes/0027-identidade-visual-wrexercise.md`): um fliperama urbano onde a turma se encontra. As regras abaixo valem para as fases G1 a G7 e não mudam nenhuma mecânica.

- **Cena:** uma quadra ou praça urbana vista em ¾, no mesmo ângulo dos avatares, com asfalto, marcações tracejadas de quadra, um alambrado ou mureta, a barra de calistenia (§9.3) e no máximo um grafite discreto. Nada de grama de RPG, pedras, tochas ou castelos.
- **Paleta da pixel art:** os tons da marca guiam o cenário e as molduras: Asphalt `#222633` e uma rampa de cinzas-azulados para o chão, Chalk `#F5F1E8` para as linhas, Coral `#FF684A`, Electric Violet `#7957E8`, Signal Yellow `#FFD34E` e Lime `#A8D978` como acentos. Peles e cabelos continuam com as próprias rampas (`avatar-kit.md`). Salve a paleta na skill como `wrexercise` e use `Sprite(palette="wrexercise")`.
- **UI em volta da cena:** componentes comuns do aplicativo (tokens de `styles/index.css`). Violeta (`game`) marca o que é do Jogo: saldo de Energia, Mão, botão de jogar carta. Amarelo (`reward`) é recompensa e conquista; lime (`progress`), progresso das Missões e da meta.
- **Selos:** Missões, Rival e situação de Jogada usam os selos `badge-*` (caixa alta, contorno, sombra de adesivo). O Rival da semana ganha um selo com o número da posição, como placar.
- **Movimento:** só as animações das folhas (§4.2) e as encenações (§3.3). A UI em volta usa os mesmos `pop` e `burst` do aplicativo, uma vez, e respeita `prefers-reduced-motion`.

#### Cartas

- **Mesma moldura para todas** (§4.4): contorno grosso escuro, cantos levemente recortados e um número de preço no canto, como etiqueta de placar.
- **Hierarquia fixa:** nome em Anton e caixa alta (como no catálogo, §6.1); selo do tipo; ícone em pixel de 32×32; efeito em uma frase curta; preço em EN.
- **Provocação e Resposta:** a moldura e a hierarquia são as mesmas, e muda só o acento. Provocação usa coral, com uma faixa de "linhas de movimento" no topo. Resposta usa violeta, com a faixa em halftone. O selo do tipo diz o nome por extenso, para a diferença não depender só da cor.
- Nenhuma carta nova e nenhuma carta de defesa (§11). Os efeitos são os de §6.1.

## 2. Modos da tela inicial

| Modo | Tela inicial (`/grupos/:id/inicio`) |
|---|---|
| `App` | A tela atual, sem mudanças. |
| `Jogo` | A cena do grupo (§3). |

- O modo é uma preferência da conta e vale em todos os dispositivos.
- O padrão é `App`, inclusive para contas novas.
- No modo App, a tela inicial mostra **uma vez** um cartão convidando para experimentar o Jogo. A pessoa pode ativar ou fechar o cartão. A dispensa fica num cookie comum, como a do aviso de instalação.
- A troca de modo fica em Configurações.
- Todos acumulam Energia e têm avatar, mesmo no modo App.
- **Cartas só circulam entre pessoas no modo Jogo.** Ninguém envia uma carta para quem está no modo App. Se o destinatário passar para o modo App, as Jogadas abertas seguem o curso normal e expiram.

## 3. Cena do grupo

A tela Jogo mostra:

- a cena com o avatar de cada membro ativo do grupo;
- o seu progresso na semana (o mesmo anel da tela App, em versão compacta);
- logo abaixo da praça, a sua Mão de cartas com a Mesa e o saldo de Energia na primeira linha (a partir das fases G2 e G3);
- o aviso de atividades para avaliar, como hoje.

A tela não tem um botão próprio de "Registrar atividade": o registro já está na navegação inferior (celular) e na lateral (computador).

### 3.1. Layout

- Vista em ¾, a mesma dos avatares: a mureta do fundo, a porta e os prédios aparecem de frente, e o chão aparece achatado. O céu é transparente: o fundo do aplicativo faz o papel de céu.
- Os avatares ficam numa **elipse larga e baixa**, no centro do chão, sobre o oval pintado na praça. Quem fica na frente cobre parte de quem fica atrás. A folha é vista de cima, em 8 direções, sem espelho (`avatar-formato.md` §6). Parado, cada avatar olha para o centro da elipse: quem fica embaixo aparece de costas (`norte`), e quem fica em cima, de frente (`sul`). Nas encenações, ele olha para onde anda ou para o alvo.
- A ordem de desenho segue o y dos pés: quem está mais abaixo na tela fica na frente. Os objetos soltos no chão (barra, caixotes, floreiras) são camadas à parte, ordenadas pela base da mesma forma: quem passa atrás de um objeto some por trás dele.
- Escala inteira, com `image-rendering: pixelated`. O quadro de 32 px aparece em 2× (64 px). A praça tem 360×400 px e encolhe por inteiro em telas mais estreitas. Com 10 membros, a elipse tem raios de 148 px e 76 px; com menos, ela encolhe na mesma proporção, para que os vizinhos fiquem à mesma distância, até um mínimo de 60 px e 48 px. O raio vertical mínimo deixa quem fica atrás visível acima de quem fica na frente. A escala é sempre 2×, qualquer que seja o tamanho do grupo.
- O nome de cada avatar só aparece durante as encenações, para quem atua nelas; no Foco, o nome fica no painel. Os 3 primeiros da Classificacao do desafio em destaque têm sempre uma medalha embaixo do avatar (🥇, 🥈, 🥉), antes do nome quando ele aparece; só com o desafio em curso ou encerrado, e só para quem já tem atividade verificada no desafio.
- Os Enfeites da vitrine (§9.2) ficam **no chão**, no cantinho de cada dono, fora da elipse: atrás de quem está na metade de trás, ao lado de quem está na metade da frente. Quantos aparecem depende do tamanho do grupo: até 3 membros, 3 por pessoa; 4 ou 5, 2; 6 ou mais, 1. Eles são ordenados pela base, como os avatares e os objetos. Um Enfeite na frente de alguém nunca cobre a cabeça nem o tronco; atrás, ao menos metade dele aparece; dois Enfeites nunca dividem o mesmo chão; o caminho da porta, a barra, os caixotes e as floreiras ficam livres. Sem chão livre, o Enfeite não aparece: as vagas são preenchidas por ordem (a primeira de todos, depois a segunda), de quem está na frente para quem está atrás. A conta fica em `enfeitesNaPraca.ts`; ver `decisoes/0050-enfeites-no-chao.md`.
- Tocar num avatar, o próprio ou o de um colega, abre o Foco (§3.5).

### 3.2. Poses

A pose de cada avatar vem de `GET /api/grupos/{id}/cena`, calculada a partir do status semanal no desafio em destaque e das atividades **deste grupo na semana corrente** (segunda a domingo, no fuso do grupo):

| Situação | Pose |
|---|---|
| Tem atividade `AguardandoFeedback` ou `EmRevisao` | `treinando` |
| Meta cumprida (`metaCumprida`) | `comemorando` |
| Demais casos, sem desafio ou a semana não conta | `parado`, com uma variação de vez em quando (`alongar`, `olhar`) |

As variações de `parado` tocam em momentos pseudoaleatórios, com semente no identificador do usuário, para que os avatares não se mexam todos juntos.

### 3.3. Encenações

As interações acontecem no centro do círculo. Como tudo é assíncrono, cada pessoa vê as encenações que ainda não viu na próxima vez que abre a cena.

| Evento | Encenação |
|---|---|
| Carta recebida | O remetente anda até o centro (`andar`), vira para o destinatário e arremessa (`arremessar`). A carta voa em CSS, o destinatário a pega (`receber`) e o remetente volta ao lugar. |
| Carta aceita (vista pelo remetente) | O destinatário faz `aceitar`. A carta se dobra num objetivo ativo. |
| Carta recusada (vista pelo remetente) | O destinatário faz `recusar` e a carta desliza para fora. |
| DESAFIO DUPLO | Igual a "carta recebida", com os papéis trocados. |
| Jogada concluída | O destinatário faz `comemorando` e a carta vira com o carimbo CONCLUÍDA. |
| Jogada expirada | A carta some com o carimbo EXPIRADA. Nenhum avatar reage. |
| Rivais da semana | Quem escolhe vê na hora, e o Rival na primeira visita: os dois avançam um passo e fazem `provocar`. Quando o resultado é final, quem escolheu vê o vencedor fazer `comemorando`. |
| Missão concluída | `comemorando` com um brilho em CSS. |
| Enfeite novo (vista só pelo dono) | `comemorando`, e o Enfeite cai no chão com o mesmo brilho (§9.2), mesmo que o tamanho do grupo o esconda fora da encenação. |

- O movimento vertical usa o mesmo `andar` de lado, com um balanço de 1 a 2 px. O deslizamento na vertical é aceito como estilo.
- No máximo uma encenação de cada vez, e no máximo 3 em sequência por visita. As demais aparecem só como estado final.
- Tocar na tela pula a encenação. Ela nunca bloqueia a navegação nem o registro de atividade.
- Com `prefers-reduced-motion`, as poses ficam estáticas e as cartas aparecem no lugar final, sem voo.
- As animações só rodam com a página visível.
- A cena sabe o que já foi visto pelas colunas `vista_em` (destinatário) e `resultado_visto_em` (remetente) da Jogada. Por isso, na G3, só "carta recebida", "carta aceita" e "carta recusada" têm encenação; Jogadas concluídas ou expiradas aparecem já no estado final, com o carimbo (`decisoes/0035-cartas-e-jogadas.md`).

### 3.4. Vida na praça

Entre as encenações, a praça tem luz e movimento próprios. Nada disso é encenação: não conta no limite de 3 por visita, não marca nada como visto e não depende do servidor.

- **Sombras:** cada avatar, o pombo e a bola têm uma sombra oval sob os pés. A sombra fica no chão quando o avatar pula.
- **Período do dia:** a luz segue a hora do aparelho de quem vê a cena. Amanhecer (5h às 7h): tom coral claro. Dia (7h às 17h): sem filtro. Entardecer (17h às 19h): violeta para coral. Noite (19h às 5h): violeta escuro. Os nomes dos avatares ficam acima da luz. A luz tinge só a praça: o céu transparente fica de fora.
- **Luminárias:** uma acima da porta do fundo, espelhada, com a cúpula centrada na porta, e duas no chão, ao lado das floreiras da frente. Elas acendem no entardecer e à noite, com um brilho em degraus feito em CSS. As posições acompanham o chão atual da praça; mude-as em `VidaNaPraca.tsx` se a praça mudar.
- **Eventos de ambiente:** um de cada vez, a cada 12 a 25 s, e o primeiro de 3 a 6 s depois de abrir a cena:
  - Um pombo pousa num trecho livre do chão (fora da elipse), bica o chão, anda alguns passos e vai embora. À noite não há pombos.
  - Dois vizinhos `parado` se viram um para o outro e conversam por 5 s; no meio, um deles faz `olhar`.
  - Uma bola entra pela borda de baixo e rola até um avatar `parado`, que a devolve com `arremessar`.
- **Entrada:** na primeira visita do dia (cookie `wx_entrada_praca`, com a data local), cada avatar chega ao seu lugar sem atravessar parede. A porta do fundo abre, e quem fica na metade de trás da elipse sai por ela, um de cada vez: dá um passo para fora (`andar`) e corre (`correr`) até o lugar; depois, a porta fecha. Quem fica na metade da frente sobe correndo pela borda de baixo, que é aberta. Em grupos de até 3 pessoas, todos saem pela porta. A porta é um recorte à parte (`ObjetosPraca.tsx`), pronta para outras encenações. As encenações começam depois da entrada. Tocar na praça pula a entrada.
- **Toque:** tocar num avatar faz ele dar um pulinho enquanto o Foco abre.
- Durante uma encenação ou a entrada, os eventos param e o pombo levanta voo. Com `prefers-reduced-motion`, não há eventos, entrada nem pulinho; a luz e as luminárias continuam. Com a página escondida, nenhum evento começa.
- A arte (pombo, bola e luminárias) fica em `webapp/public/ambiente.png`, gerada por `pixel-art/ambiente/build.py`.

### 3.5. Foco

Tocar num colega abre o **Foco**: a câmera se aproxima dele dentro da própria praça, e é dali que se joga uma carta ou se escolhe o Rival. Tocar no próprio avatar abre o mesmo Foco, com a Vitrine no lugar da Mão. O alvo fica na URL (`?foco=`), então o botão voltar fecha o Foco. A lógica está em `useFoco.ts`; o painel, em `PainelFoco.tsx`; a Mão, em `MaoJogo.tsx`; a Mesa, em `MesaJogadas.tsx`.

- **Câmera:** uma camada dentro da praça com `translate` e `scale` (2,2× no Foco, 2× na ação), com transição de 450 ms. Ela leva o chão, os avatares, os objetos e a luz. O alvo gira de direção em direção até `sul`, olhando para quem vê, com a pose `parado` das 8 direções (`getGiro`), sem arte nova. Os outros avatares e os Enfeites deles ficam apagados, e os Enfeites do alvo que não estavam no chão caem em volta dele; os eventos de ambiente e as encenações esperam o Foco fechar. Ao abrir, a página rola até o alto da praça, com a Mão logo abaixo.
- **Painel:** na parte de baixo da praça, fora da câmera. Mostra o nome, a Vitrine, a semana no desafio, a sequência, os totais e o Rival da semana, com link para a página do membro. As Jogadas entre os dois ficam na Mesa.
- **Mão:** uma só, logo abaixo da praça, dentro e fora do Foco. Ao abrir o Foco, ela sobe até ficar sob o painel e as cartas são distribuídas uma a uma, em leque. As cartas em Jogadas abertas ficam na Mesa e deixam na Mão uma vaga reservada, tracejada. Tocar seleciona: a carta sobe, cresce e balança. Para jogar, arrasta-se a carta para cima ou toca-se em "Arremessar"; o botão garante uma alternativa ao arrasto. Durante o arrasto, um anel de mira pulsa sob os pés do alvo. Antes do arremesso, a Mão confere os limites de §6.3 que as Jogadas do grupo mostram (Jogada aberta entre os dois, carta para a mesma pessoa em 24 horas, 2 enviadas em aberto): as cartas ficam apagadas e o texto diz o motivo. O servidor confere tudo de novo.
- **Carta primeiro:** fora do Foco, escolher uma Provocação faz um anel pulsar sob os colegas que podem recebê-la agora. Tocar num deles abre o Foco com a carta já escolhida. Um toque no chão ou de novo na carta desfaz a escolha.
- **Mesa:** dentro da Mão, abaixo das cartas, uma fileira de mini-cartas com as Jogadas de quem vê no grupo: ícone com um anel do prazo que resta, o outro nome e a situação. As que pedem resposta vêm primeiro e pulsam; as encerradas, no fim e apagadas. O rótulo "Na mesa" fica à esquerda da fileira. No Foco, só as Jogadas com o alvo, com o rótulo "Entre vocês"; sem Jogadas, a Mesa some.
- **Altura:** numa tela de 390×844, a praça e a Mão inteira cabem sem rolar, no início e no Foco. Por isso a Mão não tem texto de ajuda: a primeira linha mostra "Sua Mão 3/5" e o saldo de Energia, e um texto curto toma o lugar do título só quando há o que dizer (o efeito da carta escolhida, as regras do "VS", quem não recebe cartas ou o limite de envio). A carta escolhida sobe por cima dessa linha, sem altura reservada. Tocar numa abre uma janela com o objetivo, o prazo e, se for o caso, Aceitar, Recusar e Aceitar com uma Resposta.
- **Arremesso:** o painel desce e a Mão some sem mudar de altura; a câmera vai até quem joga, que gira para o alvo e faz `arremessar`. O envio corre junto, e o braço espera no quadro da soltura até o servidor responder. Deu certo: a carta voa girando, a câmera a acompanha, o alvo faz `receber`, a câmera treme, o nome da carta estoura sobre o alvo e o aparelho vibra. Deu errado: a carta cai, quem jogou faz `recusar` e a mensagem do erro aparece.
- **Rival:** a carta "VS" abre a Mão quando o colega pode ser escolhido. A câmera enquadra os dois, que dão um passo para o centro e se encaram; o "VS" bate na tela e os dois fazem `provocar`. Como a escolha toca no Foco, ela não se repete como encenação para quem escolheu; o Rival a vê na próxima visita (§3.3).
- **Próprio Foco:** tocar no próprio avatar abre o Foco nele, com a mesma câmera, giro, painel e formas de fechar. O painel não tem Rival nem aviso de cartas, e o link diz "Ver a minha página". No lugar da Mão (e da Mesa) fica a **Vitrine** (`VitrineFoco.tsx`): "Sua Vitrine 2/3", o link "Trocar avatar" (`/perfil/avatar`), as 3 vagas em ordem, os Enfeites liberados e todos os bloqueados, dos mais perto de liberar aos mais longe. Tocar num liberado o põe na próxima vaga; tocar de novo, ou na vaga, o tira. Cada toque salva na hora (os envios seguem em fila), e os Enfeites trocam no chão da praça. Se o envio falhar, a vitrine volta ao que era e o erro aparece. A ordem muda tirando e pondo de novo. Com a vitrine e os bloqueados, a página pode rolar.
- **Fechar:** "✕", Esc, o botão voltar ou um toque no chão. Tocar noutro avatar, inclusive o próprio, muda o Foco para ele.
- **Movimento reduzido:** a câmera e o giro vão direto ao fim, a carta não voa e o impacto vira um quadro fixo de `receber`; o "VS" aparece sem animação. O arrasto continua valendo, porque é entrada, não enfeite.

## 4. Avatar

- Cada usuário tem um único avatar, igual em todos os grupos.
- Quem não importou uma folha usa a folha padrão do bundle, `avatar-base-masculino.png`, em 8 direções (`decisoes/0039-avatar-em-8-direcoes.md`, Andamento). As 6 folhas de lado e a do `jpgma` saíram do bundle.
- A pessoa pode **importar a própria folha de sprites**, num formato documentado publicamente (§4.3).
- Fora da cena, o componente `Avatar` mostra a célula `retrato` da folha no lugar das iniciais. Isso vale para todos, inclusive para quem está no modo App.
- O avatar é só visual: nenhuma folha dá vantagem no Jogo.

### 4.1. Formato

> **Em revisão:** folha vista de cima, em 8 direções, proposta em `decisoes/0039-avatar-em-8-direcoes.md` (Q034). A cena usa a folha em 8 direções (`avatar-formato.md` §6), e a importação aceita só esse formato. Todas as animações do Jogo têm linhas próprias na folha, com os tempos da tabela de lá. O resumo abaixo e a tabela de §4.2 descrevem o formato de lado antigo, que não é mais aceito.

Resumo de `avatar-formato.md`:

- PNG RGBA de 8 bits, exatamente **256×384 px**: 8 colunas × 12 linhas de células de **32×32**.
- Uma linha por animação, sempre virada para a direita. Pés ancorados em (16, 31).
- As linhas, a quantidade de quadros e os tempos são fixos, o que deixa a reprodução determinística.
- Só `retrato`, `parado` e `andar` são obrigatórias. Uma linha vazia cai para `parado`.

### 4.2. Animações

A arte padrão é o personagem do template CC0 *2D Platformer Character Template*, em quadros de 16×16 ampliados 2×, com pele, cabelo, roupa e acessórios trocáveis. As linhas novas são montadas com peças dos quadros do template. Detalhes em `avatar-kit.md` §3.1.

| Linha | Quadros | Repetição | Uso | Base no template |
|---|---:|---|---|---|
| `retrato` | 1 | estático | `Avatar` fora da cena | cabeça do JumpTurn1 (de frente), ampliada 3× |
| `parado` | 4 | sim, 200 ms | pose padrão, respirando | Idle1, com a parte de cima descendo 1 px |
| `andar` | 8 | sim, 100 ms | ir ao centro e voltar | Walk1–8 |
| `treinando` | 6 | sim, 90 ms | atividade aguardando verificação | Jog 1, 2, 4, 5, 6 e 8 |
| `comemorando` | 6 | sim, 120 ms | meta cumprida, Jogada e missão concluídas, rival vencido | Jump1–5, como pulo no lugar |
| `alongar` | 4 | uma vez | variação de `parado` | Idle1 com o braço do WallSlide |
| `olhar` | 4 | uma vez | variação de `parado` | Idle1 com o olho para a frente e para trás |
| `arremessar` | 5 | uma vez; a carta sai no terceiro quadro | jogar uma carta, DESAFIO DUPLO | Crouch 1–2 (preparo), Dash 1–2 (seguimento), Idle1 |
| `receber` | 4 | uma vez | pegar a carta | Idle1 com os braços do Jump2 |
| `aceitar` | 4 | uma vez | aceitar a carta (soco no ar) | Crouch1, Idle1 com o braço do WallSlide |
| `recusar` | 3 | uma vez | recusar a carta (dar de ombros) | Idle1 com os braços erguidos 1 px |
| `provocar` | 4 | uma vez | encontro dos rivais | Idle1 com o braço esticado do Dash |

- São 53 quadros por folha. Com paleta pequena, um PNG fica entre 5 e 15 KB.
- Não precisam de animação própria: virar (troca de direção instantânea), o voo e os carimbos da carta (CSS) e o brilho da missão (CSS).

### 4.3. Importação

- O editor em `/perfil/avatar` tem três botões: **Exportar folha** (baixa a folha atual, importada ou padrão), **Exportar guia** (uma camada com a grade, o nome de cada linha e a âncora, gerada no navegador) e **Importar folha**. Antes de salvar, ele mostra uma prévia das 8 direções na animação escolhida.
- O aplicativo só confere o tamanho (432×4656): o desenho é livre.
- O navegador envia como veio um PNG RGBA de 8 bits ou com paleta indexada, sem `acTL`. Qualquer outra imagem do tamanho certo é re-codificada num canvas como PNG RGBA.
- O servidor confere de novo: assinatura PNG, cabeçalho `IHDR` com 432×4656, RGBA de 8 bits ou paleta indexada, sem `acTL` (APNG), nada depois do `IEND` e no máximo 1000 KB. O arquivo é recusado se algo falhar.
- O arquivo fica no disco, em `/dados/avatares/`, com nome aleatório, no mesmo padrão das evidências (`arquitetura.md` §11). Nada de nomes vindos do cliente.
- Só membros de um grupo em comum podem baixar a folha. A resposta usa `Content-Type: image/png` e `X-Content-Type-Options: nosniff`.
- A pessoa pode voltar ao avatar padrão a qualquer momento.
- O `Proprietario` e os `Administrador` de um grupo podem redefinir para o padrão o avatar de um membro desse grupo, como medida contra imagens impróprias.
- Ao trocar ou redefinir, o arquivo antigo é apagado. Uma limpeza diária apaga os arquivos sem dono há mais de 1 hora.

### 4.4. Arte do bundle

Todo o bundle é CC0, com o arquivo `LICENSE` junto dos sprites. Nada é carregado de servidores externos.

| Peça | Tamanho | Quantidade |
|---|---|---:|
| Folhas padrão de avatar (pele clara, morena e negra; corpo masculino e feminino) | 256×384 | 6 |
| Praça (vista em ¾, quadra urbana; ver §1.3), com céu transparente | 180×200 (mestre 1×, mostrado em 2×, ou seja, 360×400) | 1 |
| Objetos soltos da praça (barra de calistenia, caixotes, floreiras) e porta aberta | folha `praca-objetos.png`, recortes de tamanhos variados | 6 |
| Moldura de carta (mesma forma, acento coral ou violeta; ver §1.3) | 24×32 | 2 (Provocação, Resposta) |
| Ícone de carta | 32×32 | 9 |
| Enfeite (atlas `enfeites.png`: 4 quadros por linha, objeto apoiado na base da célula; o quadro 1 é o ícone) | 40×40 por quadro, mostrado em 2× | 19 linhas (§9.1) |

## 5. Energia

Energia (EN) é a moeda do Jogo. É única por usuário, somando todos os grupos. Ela só serve para comprar cartas.

### 5.1. Como se ganha

| Evento | EN |
|---|---:|
| Dia com atividade verificada, em cada grupo (antes de 9/11/2026: cada atividade verificada) | 10 |
| Semana com a meta cumprida (uma vez por pessoa por semana) | 20 |
| Sequência de 4 semanas | 25 |
| Sequência de 8 semanas | 50 |
| Sequência de 12 semanas | 100 |
| Desafio concluído | 75 |
| Missão semanal concluída (§7) | 5 a 15 |
| Rival da semana vencido (§8), em cada grupo | 15 |
| Jogada concluída, para quem recebeu (§6.4) | 1 × o preço da carta (2 × com APOSTA) |
| Jogada concluída, para quem enviou (§6.4) | 0,5 × o preço da carta |

- As sequências seguem a mesma regra das conquistas: a melhor sequência num único desafio, sem somar desafios. Cada marco paga **uma vez por pessoa**, então as sequências rendem no máximo 175 EN no total.
- **Desafio concluído:** um desafio `Encerrado` com todas as semanas que contavam para a pessoa cumpridas. Paga uma vez por desafio.
- **Vários grupos:** a base de 10 EN vale para cada dia com atividade verificada em cada grupo. Dividir um treino em várias atividades no mesmo dia não rende mais base (`decisoes/0040-meta-diaria.md`). A meta semanal paga uma vez por pessoa por semana, no primeiro grupo em que for cumprida. Os bônus de §5.2 também contam por pessoa. Assim, estar em mais grupos não multiplica a Energia. A exceção é o Rival da semana, que paga em cada grupo (§8).

**Alvo do balanceamento:** quem cumpre a meta com regularidade ganha cerca de 90 a 110 EN por semana, o suficiente para 2 ou 3 cartas. É essa escassez que faz cada gasto importar.

### 5.2. Bônus por atividade

| Qualificador | EN |
|---|---:|
| Começou antes das 06:00 | 5 |
| Começou entre 06:00 e 08:00 | 3 |
| Fim de semana | 3 |
| Primeira atividade da semana | 3 |
| Primeira atividade num dia da semana ainda sem treino | 3 |
| Primeiro tipo de atividade novo na semana, a partir do 2º tipo | 5 |

- Máximo de **10 EN de bônus por atividade**.
- Horários e semanas no fuso do grupo da atividade. Toda semana vai de segunda a domingo, como no desafio.
- Os qualificadores de "primeira" (da semana, num dia novo, tipo novo) consideram as atividades da pessoa em **todos os grupos**.
- O tipo da primeira atividade da semana só é registrado: o bônus de tipo novo vale para o 2º tipo em diante.
- O bônus de madrugada (antes das 06:00 ou entre 06:00 e 08:00) conta no máximo uma vez por pessoa por dia. O bônus de fim de semana também, de forma independente (`decisoes/0034-ordem-dos-bonus-de-energia.md`).
- Várias atividades no mesmo dia rendem o bônus de dia novo só uma vez.

### 5.3. Cálculo na leitura

Não existe carteira nem livro-razão. O saldo é calculado a cada consulta, como as conquistas (`decisoes/0018-perfil-e-pagina-do-membro.md`):

```text
saldo = Energia ganha (calculada) − soma de cartas.preco
```

Regras que garantem um saldo estável:

- Só atividades `Verificada` geram Energia. `Verificada` é final, então um crédito nunca desaparece.
- Os qualificadores ("primeira da semana", "dia novo", "tipo novo") e os bônus de horário são calculados sobre as atividades **não rejeitadas**, em **ordem de registro** (`created_at`, `id`), e não de `started_at`: assim uma atividade registrada com data passada nunca toma o bônus de outra já verificada (`decisoes/0034-ordem-dos-bonus-de-energia.md`). Uma atividade pendente já ocupa sua posição. Se ela for rejeitada, a seguinte pode ganhar o bônus, mas nenhum crédito é perdido.
- Uma Jogada só rende Energia quando fica `Concluida`, e isso depende de uma atividade `Verificada`. Esse crédito também nunca desaparece.
- Por isso a Energia ganha **só cresce**, e o saldo nunca fica negativo se cada compra verificar `saldo >= preço`.
- Usar uma carta não muda o saldo: o custo entrou na compra. Uma Provocação devolvida à Mão (§6.2) também não muda o saldo, porque continua comprada.
- Atividades de grupos que a pessoa deixou continuam valendo, como as conquistas.

A regra fica numa função pura (`CalculoEnergia`), que recebe as atividades de todos os grupos da pessoa, com testes unitários. Um dos testes simula uma semana típica: as fontes da G2 rendem 73 EN, e Missões, Rival e Jogadas completam o alvo de §5.1. Outro confere que o mesmo treino registrado em dois grupos não dobra os bônus nem a meta.

## 6. Loja e cartas

### 6.1. Catálogo

O catálogo fica no código, sem tabela. Cada carta tem nome, tipo, preço e efeito.

| Carta | Tipo | Preço | Efeito |
|---|---|---:|---|
| +10 MIN | `Provocacao` | 30 | Somar num mesmo dia pelo menos a duração mínima do desafio + 10 minutos. |
| +20 MIN | `Provocacao` | 45 | Somar num mesmo dia pelo menos a duração mínima do desafio + 20 minutos. |
| VARIEDADE | `Provocacao` | 35 | Uma atividade de tipo diferente da última atividade do destinatário, neste grupo, já verificada no momento do envio. |
| FIM DE SEMANA | `Provocacao` | 35 | Uma atividade que comece no sábado ou no domingo. |
| ANTES DAS 8 | `Provocacao` | 40 | Uma atividade que comece antes das 08:00. |
| DESAFIO DUPLO | `Resposta` | 40 | Aceita a Provocação e devolve o mesmo objetivo para o remetente, como uma Jogada nova. |
| APOSTA | `Resposta` | 25 | Aceita a Provocação. Se a Jogada for concluída, a recompensa do destinatário dobra. Se não for, a carta está perdida. |
| PRAZO EXTRA | `Resposta` | 20 | Aceita a Provocação com 24 horas a mais de prazo. |
| MUDANÇA DE PLANOS | `Resposta` | 25 | Aceita a Provocação trocando o objetivo por um equivalente da mesma faixa de preço, à escolha de quem aceita. |

- **Trocas da MUDANÇA DE PLANOS:** `+10 MIN ↔ VARIEDADE ↔ FIM DE SEMANA` e `+20 MIN ↔ ANTES DAS 8`. A duração-alvo e o tipo a evitar do objetivo novo são fixados no aceite. Trocar para +10 MIN ou +20 MIN exige um desafio ativo. O prazo passa a ser o maior entre o atual e o da carta nova, contado do envio (`decisoes/0036-respostas.md`).
- **DESAFIO DUPLO:** a Jogada devolvida usa a Provocação original, que continua sendo de quem a comprou. A duração-alvo é copiada, o tipo a evitar da VARIEDADE é o de quem vai cumprir e o prazo começa no aceite. A devolução não confere os limites de §6.3, mas exige que quem enviou esteja no modo Jogo.
- Os objetivos usam as regras e o fuso do grupo da Jogada. Sem desafio ativo, as cartas +10 MIN e +20 MIN não podem ser jogadas nesse grupo. A duração-alvo e o tipo a evitar da VARIEDADE são fixados no envio. Em +10 MIN e +20 MIN, somam-se as atividades de um mesmo dia dentro do prazo da Jogada.
- Cumprir a Provocação também conta para a meta oficial, como qualquer atividade. Não existe tipo especial de atividade.
- Não cumprir ou recusar não muda nada no progresso oficial.

### 6.2. Compra e Mão

- A **Mão** é por pessoa, com no máximo **5 cartas**, e vale em qualquer grupo. O limite conta as cartas não gastas, inclusive as que estão em Jogadas abertas, para que uma carta devolvida nunca passe de 5.
- A compra roda numa transação que bloqueia a linha do usuário, recalcula o saldo e só grava se `saldo >= preço` e a Mão tiver espaço.
- A linha de `cartas` guarda o preço pago. Mudar o catálogo depois não altera cartas já compradas.
- **Quando uma carta é gasta**, calculado a partir das Jogadas, sem coluna de data de uso:
  - uma Provocação só é gasta quando a Jogada fica `Concluida`. Se a Jogada ficar `Recusada` ou `Expirada`, a carta volta para a Mão de quem enviou e pode ser jogada de novo;
  - uma Resposta é gasta quando é usada, aconteça o que acontecer com a Jogada.
- Não há pacotes aleatórios, troca de cartas nem devolução.

### 6.3. Jogar uma carta

- Uma Provocação é jogada da Mão para outro membro do mesmo grupo que esteja no modo Jogo.
- O destinatário recebe uma `Notificacao` no aplicativo (`decisoes/0020-notificacoes-no-aplicativo.md`) e vê a encenação na próxima visita à cena.
- O remetente recebe uma `Notificacao` quando a carta é aceita, recusada ou concluída. A conclusão também avisa o destinatário. Ela é gravada pelo contrato `Features/Jogo/Contracts/JogadasConcluidas.cs`, chamado em todo lugar que deixa uma atividade `Verificada`, na mesma transação. A expiração não gera aviso, porque é calculada na leitura.
- Diante de uma Provocação, o destinatário pode:
  - **Aceitar**, de graça;
  - **Aceitar com uma Resposta** da própria Mão (DESAFIO DUPLO, APOSTA, PRAZO EXTRA ou MUDANÇA DE PLANOS), no máximo uma por Jogada;
  - **Recusar**, de graça e sem consequência.
- Uma Jogada criada por DESAFIO DUPLO não aceita outro DESAFIO DUPLO. Não existem cadeias de contra-ataque.

**Limites**, conferidos na mesma transação que bloqueia a linha do remetente:

| Regra | Limite |
|---|---:|
| Cartas na Mão | 5 |
| Jogadas recebidas ativas | 2 |
| Jogadas enviadas ativas | 2 |
| Jogada ativa por par de pessoas | 1 |
| Jogada nova para a mesma pessoa | 1 a cada 24 horas |

- **Jogada ativa**, para esses limites: `Enviada` ou `Aceita`. Uma Jogada `AguardandoVerificacao` libera a vaga, mesmo com a atividade em `EmRevisao`.
- Os limites só são conferidos ao enviar. Se uma atividade for rejeitada e a Jogada voltar a `Aceita`, a contagem pode passar do máximo por um tempo.
- Os limites ficam em constantes no código, fáceis de ajustar depois de observar o uso.

### 6.4. Ciclo de vida de uma Jogada

**Gravado:** remetente, destinatário, grupo, carta, `enviada_em`, `expira_em`, `resposta` (`Aceita` ou `Recusada`), `respondida_em`, carta de resposta usada, `origem_id` (a Jogada original, no DESAFIO DUPLO), o objetivo trocado pela MUDANÇA DE PLANOS (`codigo_objetivo`), o tipo-alvo da VARIEDADE (o tipo a evitar, fixado no envio), `vista_em` e `resultado_visto_em`.

**Calculado na leitura**, sem tarefa agendada:

| Situação | Condição |
|---|---|
| `Enviada` | Sem resposta e antes de `expira_em`. |
| `Recusada` | `resposta = Recusada`. |
| `Aceita` | Aceita e ainda sem atividade que conte. |
| `AguardandoVerificacao` | Aceita, com atividade que conta ainda não verificada. |
| `Concluida` | Aceita, com atividade que conta `Verificada`. |
| `Expirada` | Passou de `expira_em` sem resposta, ou aceita sem atividade que conte até lá. |

- **Atividade que conta:** não rejeitada, do destinatário, no grupo da Jogada, com `started_at` entre `enviada_em` e `expira_em`, que cumpre o objetivo da carta. A Jogada precisa ter sido aceita antes de `expira_em`.
- Uma atividade feita antes de aceitar, mas depois do envio, conta.
- A verificação pode acontecer depois de `expira_em`: o que importa é o `started_at`.
- A mesma atividade pode concluir várias Jogadas e missões.
- **Prazo:** 48 horas a partir do envio. FIM DE SEMANA vale até o fim do próximo domingo (enviada num domingo, vale até o domingo seguinte). ANTES DAS 8 vale até as 08:00 do segundo dia depois do envio, para que sempre haja duas manhãs. PRAZO EXTRA soma 24 horas, uma vez por Jogada. Tudo no fuso do grupo.
- **Recompensas** (§5.1): ao ficar `Concluida`, o destinatário ganha 1 × o preço da carta e o remetente ganha 0,5 × o preço, arredondado para baixo. Com APOSTA, a parte do destinatário dobra (2 × o preço), o que rende de 5 a 20 EN líquidos a mais, ou custa os 25 EN da carta. Na Jogada criada por DESAFIO DUPLO, as recompensas usam o preço da Provocação original.
- **Custo para quem enviou:** só uma Jogada `Concluida` gasta a Provocação. Com a recompensa de 0,5 ×, uma carta concluída custa na prática metade do preço. Uma carta recusada ou expirada volta para a Mão (§6.2).

## 7. Missões semanais

- Toda semana (de segunda a domingo, no fuso do grupo), cada pessoa recebe de 2 a 3 missões, sorteadas de uma lista fixa no código. O sorteio é determinístico por usuário e semana, então não precisa ser gravado.
- O progresso vem das atividades verificadas, como tudo o mais.
- A mesma atividade pode contar para várias missões e Jogadas.
- Exemplos:
  - Treinar em 3 dias diferentes.
  - Registrar 2 tipos de atividade diferentes.
  - Cumprir a meta antes de sexta-feira.
  - Treinar num fim de semana.
- Missões não exigem mais do que uma participação saudável nem um desempenho físico específico.
- Não há missões diárias.

## 8. Rival da semana

Decidido em `decisoes/0038-rival-da-semana.md`.

- Cada pessoa no modo Jogo pode escolher um Rival por semana **em cada grupo**, entre os colegas também no modo Jogo. O Rival não precisa aceitar e não perde nada.
- A semana é a do desafio em destaque, que precisa estar `Ativo`, e as duas pessoas precisam participar dela.
- A escolha só vale **antes de alguém dos dois cumprir a meta**, contando as atividades pendentes. Depois de escolhido, o Rival não muda.
- Critério normalizado: vence quem cumprir a meta semanal primeiro, pelo momento em que o N-ésimo dia passou a contar, com N = dias por semana. Um dia conta no `verified_at` da atividade verificada que levou a soma do dia ao mínimo. Se só um cumprir, esse vence. Empate exato ou ninguém cumprindo até o domingo mais 2 dias de verificação: sem vencedor.
- Venceu e Perdeu são finais assim que aparecem, porque `Verificada` é final. Tudo é calculado na leitura, sem tarefa agendada.
- Prêmio: 15 EN e um selo, só para quem escolheu. Cada grupo paga à parte.
- O Rival recebe a `Notificacao` `RivalEscolhido`. Só as duas pessoas do par veem o par.
- Na praça, um marcador ⚔ ao lado do avatar mostra o par: cheio em quem você escolheu, contornado em quem escolheu você. O painel do Foco diz a relação por extenso. A escolha é a carta "VS" do Foco (§3.5). O resultado aparece na encenação e na notificação; não há painel próprio do Rival.
- Não há aposta de Energia.
- Na cena, os rivais fazem `provocar`, e o vencedor faz `comemorando` (§3.3).

## 9. Enfeites

Cada Conquista libera um **Enfeite**: um objeto que conta, só de olhar, o que a pessoa conquistou. Quem pedala muito ganha a bicicleta; quem treina cedo ganha o galo. Os Enfeites são calculados como as conquistas, sem tabela e sem compra.

### 9.1. Conquistas e Enfeites

As conquistas marcadas como **nova** ainda não existem em `ConquistasUsuario` e entram na G6. Todas contam só atividades `Verificada`, somando todos os grupos, com horários no fuso do grupo de cada atividade, como as atuais.

**Constância**, a conquista principal: o Enfeite evolui em vez de se repetir.

| Conquista | Condição | Enfeite |
|---|---|---|
| `Sequencia4` | Sequência de 4 semanas | Muda num vaso |
| `Sequencia8` | Sequência de 8 semanas | Planta com flor (substitui a muda) |
| `Sequencia12` | Sequência de 12 semanas | Árvore pequena em vaso (substitui a planta) |
| `Treinos10` | 10 dias com treino | Kettlebell de bronze |
| `Treinos50` | 50 dias com treino | Kettlebell de prata (substitui o de bronze) |
| `Treinos100` | 100 dias com treino | Kettlebell de ouro (substitui o de prata) |
| `DesafioCompleto` (nova) | Um desafio `Encerrado` com todas as semanas que contavam cumpridas (a mesma regra do desafio concluído, §5.1) | Troféu no pedestal |

**Tipo de atividade**, uma por tipo, com **10 atividades** daquele tipo. `Outro` não tem conquista.

| Conquista | Tipo | Enfeite |
|---|---|---|
| `RatoDeAcademia` (nova) | `Musculacao` | Suporte de halteres |
| `Corredor` (nova) | `Corrida` | Bloco de largada com tênis |
| `Andarilho` (nova) | `Caminhada` | Cachorro sentado, de coleira |
| `Ciclista` (nova) | `Ciclismo` | Bicicleta |
| `Peixe` (nova) | `Natacao` | Piscininha com boia |
| `Boleiro` (nova) | `Esporte` | Golzinho com bola |
| `Zen` (nova) | `Yoga` | Tapete de ioga meio desenrolado |
| `PesoDoCorpo` (nova) | `Calistenia` | Paralelas (as barrinhas de chão) |

**Hábito**, para jeitos diferentes de manter a rotina:

| Conquista | Condição | Enfeite |
|---|---|---|
| `Madrugador` | 10 atividades iniciadas antes das 07:00 (já existe) | Galo |
| `Noturno` (nova) | 10 atividades iniciadas a partir das 20:00 | Poste de luz da praça (apagado de dia, aceso no entardecer e à noite) |
| `FimDeSemana` (nova) | 10 atividades no sábado ou no domingo | Caixa de som de festa |
| `Ecletico` (nova) | Atividades verificadas de 5 tipos diferentes, sem contar `Outro` | Bolsa de academia |

- São 19 conquistas e 19 desenhos de Enfeite. A planta e o kettlebell evoluem, então a cena mostra no máximo 15 objetos diferentes.
- Todo Enfeite é um objeto que faz sentido no chão de uma praça (nada de medalha pendurada ou guia solta no ar), com o tamanho real: os maiores (árvore, poste, troféu no pedestal) têm a altura de um avatar. Cada um tem um ciclo parado de 4 quadros, fora de sincronia com os outros; com movimento reduzido, fica no quadro 1.
- Nenhuma conquista usa distância, ritmo ou carga: só contagem de atividades, horário e semanas cumpridas. Uma pessoa que faz 20 caminhadas tem a mesma chance de um Enfeite que uma pessoa que faz 20 corridas.
- Os nomes e os limites (10 por tipo, 10 por hábito) ficam em constantes, como os demais marcos.
- `Madrugador` e `Noturno` cobrem as duas pontas do dia, para que treinar cedo não seja o único horário premiado (ver §12, item 3).

### 9.2. Vitrine

- A pessoa tem uma **vitrine** com até **3 Enfeites**. A cena os põe no chão, na ordem da vitrine, até o limite do tamanho do grupo (§3.1).
- Tocar no avatar abre o Foco, cujo painel mostra a vitrine inteira, junto do acesso à página do membro e às cartas.
- Um Enfeite novo entra sozinho na vitrine se houver espaço. A pessoa reorganiza ou tira Enfeites no próprio Foco (§3.5) ou em `/perfil/avatar`.
- Um Enfeite que evolui troca de nível no mesmo lugar da vitrine.
- Em `/perfil/avatar`, os Enfeites que ainda faltam aparecem cinza, com cadeado, a conquista que os libera e o progresso. Eles não podem entrar na vitrine.
- Só aparecem Enfeites de quem está no modo Jogo. Quem está no modo App não expõe nada.
- **Enfeite novo:** na próxima visita à cena, o avatar faz `comemorando` e o Enfeite cai no chão com o mesmo brilho em CSS da missão (§3.3). Os colegas veem o Enfeite já no lugar, sem encenação.

A vitrine é uma lista de **códigos de Enfeite** (a família: `planta`, `kettlebell`…), e não de conquista, guardada na tabela `avatares`. Assim a evolução mantém o lugar. Um código que não vale mais é ignorado na leitura, e os Enfeites já vistos (`enfeites_vistos`) que a pessoa tirou não voltam sozinhos.

**Privacidade.** Hoje as conquistas aparecem só no próprio Perfil (`decisoes/0018-perfil-e-pagina-do-membro.md`). A vitrine mostra aos colegas apenas o que está nela, só para quem escolheu o modo Jogo, e a pessoa pode esvaziá-la. Decidido em `decisoes/0037-avatar-importado-e-enfeites.md`.

### 9.3. Calistenia na praça

A Calistenia combina com a direção criativa: é o treino da praça. Por isso:

- O chão da praça (§4.4) tem uma barra fixa num canto, como cenário.
- A conquista `PesoDoCorpo` libera as Paralelas, um Enfeite que conversa com a barra da praça.
- Nenhuma carta ou missão favorece a Calistenia. Ela conta como qualquer outro tipo para VARIEDADE, para o bônus de tipo novo (§5.2) e para `Ecletico`.

## 10. Fases de implementação

Cada fase entrega algo visível e pode ser a última. Todas ficam na fatia `Jogo` (backend `Features/Jogo`, frontend `features/jogo`), exceto onde indicado.

### G1 — Modo Jogo e cena

- Migração: `usuarios.modo_inicio text NOT NULL DEFAULT 'App'`, com `CHECK` para `App` e `Jogo`.
- `PUT /api/usuarios/eu/modo-inicio` e o campo `modoInicio` na sessão (ambos na fatia `Autenticacao`).
- `GET /api/grupos/{id}/cena` (fatia `Jogo`) devolve as poses. A regra do desafio em destaque ficou em `Progresso/Contracts/DesafioDestaque.cs`, compartilhada com o resumo do grupo.
- Opção na página Configurações e cartão de convite único na tela App.
- `InicioPage` escolhe entre a visão App atual e a cena, na mesma rota.
- As folhas padrão (geradas em `pixel-art/avatar-base/saida/`), o chão da praça e o `LICENSE` CC0 no bundle.
- Cena em elipse com as poses `parado`, `treinando` e `comemorando` e as variações de `parado`.
- O `Avatar` mostra o `retrato` da folha padrão.

### G2 — Energia

- Regra pura `CalculoEnergia` e a consulta SQL que a alimenta.
- `GET /api/jogo/energia` devolve o saldo e a Energia ganha por origem.
- Saldo na cena e no Perfil, só leitura. No Perfil, só para quem está no modo Jogo.

### G3 — Cartas

- Tabelas `cartas` e `jogadas`.
- Loja com as Provocações e a Mão: `GET /api/jogo/loja` e `POST /api/jogo/cartas`.
- Jogar, aceitar e recusar: `POST /api/grupos/{id}/jogadas`, `PUT /api/jogadas/{id}/resposta`.
- Situações e recompensas calculadas na leitura.
- `Notificacao` de carta recebida, aceita, recusada e concluída.
- Encenações `andar`, `arremessar`, `receber`, `aceitar` e `recusar`.

### G4 — Respostas

- DESAFIO DUPLO, APOSTA, PRAZO EXTRA e MUDANÇA DE PLANOS, à venda na Loja e usadas ao aceitar uma Provocação.
- Migração `017-add-objetivo-jogada.sql`, moldura violeta da Resposta e os quatro ícones.

### G5 — Missões semanais

- Lista fixa, sorteio determinístico e progresso derivado.
- A recompensa entra na Energia ganha. Brilho na cena.

### G6 — Avatar importado e Enfeites

- Tabela `avatares`: uma linha por usuário, com o caminho da folha importada.
- Editor em `/perfil/avatar`, upload validado e redefinição pelo `Proprietario` ou por um `Administrador`.
- Um mapa global `GET /api/jogo/avatares` (folha e vitrine de quem consulta e dos colegas de grupo) e o download da folha em `GET /api/avatares/{arquivo}`. Também `GET /api/jogo/avatar`, `PUT /api/jogo/avatar/vitrine` e `POST /api/jogo/enfeites/vistos`.
- As conquistas novas de §9.1 em `ConquistasUsuario` (fatia `Usuarios`), visíveis no Perfil para todos, inclusive no modo App.
- Enfeites liberados pelas conquistas, vitrine e encenação de Enfeite novo.
- ADR 0037 sobre mostrar a vitrine aos colegas (§9.2).

### G7 — Rival da semana

- Migração `019-create-rivais.sql`: tabela `rivais`, coluna `atividades.verified_at` e o tipo de notificação `RivalEscolhido`.
- `GET` e `POST /api/grupos/{id}/rivais` e `POST /api/grupos/{id}/rivais/vistos`.
- Resultado e recompensa derivados das atividades verificadas (`ResultadoRival`, `Rivais`).
- Painel "Rival da semana" (depois trocado pelo marcador ⚔ na praça, §8), escolha no menu do avatar e encenações `provocar` e `comemorando`.

## 11. Adiado e descartado

**Adiado**, sem fase definida:

| Ideia | Motivo |
|---|---|
| Eventos do grupo (festival de fim de semana, bingo, 100 sessões) | Precisam de datas, regras e itens próprios. Reavaliar depois da G7. |
| Marcos coletivos do grupo | Idem. |
| Cartas novas | Observar o uso das 9 primeiras antes. |
| Editor de avatar por peças (`avatar-kit.md` §7) | Montar o avatar com corpo, pele, cabelo, roupa e acessórios do kit, guardando só as escolhas. Reavaliar depois da G6. |
| Ciclos de andar de frente e de costas | O andar de lado com balanço atende. Exigiriam mais linhas em toda folha importada. |

**Descartado:**

| Ideia | Motivo |
|---|---|
| Carteira e livro-razão de transações | A Energia calculada na leitura, menos o preço guardado em cada carta, atende sem sincronização nem idempotência. |
| Loja de cosméticos | Itens permanentes deixam de importar depois de comprados. A Energia vai só para cartas, e os Enfeites vêm das conquistas. |
| Cartas de defesa (BLOQUEIO, ESCUDO) | Fazem o mesmo que recusar e desligar o modo Jogo, que são grátis. |
| Toca aqui | Sem Energia e sem escolha. A cena já tem interação com as cartas. |
| Desafio extra | As Provocações fazem o mesmo, com mais variedade. |
| Pegadinhas e efeitos sobre outros jogadores | Contrariam "ninguém prejudica ninguém". |
| Cadeias de contra-ataque, troca de cartas, pacotes aleatórios | Complexidade e sorte sem ganho para a constância. |
| Duelos com aposta entre pessoas | A APOSTA só arrisca a própria Energia. Apostar contra outra pessoa traz perda e pressão. |
| Missões diárias | Pedem abrir o aplicativo todo dia e distraem do objetivo. |
| Ranking de Energia | Mistura jogo com desempenho e cria uma segunda classificação. |
| Distância e eventos baseados em distância | Fora do escopo (`arquitetura.md` §19). |

## 12. Questões em aberto

Fechadas em `decisoes/0033-regras-de-energia-e-jogadas.md`: valores das recompensas, desafio concluído, Energia com vários grupos, marcos de sequência, carta recusada, Jogada ativa, alvo da VARIEDADE e notificações para quem enviou. A tabela da MUDANÇA DE PLANOS foi fechada em `decisoes/0036-respostas.md` (§6.1).

1. ~~Tabela da MUDANÇA DE PLANOS~~ — fechada (§6.1).
2. **Semana das missões para quem está em vários grupos** (G5). Recomendação: avaliar cada atividade na semana do fuso do seu grupo. Falta definir qual grupo serve de referência para a semana, porque o aplicativo não tem um "grupo em destaque".
3. **Madrugada.** O bônus antes das 06:00 somado à carta ANTES DAS 8 pode estimular treino de madrugada em vez de constância. Revisar depois de observar o uso.
4. **Enfeite e cena com 10 pessoas.** Aberta: implementado com o Enfeite de 16 px ao lado do avatar (escala fixa 2×, em todos os grupos); falta conferir num celular de 360 px se o Enfeite ao lado do avatar não polui o círculo. Se poluir, mostrar só ao tocar no avatar.
5. ~~Limites das conquistas novas~~ — fechada: 10 por tipo (`decisoes/0037-avatar-importado-e-enfeites.md`). Rever com os dados reais.
6. **Atividades antigas de "Treino em casa".** A migração 014 as trocou por `Calistenia`, então elas contam para `PesoDoCorpo`. Recomendação: aceitar, porque quase sempre eram treinos com o peso do corpo.

## 13. Critérios de sucesso

1. Quem usa o Jogo registra treinos pelo menos com a mesma regularidade de antes.
2. Quem joga gasta Energia toda semana, e as cartas geram conversa no grupo.
3. Pessoas com condicionamentos diferentes têm chances parecidas. As cartas de duração são opcionais e podem ser recusadas.
4. Nenhuma mecânica dificulta a meta de outra pessoa.
5. O desafio oficial continua compreensível e confiável. Quem prefere o App não recebe cartas e não percebe diferença, salvo pelos retratos no lugar das iniciais.
