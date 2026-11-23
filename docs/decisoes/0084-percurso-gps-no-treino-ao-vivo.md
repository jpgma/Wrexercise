# 0084 — Percurso: o GPS no treino ao vivo

**Status:** DECIDIDA. Reabre o item "Rastreamento GPS" de `arquitetura.md` §19 e revê a regra de `gamificacao.md` §9.1 de que nenhuma conquista usa distância. Complementa as ADRs 0061, 0078 e 0080. O cálculo dos números e o percurso do Movr mudaram na ADR 0085.

## Contexto

- Na Corrida, na Caminhada e no Ciclismo, o grupo quer ver o caminho, a distância e o ritmo, não só um ponto de início (ADR 0080).
- `arquitetura.md` §19 deixava o rastreamento GPS de fora, e `gamificacao.md` §9.1 dizia que nenhuma conquista usa distância: quem caminha teria a mesma chance de quem corre.
- O aplicativo é um PWA. Com a tela apagada ou o app em segundo plano, o navegador para de entregar a posição (no iOS e, quase sempre, no Android). Não dá para gravar escondido no bolso como um app nativo.
- O ponto de início e o fim de um percurso mostram, quase sempre, onde a pessoa mora.
- A regra de mapas da ADR 0080 continua: o navegador nunca fala com o servidor de mapas.

## Alternativas consideradas

- **Só no app nativo (Movr):** grava em segundo plano, mas deixa de fora quem não usa o Movr. Fica para depois: o Movr pode mandar o percurso dele pela integração.
- **Distância que pontua (metas, Energia):** quebra a igualdade entre os tipos de treino e confia num sinal fácil de forjar. Descartada.
- **Percurso só para o autor, sem mapa para o grupo:** protege a casa, mas tira a graça de ver o colega ao vivo. Descartada; no lugar dela, o aparo de 200 m.
- **Gravar o percurso como um arquivo (blob) por atividade:** um campo só, mas o envio em lotes, a repetição de um lote e o corte no fim informado viram código próprio. Escolhida a tabela de pontos.

## Decisão

### Gravação

- Só no treino ao vivo começado no aplicativo, nos tipos `Corrida`, `Caminhada` e `Ciclismo`.
- Em "Começar agora", a caixa "Mostrar ao grupo o local do treino" vira, nesses tipos, "Gravar o percurso e mostrar ao grupo", marcada por padrão. Desmarcada, o GPS fica desligado. Marcada, o ponto de início da ADR 0080 não é guardado: ele mostraria o que o percurso esconde.
- O aparelho que começou o treino grava com `watchPosition` (alta precisão), guarda os pontos no IndexedDB e os manda em lotes, a cada 30 segundos, para `POST /api/atividades/{id}/percurso/pontos`. Outro aparelho só grava se a pessoa tocar em "Gravar neste aparelho".
- Gravando, a tela fica acesa ("Manter a tela acesa" ligado e travado). O "Modo bolso" é um modo ambiente, como o de um relógio: tela preta pura (apagada no OLED), números em branco (hora, distância, cronômetro, ritmo e estado do GPS), por cima de tudo e segurando todos os toques. Pede a tela cheia e trava a tela em pé onde o navegador deixa (no iPhone, não há tela cheia). Os números mudam alguns pixels a cada minuto, para não marcar a tela. O Voltar não sai; sai segurando "Segure para sair" por 2 segundos.
- Cada volta à tela (ou recarga do app) começa um trecho novo (`segmento`). A lacuna aparece tracejada e não conta distância.
- O aparelho descarta pontos com precisão pior que 30 m e guarda um ponto a cada 3 s ou 5 m. Antes de "Terminar treino", manda o que falta.

### Dados e regras

- `percursos` (o resumo, uma linha por atividade que grava) e `pontos_percurso` (os pontos crus, com chave `(atividade_id, seq)`): repetir um lote não duplica nada. Migração 045.
- O filtro é feito na leitura, em C# (`PercursoAtividade.cs`): pontos imprecisos, tempo voltando, saltos impossíveis para o tipo (6, 10 e 25 m/s), lacunas de mais de 30 s. Três pontos seguidos e coerentes depois de um salto viram um trecho novo. Parado, o tremor do GPS não soma: só conta um deslocamento de 5 m ou da precisão média, o que for maior.
- Só contam os pontos dentro de [início, início + duração]. Diminuir a duração ao terminar, ou numa `Correcao`, também corta o percurso.
- O resumo guarda a distância, a velocidade máxima (em janelas de 10 s) e a subida aproximada. O ritmo médio vem da duração da atividade. As parciais são de 1 km (5 km no Ciclismo).
- O servidor é a referência. O aparelho só estima a distância dos pontos que ainda não subiu, com uma cópia das regras em `features/percurso/calculo.ts`.
- Limites: 500 pontos por lote, 20.000 por atividade (`PERCURSO_CHEIO`), 20 lotes por minuto por IP.

### Quem vê o quê

- O grupo vê o percurso ao vivo e depois, sem os primeiros e os últimos 200 m em linha reta. Ao vivo, o fim é onde a pessoa está: o grupo vê o trajeto uns 200 m atrás. Um percurso que fica todo perto do começo ou do fim aparece sem mapa.
- O autor vê tudo, com o lembrete do que o grupo não vê.
- Os tiles vêm de `GET /api/atividades/{id}/percurso/mapa/{z}/{x}/{y}`: zoom de 12 a 17, só os tiles que tocam o percurso que quem pede vê, com um de margem, e no máximo 36. O cache e o servidor de mapas são os da ADR 0080.
- A praça mostra a distância de quem treina ao vivo no `Foco`; tocar nela abre o mapa.

### A imagem do percurso

- Depois de "Terminar treino", o navegador desenha a imagem (mapa aparado, distância, tempo, ritmo ou velocidade, máxima, subida, parciais, recorde, Torcidas e parceiro) e a manda para `POST /api/atividades/{id}/percurso/imagem`.
- Ela vira evidência com a origem `Percurso`, que só o servidor atribui, e o selo "Percurso". Uma por atividade, dentro do limite de 10.
- Ela nunca verifica a atividade nem muda a comprovação: um treino só com a imagem do percurso continua `RegistroManual`. O GPS é um sinal do aparelho, não prova (ADR 0052).
- O estilo segue o modo da tela inicial: no `App`, o cartão da identidade; no `Jogo`, moldura e trajeto em pixel, com o Avatar comemorando no fim.

### O que o Percurso não faz

- A distância nunca entra no progresso, na Energia, na classificação nem nas cartas. Um teste de arquitetura impede que outras fatias leiam `percursos`, fora a Cena e as conquistas.
- **Recordes pessoais** (só um selo): a maior distância e o melhor ritmo (ou velocidade) do tipo, em treinos de 1 km ou mais (5 km no Ciclismo), contra os próprios treinos com percurso não rejeitados nem anulados. O primeiro treino com percurso não tem recorde.
- **Treinaram juntos:** quando dois treinos ao vivo com percurso, de pessoas do mesmo grupo, se sobrepõem por 15 min ou mais e ficam a até 50 m em 60% das amostras (a cada 30 s), os dois mapas mostram o trajeto do parceiro. A Parceria da Energia continua só pela sobreposição de tempo (ADR 0078).
- **Conquistas de distância** (só reconhecimento): `Fundista` (50 km de Corrida), `Peregrino` (50 km de Caminhada) e `Estradeiro` (200 km de Ciclismo), somando o percurso das atividades verificadas. Cada uma libera um Enfeite: "Marco de quilômetro", "Placa de trilha" e "Bicicletário". Os alvos levam em conta o esforço de cada tipo.

## Impacto

- **Privacidade:** a posição só sai do aparelho com a caixa marcada. O grupo nunca vê o começo nem o fim. Coordenadas e tiles nunca vão para o log. Os pontos somem com a atividade (cancelar, expirar, apagar) e, com a conta, entram em Q029.
- **Armazenamento:** uns 2.400 pontos (≈ 100 KB no banco) por 2 h de treino. A imagem tem no máximo 5 MB, como uma foto.
- **Servidor:** cada lote refaz o resumo a partir dos pontos crus; com 20.000 pontos no máximo, é leve.
- **Bateria:** o GPS e a tela acesa gastam bateria. A pessoa escolhe gravar a cada treino.
- **Limite do PWA:** com a tela apagada, o percurso tem lacunas. O texto do cronômetro avisa.
- Relacionadas: 0052, 0061, 0065, 0078, 0080, 0081.
