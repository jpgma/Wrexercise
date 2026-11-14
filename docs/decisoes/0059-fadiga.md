# 0059 — FADIGA: consequência por recusar ou falhar uma carta

**Status:** DECIDIDA. Muda o princípio 5 de `gamificacao.md` §1.1 ("recusar é sempre grátis e não tem consequência") e complementa `0033-regras-de-energia-e-jogadas.md`, `0035-cartas-e-jogadas.md` e `0040-meta-diaria.md`.

## Contexto

Recusar uma Provocação era grátis e sem consequência, então recusar sempre era a escolha segura, e a carta perdia peso. O mantenedor pediu uma consequência que:

- não tire Energia já ganha nem deixe o saldo negativo;
- não torne o treino mais difícil nem mexa no progresso oficial;
- desapareça só com treino, para que a punição empurre de volta ao exercício.

A Energia não tem carteira nem livro-razão: `CalculoEnergia` recalcula tudo a cada leitura, e a regra é que a Energia ganha só cresce (`gamificacao.md` §5.3). A situação das Jogadas também é calculada na leitura, e a expiração não tem evento nem tarefa agendada.

## Alternativas consideradas

- **Contador gravado** (`fadiga_restante` de 0 a 3 no usuário, decrementado ao verificar um treino): é o que o pedido sugeria. Não serve aqui. A base de cada dia é recalculada sempre, então o contador não diz quais dias foram reduzidos, e a Energia antiga mudaria na leitura seguinte. Também exigiria gravar na expiração, que não tem momento de escrita, e travas para não decrementar duas vezes.
- **Tabela dos dias reduzidos e coluna de gatilho em `jogadas`:** funciona, mas precisa de migração, de gravação preguiçosa na leitura para as expirações e de cuidado com concorrência. É mais código para o mesmo resultado.
- **Tirar Energia do saldo na recusa:** viola o pedido e o princípio 3.
- **FADIGA calculada na leitura:** escolhida. Segue o padrão do Rival da semana (`0038`), que ordena os eventos pelo `verified_at`.

## Decisão

- **Gatilho**, só para quem recebeu a Provocação:
  - `Recusada`, no `respondida_em`;
  - `Expirada` sem resposta, ou aceita sem cumprir, no `expira_em`.
- **Efeito:** os próximos 3 dias com atividade verificada, em cada grupo (os mesmos dias que pagam a base desde a `0040`), rendem `PorDia / 2` = 5 EN. Cada dia gasta uma carga.
- **Não acumula nem renova.** Um gatilho com cargas restantes não muda nada. A FADIGA não expira com o tempo.
- **Cálculo:** a função pura `Fadiga.Calcular` percorre, em ordem de tempo, os gatilhos e os dias verificados. Cada dia entra no `verified_at` da primeira atividade verificada dele. Num empate, o dia vem antes do gatilho. `EnergiaGanha` ganha a parcela `Fadiga` (negativa), e `GET /api/jogo/energia` devolve `detalhamento.fadiga` e `fadigaRestante`.
- **A Energia já ganha não diminui:** um dia só tem FADIGA se foi verificado depois do gatilho. Antes de o gatilho existir, nenhuma leitura podia ter pago a base inteira a esse dia. `Fadiga.Inicio` (13/11/2026) ignora recusas e expirações anteriores à implantação, para não reduzir dias já pagos.
- **Dia anulado** (`0054`): o `verified_at` fica gravado, então o dia continua gastando a carga, mas não rende. Assim, a carga não passa para um dia seguinte já pago.
- **Idempotência e concorrência:** nada é gravado. Repetir a recusa (que já devolve `JOGADA_ENCERRADA`), reler a Energia ou enviar o mesmo treino duas vezes (a chave de idempotência da `0053` e a base por dia já evitam o crédito em dobro) não muda o resultado.
- **Interface:**
  - selo `FADIGA` com uma bateria fraca em pixel (`pixel-art/fadiga/build.py`) e os treinos restantes;
  - confirmação antes de recusar;
  - os dois resultados na carta recebida;
  - avisos quando o número muda, guardado no cookie comum `wx_fadiga` (o `localStorage` é bloqueado pelo ESLint).

## Casos de borda aceitos

- **Expiração sem resposta conta.** Quem recebe cartas e não abre o aplicativo fica com FADIGA. Isso relaxa o princípio 3: uma carta enviada pode reduzir a Energia futura de outra pessoa, se ela a ignorar. A proteção são o não acúmulo e os limites de envio (`gamificacao.md` §6.3: 1 carta por par a cada 24 horas, 2 recebidas ativas). Revisar se houver abuso.
- **Aceita, atividade pendente depois do prazo e depois rejeitada.** A Jogada passa de `AguardandoVerificacao` a `Expirada`, com gatilho no `expira_em`. Os dias verificados nesse intervalo, que já rendiam 10 EN, passam a render 5 EN. Isso é raro e decorre de uma comprovação rejeitada.
- **Jogada concluída e depois anulada** (`0054`): a mesma coisa. A FADIGA aparece de forma retroativa, como a recompensa da carta desaparece.

## Impacto

- Sem migração. `EstadoJogo` faz uma consulta a mais (os dias verificados, inclusive os anulados) e passa os gatilhos das Jogadas que já carrega.
- Nada muda nos limites, prazos, recompensas e na volta da carta à Mão, nem na verificação, nas metas, nas sequências e na classificação.
- Testes: `FadigaTests` (regra pura) e `FadigaRoutesTests` (recusa, expiração, falha, não acúmulo, dias com metade, mesmo dia, dia anulado).
