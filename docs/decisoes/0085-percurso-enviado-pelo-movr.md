# 0085 — Percurso enviado pelo Movr e o algoritmo v1

**Status:** DECIDIDA. Complementa as ADRs 0065, 0081 e 0084. Muda o cálculo dos números do Percurso da ADR 0084.

## Contexto

- O Movr passou a gravar caminhada, corrida e pedal com o GPS do celular, ou do relógio quando ele tem GPS, inclusive com a tela apagada. O navegador não grava com a tela apagada (ADR 0084).
- No treino ao vivo começado pelo Movr (ADR 0081), o grupo via o Avatar e o cronômetro, mas não o trajeto: a ADR 0084 deixava o Percurso só para o navegador.
- Os dois aplicativos calculavam a distância de jeitos diferentes. O Movr usa um filtro de Kalman; o Wrexercise usava o limiar de 5 m entre pontos crus. O mesmo treino tinha uma distância no Movr e outra no grupo, e o tremor do GPS parado ainda somava metros no Wrexercise.

## Alternativas consideradas

- **O Movr manda só o resumo (distância e parciais) e a imagem:** simples, mas o grupo não vê o trajeto ao vivo nem no mapa, e os recordes e as conquistas de distância comparariam números de dois algoritmos. Descartada.
- **O Movr manda um arquivo (GPX) no fim:** sem trajeto ao vivo, e o servidor teria um segundo formato de entrada. Descartada.
- **Cada aplicativo com o seu algoritmo:** a pessoa veria dois números para o mesmo treino. Descartada.
- **O navegador também grava no treino do Movr:** dois aparelhos mandariam pontos para o mesmo percurso, com `seq` e trechos que colidem. Descartada: no treino do Movr, só o Movr grava.

## Decisão

### O Movr manda o Percurso

- Ao começar o treino ao vivo (`PUT /api/integracoes/treinos/{externalId}`), o Movr manda `gravarPercurso: true` na Corrida, na Caminhada e no Ciclismo ao ar livre. O servidor cria o Percurso, como no navegador, e não guarda o local de início. A resposta traz `percursoUltimoSeq`: o maior `seq` já guardado (−1 sem nenhum), ou `null` sem percurso.
- O Movr manda os pontos em lotes a cada 30 s e antes do fim por `PUT /api/integracoes/treinos/{externalId}/percurso/pontos`, com o mesmo corpo, as mesmas regras e os mesmos limites do navegador (500 pontos por lote, 20.000 por treino, repetir um lote não duplica). O token do app (ADR 0065) já permite começar e terminar o treino ao vivo; o Percurso fica no mesmo escopo.
- O Movr numera os pontos a partir de `percursoUltimoSeq + 1`. Quando adota o treino aberto no navegador (ADR 0081), o percurso que o navegador gravou fica, e os pontos do Movr vêm depois.
- No treino do Movr, nenhum navegador grava: o cronômetro mostra "O Movr está gravando o percurso deste treino.", com a distância do servidor, sem "Gravar neste aparelho" e sem "Modo bolso".
- A imagem do percurso continua com o navegador, depois que a pessoa termina o treino aqui.

### O algoritmo v1, igual nos dois aplicativos

- Os números (distância, velocidade máxima, subida e parciais) vêm de `MetricasPercurso.cs`, uma tradução linha a linha do `RouteMetrics` do Movr. Os dois guardam os pontos crus e calculam na leitura:
  1. Precisão pior que 30 m é descartada.
  2. Salto acima da velocidade limite do tipo (6, 10 e 25 m/s) é descartado; depois de 5 descartes seguidos, o percurso recomeça no ponto novo, sem somar o salto.
  3. Filtro de Kalman de velocidade constante, por eixo, num plano local, com a precisão de cada ponto.
  4. Só soma em movimento: velocidade suavizada acima do limiar do tipo (0,5, 1 e 1,5 m/s) e deslocamento em 15 s compatível com ele.
  5. A distância soma cordas do trajeto suavizado a partir de 10 m (20 m no Ciclismo) ou da precisão, tirando o ruído esperado da posição.
  6. Lacuna de 30 s a 2 min com velocidade plausível (3, 7 e 20 m/s) soma a reta (túnel, tela apagada por pouco tempo); mais que isso, ou outro `segmento`, não soma.
  7. Altitude suavizada com histerese de 3 m. Parciais interpoladas na marca exata.
- O desenho do mapa (trechos, lacunas tracejadas, aparo de 200 m e simplificação) continua o da ADR 0084.
- `webapp/src/features/percurso/calculo.ts` tem a mesma distância, para a estimativa ao vivo dos pontos que ainda não subiram.
- Os oito percursos de referência do Movr (`Wrexercise.UnitTests/Features/Atividades/Percursos/`) e os números que o Movr calcula para cada um (`Resultados/movr-v1.json`) ficam nos dois repositórios. Os testes exigem o mesmo número do Movr. Mudar uma regra exige a mesma mudança nos dois aplicativos e nos arquivos de referência, numa versão nova do algoritmo.

## Impacto

- **Números diferentes:** os percursos gravados antes guardam os números antigos até um lote novo, o término ou uma correção refazer o resumo. O tremor do GPS parado deixa de somar. Pontos sintéticos perfeitos (sem ruído) ficam uns 6% abaixo da reta, porque o v1 desconta o ruído que a precisão anuncia; com o ruído real, a distância fica perto da verdade (os percursos de referência, também com um ponto a cada 3 s, como o navegador).
- **Lacunas curtas somam:** a ADR 0084 dizia que a lacuna não conta distância. Agora uma lacuna de até 2 min com velocidade plausível soma a reta; o desenho continua tracejado.
- **Recordes e conquistas** passam a usar os números do v1 conforme os resumos são refeitos.
- **Privacidade:** as mesmas regras da ADR 0084. O Movr só manda o percurso quando a pessoa escolheu ir ao vivo, e o grupo nunca vê os primeiros e os últimos 200 m.
- Relacionadas: 0065, 0080, 0081, 0083, 0084.
