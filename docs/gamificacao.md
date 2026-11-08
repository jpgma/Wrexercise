# Gamificação — Wrexercise

> **Status:** proposta refinada; as fases G1 (modo Jogo e cena), G2 (Energia), G3 (cartas), G4 (Respostas) e G6 (avatar importado e Enfeites) estão implementadas, as demais ainda não. A direção geral está em `decisoes/0023-gamificacao.md`, com as mudanças de `decisoes/0024-cartas-e-avatar-importado.md`, as regras de Energia e Jogadas de `decisoes/0033-regras-de-energia-e-jogadas.md`, a ordem dos bônus de `decisoes/0034-ordem-dos-bonus-de-energia.md`, os detalhes das cartas de `decisoes/0035-cartas-e-jogadas.md` as Respostas de `decisoes/0036-respostas.md` e o avatar importado e os Enfeites de `decisoes/0037-avatar-importado-e-enfeites.md`. O formato da folha de sprites está em `avatar-formato.md`, e o avatar base e o kit de camadas estão em `avatar-kit.md`. Cada fase atualiza `funcionalidades.md` quando for entregue.

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

- **Cena:** uma quadra ou praça urbana vista de cima, com asfalto, marcações tracejadas de quadra, um alambrado ou mureta, a barra de calistenia (§9.3) e no máximo um grafite discreto. Nada de grama de RPG, pedras, tochas ou castelos.
- **Paleta da pixel art:** os tons da marca guiam o cenário e as molduras: Asphalt `#222633` e uma rampa de cinzas-azulados para o chão, Chalk `#F5F1E8` para as linhas, Coral `#FF684A`, Electric Violet `#7957E8`, Signal Yellow `#FFD34E` e Lime `#A8D978` como acentos. Peles e cabelos continuam com as próprias rampas (`avatar-kit.md`). Salve a paleta na skill como `wrexercise` e use `Sprite(palette="wrexercise")`.
- **UI em volta da cena:** componentes comuns do aplicativo (tokens de `styles/index.css`). Violeta (`game`) marca o que é do Jogo: saldo de Energia, Mão, botão de jogar carta. Amarelo (`reward`) é recompensa e conquista; lime (`progress`), progresso das Missões e da meta.
- **Selos:** Missões, Rival e situação de Jogada usam os selos `badge-*` (caixa alta, contorno, sombra de adesivo). O Rival da semana ganha um selo com o número da posição, como placar.
- **Movimento:** só as animações das folhas (§4.2) e as encenações (§3.3). A UI em volta usa os mesmos `pop` e `burst` do aplicativo, uma vez, e respeita `prefers-reduced-motion`.

#### Cartas

- **Mesma moldura para todas** (§4.4): contorno grosso escuro, cantos levemente recortados e um número de preço no canto, como etiqueta de placar.
- **Hierarquia fixa:** nome em Anton e caixa alta (como no catálogo, §6.1); selo do tipo; ícone em pixel de 16×16; efeito em uma frase curta; preço em EN.
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
- o saldo de Energia e a sua Mão de cartas (a partir das fases G2 e G3);
- o aviso de atividades para avaliar, como hoje.

A tela não tem um botão próprio de "Registrar atividade": o registro já está na navegação inferior (celular) e na lateral (computador).

### 3.1. Layout

- Vista 2,5D, de cima e de lado: o chão é uma praça vista de cima, e os avatares são desenhados de lado.
- Os avatares ficam numa **elipse em pé**, que ocupa a altura da praça, virados para o centro. Quem está à esquerda do centro olha para a direita, e quem está à direita aparece espelhado (`scaleX(-1)`).
- A ordem de desenho segue o y dos pés: quem está mais abaixo na tela fica na frente.
- Escala inteira, com `image-rendering: pixelated`. O quadro de 32 px aparece em 2× (64 px). A praça tem 360×460 px e encolhe por inteiro em telas mais estreitas. A elipse tem raio horizontal de cerca de 130 px, e cabem 10 avatares com folga. Grupos de até 5 pessoas usam 3×.
- O primeiro Enfeite da vitrine (§9.2) aparece ao lado de cada avatar.
- Tocar num avatar mostra a vitrine de Enfeites e dá acesso à página do membro. No modo Jogo, também permite jogar uma carta da Mão para esse colega.

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
| Rivais da semana | Na primeira visita da semana, os dois avançam um passo e fazem `provocar`. Ao fim da semana, o vencedor faz `comemorando`. |
| Missão concluída | `comemorando` com um brilho em CSS. |
| Enfeite novo (vista só pelo dono) | `comemorando`, e o Enfeite aparece ao lado com o mesmo brilho (§9.2). |

- O movimento vertical usa o mesmo `andar` de lado, com um balanço de 1 a 2 px. O deslizamento na vertical é aceito como estilo.
- No máximo uma encenação de cada vez, e no máximo 3 em sequência por visita. As demais aparecem só como estado final.
- Tocar na tela pula a encenação. Ela nunca bloqueia a navegação nem o registro de atividade.
- Com `prefers-reduced-motion`, as poses ficam estáticas e as cartas aparecem no lugar final, sem voo.
- As animações só rodam com a página visível.
- A cena sabe o que já foi visto pelas colunas `vista_em` (destinatário) e `resultado_visto_em` (remetente) da Jogada. Por isso, na G3, só "carta recebida", "carta aceita" e "carta recusada" têm encenação; Jogadas concluídas ou expiradas aparecem já no estado final, com o carimbo (`decisoes/0035-cartas-e-jogadas.md`).

## 4. Avatar

- Cada usuário tem um único avatar, igual em todos os grupos.
- Quem nunca configurou o avatar recebe uma das 6 folhas padrão do bundle, escolhida de forma estável pelo identificador do usuário.
- A pessoa pode **importar a própria folha de sprites**, num formato documentado publicamente (§4.3).
- Fora da cena, o componente `Avatar` mostra a célula `retrato` da folha no lugar das iniciais. Isso vale para todos, inclusive para quem está no modo App.
- O avatar é só visual: nenhuma folha dá vantagem no Jogo.

### 4.1. Formato

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
- Não precisam de animação própria: virar (espelho instantâneo), o voo e os carimbos da carta (CSS) e o brilho da missão (CSS).

### 4.3. Importação

- O editor em `/perfil/avatar` mostra uma prévia de todas as linhas antes de salvar.
- O navegador confere o tamanho e re-codifica a folha num canvas antes de enviar, o que remove metadados.
- O servidor confere de novo: assinatura PNG, cabeçalho `IHDR` com 256×384 e RGBA de 8 bits, sem `acTL` (APNG) e no máximo 64 KB. O arquivo é recusado se algo falhar.
- O arquivo fica no disco, em `/dados/avatares/`, com nome aleatório, no mesmo padrão das evidências (`arquitetura.md` §11). Nada de nomes vindos do cliente.
- Só membros de um grupo em comum podem baixar a folha. A resposta usa `Content-Type: image/png` e `X-Content-Type-Options: nosniff`.
- A pessoa pode voltar ao avatar padrão a qualquer momento.
- O `Proprietario` e os `Administrador` de um grupo podem redefinir para o padrão o avatar de um membro desse grupo, como medida contra imagens impróprias.
- Ao trocar ou redefinir, o arquivo antigo é apagado.

### 4.4. Arte do bundle

Todo o bundle é CC0, com o arquivo `LICENSE` junto dos sprites. Nada é carregado de servidores externos.

| Peça | Tamanho | Quantidade |
|---|---|---:|
| Folhas padrão de avatar (pele clara, morena e negra; corpo masculino e feminino) | 256×384 | 6 |
| Chão da praça (vista de cima, quadra urbana; ver §1.3), com barra fixa num canto | 180×230 (mestre 1×, mostrado em 2×, ou seja, 360×460) | 1 |
| Moldura de carta (mesma forma, acento coral ou violeta; ver §1.3) | 24×32 | 2 (Provocação, Resposta) |
| Ícone de carta | 16×16 | 9 |
| Enfeite | 16×16 ou 32×32 | 19 (§9.1) |

## 5. Energia

Energia (EN) é a moeda do Jogo. É única por usuário, somando todos os grupos. Ela só serve para comprar cartas.

### 5.1. Como se ganha

| Evento | EN |
|---|---:|
| Atividade verificada | 10 |
| Semana com a meta cumprida (uma vez por pessoa por semana) | 20 |
| Sequência de 4 semanas | 25 |
| Sequência de 8 semanas | 50 |
| Sequência de 12 semanas | 100 |
| Desafio concluído | 75 |
| Missão semanal concluída (§7) | 5 a 15 |
| Rival da semana vencido (§8) | 15 |
| Jogada concluída, para quem recebeu (§6.4) | 1 × o preço da carta (2 × com APOSTA) |
| Jogada concluída, para quem enviou (§6.4) | 0,5 × o preço da carta |

- As sequências seguem a mesma regra das conquistas: a melhor sequência num único desafio, sem somar desafios. Cada marco paga **uma vez por pessoa**, então as sequências rendem no máximo 175 EN no total.
- **Desafio concluído:** um desafio `Encerrado` com todas as semanas que contavam para a pessoa cumpridas. Paga uma vez por desafio.
- **Vários grupos:** a base de 10 EN vale para cada atividade verificada, em qualquer grupo. A meta semanal paga uma vez por pessoa por semana, no primeiro grupo em que for cumprida. Os bônus de §5.2 também contam por pessoa. Assim, estar em mais grupos não multiplica a Energia.

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
| +10 MIN | `Provocacao` | 30 | Uma atividade com pelo menos a duração mínima do desafio + 10 minutos. |
| +20 MIN | `Provocacao` | 45 | Uma atividade com pelo menos a duração mínima do desafio + 20 minutos. |
| VARIEDADE | `Provocacao` | 35 | Uma atividade de tipo diferente da última atividade do destinatário, neste grupo, já verificada no momento do envio. |
| FIM DE SEMANA | `Provocacao` | 35 | Uma atividade que comece no sábado ou no domingo. |
| ANTES DAS 8 | `Provocacao` | 40 | Uma atividade que comece antes das 08:00. |
| DESAFIO DUPLO | `Resposta` | 40 | Aceita a Provocação e devolve o mesmo objetivo para o remetente, como uma Jogada nova. |
| APOSTA | `Resposta` | 25 | Aceita a Provocação. Se a Jogada for concluída, a recompensa do destinatário dobra. Se não for, a carta está perdida. |
| PRAZO EXTRA | `Resposta` | 20 | Aceita a Provocação com 24 horas a mais de prazo. |
| MUDANÇA DE PLANOS | `Resposta` | 25 | Aceita a Provocação trocando o objetivo por um equivalente da mesma faixa de preço, à escolha de quem aceita. |

- **Trocas da MUDANÇA DE PLANOS:** `+10 MIN ↔ VARIEDADE ↔ FIM DE SEMANA` e `+20 MIN ↔ ANTES DAS 8`. A duração-alvo e o tipo a evitar do objetivo novo são fixados no aceite. Trocar para +10 MIN ou +20 MIN exige um desafio ativo. O prazo passa a ser o maior entre o atual e o da carta nova, contado do envio (`decisoes/0036-respostas.md`).
- **DESAFIO DUPLO:** a Jogada devolvida usa a Provocação original, que continua sendo de quem a comprou. A duração-alvo é copiada, o tipo a evitar da VARIEDADE é o de quem vai cumprir e o prazo começa no aceite. A devolução não confere os limites de §6.3, mas exige que quem enviou esteja no modo Jogo.
- Os objetivos usam as regras e o fuso do grupo da Jogada. Sem desafio ativo, as cartas +10 MIN e +20 MIN não podem ser jogadas nesse grupo. A duração-alvo e o tipo a evitar da VARIEDADE são fixados no envio.
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

- Cada pessoa pode escolher um rival por semana no grupo. O rival não precisa aceitar e não perde nada.
- Critério normalizado: vence quem cumprir a meta semanal primeiro (pelo horário de verificação da sessão que completou a meta). Se só um cumprir, esse vence. Se ninguém cumprir, não há vencedor.
- Prêmio: 15 EN e um selo, só para quem escolheu.
- Não há aposta de Energia.
- Na cena, os rivais fazem `provocar` no começo da semana, e o vencedor faz `comemorando` no fim (§3.3).

## 9. Enfeites

Cada Conquista libera um **Enfeite**: um objeto que conta, só de olhar, o que a pessoa conquistou. Quem pedala muito ganha a bicicleta; quem treina cedo ganha o despertador. Os Enfeites são calculados como as conquistas, sem tabela e sem compra.

### 9.1. Conquistas e Enfeites

As conquistas marcadas como **nova** ainda não existem em `ConquistasUsuario` e entram na G6. Todas contam só atividades `Verificada`, somando todos os grupos, com horários no fuso do grupo de cada atividade, como as atuais.

**Constância**, a conquista principal: o Enfeite evolui em vez de se repetir.

| Conquista | Condição | Enfeite |
|---|---|---|
| `Sequencia4` | Sequência de 4 semanas | Muda num vaso |
| `Sequencia8` | Sequência de 8 semanas | Planta com flor (substitui a muda) |
| `Sequencia12` | Sequência de 12 semanas | Árvore pequena em vaso (substitui a planta) |
| `Treinos10` | 10 atividades | Medalha de bronze |
| `Treinos50` | 50 atividades | Medalha de prata (substitui a de bronze) |
| `Treinos100` | 100 atividades | Medalha de ouro (substitui a de prata) |
| `DesafioCompleto` (nova) | Um desafio `Encerrado` com todas as semanas que contavam cumpridas (a mesma regra do desafio concluído, §5.1) | Troféu |

**Tipo de atividade**, uma por tipo, com **10 atividades** daquele tipo. `Outro` não tem conquista.

| Conquista | Tipo | Enfeite |
|---|---|---|
| `RatoDeAcademia` (nova) | `Musculacao` | Haltere |
| `Corredor` (nova) | `Corrida` | Cronômetro |
| `Andarilho` (nova) | `Caminhada` | Cachorro na coleira |
| `Ciclista` (nova) | `Ciclismo` | Bicicleta |
| `Peixe` (nova) | `Natacao` | Boia |
| `Boleiro` (nova) | `Esporte` | Bola |
| `Zen` (nova) | `Yoga` | Tapete de ioga enrolado |
| `PesoDoCorpo` (nova) | `Calistenia` | Paralelas (as barrinhas de chão) |

**Hábito**, para jeitos diferentes de manter a rotina:

| Conquista | Condição | Enfeite |
|---|---|---|
| `Madrugador` | 10 atividades iniciadas antes das 07:00 (já existe) | Despertador |
| `Noturno` (nova) | 10 atividades iniciadas a partir das 20:00 | Poste de luz da praça |
| `FimDeSemana` (nova) | 10 atividades no sábado ou no domingo | Caixa de som portátil |
| `Ecletico` (nova) | Atividades verificadas de 5 tipos diferentes, sem contar `Outro` | Mochila de academia |

- São 19 conquistas e 19 desenhos de Enfeite. A planta e a medalha evoluem, então a cena mostra no máximo 15 objetos diferentes.
- Nenhuma conquista usa distância, ritmo ou carga: só contagem de atividades, horário e semanas cumpridas. Uma pessoa que faz 20 caminhadas tem a mesma chance de um Enfeite que uma pessoa que faz 20 corridas.
- Os nomes e os limites (10 por tipo, 10 por hábito) ficam em constantes, como os demais marcos.
- `Madrugador` e `Noturno` cobrem as duas pontas do dia, para que treinar cedo não seja o único horário premiado (ver §12, item 3).

### 9.2. Vitrine

- A pessoa tem uma **vitrine** com até **3 Enfeites**. A cena mostra o primeiro ao lado do avatar.
- Tocar no avatar mostra a vitrine inteira, junto do acesso à página do membro e às cartas.
- Um Enfeite novo entra sozinho na vitrine se houver espaço. A pessoa reorganiza ou tira Enfeites em `/perfil/avatar`.
- Um Enfeite que evolui troca de nível no mesmo lugar da vitrine.
- Só aparecem Enfeites de quem está no modo Jogo. Quem está no modo App não expõe nada.
- **Enfeite novo:** na próxima visita à cena, o avatar faz `comemorando` e o Enfeite aparece com o mesmo brilho em CSS da missão (§3.3). Os colegas veem o Enfeite já no lugar, sem encenação.

A vitrine é uma lista de **códigos de Enfeite** (a família: `planta`, `medalha`…), e não de conquista, guardada na tabela `avatares`. Assim a evolução mantém o lugar. Um código que não vale mais é ignorado na leitura, e os Enfeites já vistos (`enfeites_vistos`) que a pessoa tirou não voltam sozinhos.

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

- Tabela `rivais`.
- Resultado e recompensa derivados das atividades verificadas.
- Encenação `provocar`.

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
4. **Enfeite e cena com 10 pessoas.** Aberta: implementado com o Enfeite de 16 px ao lado do avatar na escala 2; falta conferir num celular de 360 px se o Enfeite ao lado do avatar não polui o círculo. Se poluir, mostrar só ao tocar no avatar.
5. ~~Limites das conquistas novas~~ — fechada: 10 por tipo (`decisoes/0037-avatar-importado-e-enfeites.md`). Rever com os dados reais.
6. **Atividades antigas de "Treino em casa".** A migração 014 as trocou por `Calistenia`, então elas contam para `PesoDoCorpo`. Recomendação: aceitar, porque quase sempre eram treinos com o peso do corpo.

## 13. Critérios de sucesso

1. Quem usa o Jogo registra treinos pelo menos com a mesma regularidade de antes.
2. Quem joga gasta Energia toda semana, e as cartas geram conversa no grupo.
3. Pessoas com condicionamentos diferentes têm chances parecidas. As cartas de duração são opcionais e podem ser recusadas.
4. Nenhuma mecânica dificulta a meta de outra pessoa.
5. O desafio oficial continua compreensível e confiável. Quem prefere o App não recebe cartas e não percebe diferença, salvo pelos retratos no lugar das iniciais.
