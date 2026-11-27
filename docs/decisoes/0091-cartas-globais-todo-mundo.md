# 0091 — Cartas Globais e a Convocação do TODO MUNDO

**Status:** DECIDIDA. Acrescenta uma terceira categoria de carta (`Global`) a `gamificacao.md` §6. Reusa as regras de FADIGA da `0089-fadiga-visivel-e-recusa-com-peso.md`.

## Contexto

As cartas são todas de um para um: uma Provocação vai de uma pessoa para outra, e a tabela `jogadas` exige um `destinatario_id`. O mantenedor pediu uma carta que vale para o grupo inteiro, cara o bastante para dar um motivo de juntar Energia por mais tempo, e que vire um evento social: "Essa semana todo mundo vai de Ciclismo!".

A especificação original pedia preferências de modalidade por pessoa, nenhuma FADIGA e uma devolução fixa de 15 EN por pessoa, até 75 EN. Na conversa, o mantenedor mudou essas regras (ver **Decisão**).

## Alternativas consideradas

- **Guardar a Convocação na tabela `jogadas`:** exigiria destinatário nulo e mudaria limites, Chegada, Revide e Devolução. Descartada: a Convocação tem tabela própria.
- **Preferências de modalidade por pessoa (só tipos que todos topam):** num grupo pequeno, uma pessoa trava todas as opções. Descartada por ora: a ideia é empurrar a galera para coisas novas.
- **Convite sem consequência (sem FADIGA):** a recusa fica "de graça", e a carta perde peso. Descartada: o convite funciona como uma Provocação recebida.
- **Devolução fixa de 15 EN, até 75:** em grupos pequenos, quase nada volta. Descartada: o preço é dividido entre os convidados.
- **Chegada obrigatória e bloqueio do registro de treino:** pesado demais para um evento de 7 dias. Descartada: o convite fica no painel da praça e numa notificação.

## Decisão

**Carta:** TODO MUNDO, tipo `Global`, 150 EN, na Loja. Ocupa uma vaga da Mão (limite de 5). Não entra em Fusão, não aceita Resposta e não é jogada em `/jogadas` (`CARTA_INVALIDA`). É gasta quando cria a Convocação; o preço não é cobrado de novo.

**Convocar** (`POST /api/grupos/{id}/convocacoes`, `{ cartaId, tipo }`):

- Quem convoca (o anfitrião) é membro do grupo, está no modo Jogo, tem a carta na Mão e não está com FADIGA (`FADIGA_ATIVA`).
- Uma Convocação dentro do prazo por grupo (`CONVOCACAO_ATIVA`). A que só espera verificação não bloqueia a próxima.
- O tipo é qualquer tipo de atividade, menos `Outro` e `Esporte` (genéricos demais). Não há preferências de modalidade.
- Pelo menos um colega no modo Jogo (`CONVOCACAO_SEM_CONVIDADOS`).
- Uma transação trava o grupo e depois o anfitrião; `carta_id` é único em `convocacoes`. A falha não grava nada, e a carta continua na Mão.
- **Retrato:** os convidados são o anfitrião e os membros ativos com `modo_inicio = 'Jogo'` naquele instante. Quem entra no grupo ou no modo Jogo depois fica de fora. Sair do modo Jogo depois não muda nada (o modo é da conta, não do grupo).
- O prazo é 7 dias depois da criação, sem alinhar com a semana do desafio. O tipo e o prazo não mudam.
- Cada convidado, menos o anfitrião, recebe a notificação `Convocacao` (lista e celular). Não há lembretes.

**Responder** (`PUT /api/grupos/{id}/convocacoes/{convocacaoId}/participacao`, `{ decisao }`), só antes do prazo:

- Sem resposta: `Aceita` ("Tô dentro!") ou `Recusada` ("Dessa vez não"). A recusa é final.
- Depois de aceitar: `Desistiu` ("Sair"), final, e só sem treino que conta ou ainda pode contar (`TREINO_JA_CONTA`).
- O anfitrião já participa e não sai.

**Objetivo:** um treino da pessoa, no grupo, do tipo escolhido, com `started_at` entre a criação (inclusive) e o prazo (exclusive), `Verificada`. A pessoa precisa ter aceitado antes do prazo. Um treino de antes do aceite também conta (como na decisão 0076). A verificação pode vir depois do prazo; o treino precisa começar antes. Um treino rejeitado não conta, mas outro pode. Outros tipos nunca fazem falhar. Não há regra de próximo treino nem duração mínima própria.

**Situações**, calculadas na leitura, sem tarefa agendada:

- Convocação: `Ativa`, `AguardandoVerificacao` (passou do prazo, com treino ainda em verificação) e `Encerrada`.
- Participação: `Pendente`, `Participando`, `AguardandoVerificacao`, `Concluida`, `Recusada`, `Desistiu`, `Falhou`, `Expirada` (sem resposta no prazo) e `ForaDoGrupo` (saiu do grupo antes de um resultado final).

**Para o convidado, funciona como uma Provocação recebida** (decisão 0089): `Recusada` dá as cargas da recusa (3, ou +1 até 5); `Expirada` dá as da expiração (3); `Falhou` e `Desistiu` dão as da falha (2). A falha acontece no prazo, ou quando o último treino que ainda podia contar é rejeitado (pelo histórico de revisão). Cumprir tira 1 carga. Quem está com FADIGA pode ser convidado e participar. O anfitrião não ganha FADIGA na própria Convocação. `ForaDoGrupo` não dá FADIGA.

**Energia** (componente `Convocacoes` em `EnergiaGanha`):

- Quem conclui ganha 30 EN, uma vez por Convocação, o anfitrião também.
- **Devolução ao anfitrião:** o preço pago é dividido entre os outros convidados; a parte é `floor(preço / outros)`. `Concluida`, `Recusada` e `ForaDoGrupo` devolvem a parte inteira; `Expirada`, `Falhou` e `Desistiu` devolvem metade dela, arredondada para baixo, como a Devolução da Provocação (decisão 0087). Resolvido tudo, volta também a sobra da divisão: o preço menos o que ficou nas metades. Com 3 outros: 50 cada, ou 25 de quem não cumpre. Com 9: 16 cada, ou 8.
- **Por que metade** (revisão de 27/11/2026, antes da primeira implantação): com a devolução inteira em qualquer resultado, o anfitrião nunca perdia nada (+30 EN se cumprisse, 0 se não), e a carta virava uma FADIGA de graça para o grupo todo: bastava escolher um tipo que só o anfitrião faz (em produção, Ciclismo e Yoga tinham uma pessoa cada). Com a metade, esse risco custa até 25 EN por convidado num grupo de 4. A recusa continua devolvendo tudo, como na Provocação (decisão 0089): quem paga pela recusa é quem recusa. O preço de 150 EN fica: com a renda de 90 a 115 EN por semana vista em produção, são cerca de 1,5 semana guardando Energia.
- Sem Revanche e sem a devolução das Provocações.

**Na interface:** a seção "Globais" na Loja (selo amarelo "Global"; o violeta já é da Resposta). Na Mão, tocar na carta fora do Foco abre "Convocar a galera", com o tipo, os convidados e as recompensas. Na praça, acima da Mão, o painel da Convocação ativa, ou da última encerrada há até 3 dias, com "Tô dentro!", "Dessa vez não · FADIGA" e "Sair". O detalhe e as "Convocações anteriores" ficam em `/grupos/{id}/convocacoes/{convocacaoId}`.

## Impacto

- Migração `047-create-convocacoes.sql`: `convocacoes`, `participantes_convocacao`, o código `TodoMundo` em `cartas` e o aviso `Convocacao`.
- `EstadoJogo` carrega as Convocações da pessoa: gastam a carta, rendem Energia e entram na FADIGA. A praça mostra "TODO MUNDO" e o anfitrião como motivo da FADIGA.
- A arte (`carta-global.png` e `carta-todomundo.png`) foi gerada no ChatGPT e passa pelo `pixel-art/cartas/build.py`, como as outras: a moldura amarela fica no código (`moldura_global`), e o ícone é reduzido de `fonte/todo-mundo.png`. Os prompts estão em `pixel-art/cartas/prompts-gemini.md`.

## Casos de borda aceitos

- **A Energia já ganha não diminui** com resultados novos: a recompensa e a devolução só crescem. Como nas Jogadas, se o treino que concluiu for rejeitado ou anulado depois, a recompensa sai.
- Um treino rejeitado depois do prazo dá a FADIGA da falha no instante da rejeição. Um treino anulado não deixa esse instante; a falha fica no prazo.
- A devolução do anfitrião chega aos poucos: enquanto alguém está pendente ou em verificação, a parte dele não volta.
