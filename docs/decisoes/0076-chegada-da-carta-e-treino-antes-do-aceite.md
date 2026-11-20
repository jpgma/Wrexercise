# 0076 — A Chegada da carta e o treino antes do aceite

**Status:** DECIDIDA. Muda, em `0075-proximo-treino-e-antes-das-8.md`, a regra "Treinos entre o envio e o aceite não contam nem fazem falhar": agora contam, se cumprem. Muda, em `../gamificacao.md` §3.3, a regra "a encenação nunca bloqueia a navegação nem o registro de atividade": a Chegada bloqueia até a resposta. A FADIGA (`0059`), a Fusão (`0070`) e a carta perdida (`0074`) continuam valendo. Detalhes em `../gamificacao.md` §3.3, §3.5, §6.3 e §6.4.

## Contexto

- A carta recebida aparecia como uma encenação pequena na praça, que dava para pular, e a resposta ficava na Mesa. Muita carta passava despercebida e expirava em FADIGA.
- O mantenedor pediu que a carta recebida apareça na hora, com a mesma encenação aproximada do arremesso, e que a resposta venha antes de qualquer outra ação.
- O risco apontado: quem abre o aplicativo para registrar um treino que acabou de fazer teria de responder antes. Pela `0075`, a janela começava no aceite: esse treino não contaria, nem se cumprisse o objetivo.

## Alternativas consideradas

- **Aviso sem bloqueio (faixa ou selo):** não resolve a carta esquecida. Descartada.
- **Bloquear só a tela inicial ou só a praça:** quem vai direto ao formulário (pelo atalho ou pelo Movr) registra antes de responder. Descartada: o portão vale em qualquer tela, e o servidor também recusa o registro.
- **Janela sobreposta (overlay) em vez de abrir a praça:** guarda o formulário embaixo sem esforço, mas o mantenedor preferiu levar a pessoa à praça do grupo. O formulário já guarda um rascunho no aparelho, então ninguém perde o que digitou.
- **Treino antes do aceite conta e também pode falhar:** quem acabou de treinar sem saber da carta teria de escolher entre recusar (FADIGA) e aceitar para falhar na hora. Descartada.
- **Manter a `0075` (treino antes do aceite não conta):** o treino recém-feito seria desperdiçado em silêncio. Descartada.
- **Uma data de corte nova para a regra:** a regra só acrescenta vitórias, então vale para todas as Jogadas da regra do próximo treino.
- **Prazo de resposta menor que o da carta:** pediria uma coluna e regras novas. Descartada: o portão já força a resposta na próxima abertura.

## Decisão

- **Chegada:** com uma Provocação recebida que espera resposta, em qualquer grupo, qualquer tela do aplicativo leva à praça do grupo da carta (`/grupos/{id}/inicio?chegada={jogadaId}`), em qualquer modo, também no App. Abas, menu e troca de grupo somem até a resposta.
  - A encenação é o arremesso visto do outro lado, com a câmera do Foco: a câmera vai até quem jogou, que gira e arremessa; a carta voa até quem vê, com a câmera atrás; no impacto, o tremor, o nome da carta e uma vibração curta. Quem dorme se levanta e toma o susto, como no Foco.
  - Tocar na praça pula direto para a resposta. Com movimento reduzido, tudo aparece já no fim.
  - A carta cresce até virar o painel da resposta, sem botão de fechar: "Aceitar · +N EN", "Recusar · FADIGA: 3 treinos com −50% EN" (com um toque, sem confirmação) e, embaixo, as Respostas da Mão.
  - Depois do aceite: "Registrar treino" e "Depois". Se um treino já cumpre a carta, o painel diz isso. Depois da recusa: "Entendi".
  - Com mais de uma carta, uma depois da outra, da mais antiga para a mais nova. No fim, a pessoa volta para onde estava, ou para o formulário de treino.
  - O portão não tira a pessoa do formulário de treino aberto quando ela só volta para o aplicativo. Nesse caso, o servidor recusa o registro (`CARTA_SEM_RESPOSTA`), e o botão "Responder a carta" leva à Chegada e de volta ao formulário.
  - Sem a lista de cartas (erro ou sem rede), nada bloqueia.
- **Servidor:** `GET /api/jogadas/pendentes` lista as cartas sem resposta da pessoa, em todos os grupos. `POST /api/grupos/{id}/atividades` responde 409 `CARTA_SEM_RESPOSTA` enquanto houver uma, em qualquer grupo. Anexar evidências e criar rascunhos do Movr continuam livres.
- **Responder** grava `vista_em` (a carta foi vista) e marca como lido o aviso `CartaRecebida` dessa carta.
- **Treino antes do aceite:** a janela das atividades começa no envio. Um treino que começou entre a chegada da carta e o aceite conta se cumpre o objetivo e nunca faz a Jogada falhar. Só os treinos que começam depois do aceite são o próximo treino da `0075`.
  - Nas cartas de duração fora da Fusão, os treinos de antes do aceite somam com o dia do próximo treino, e um dia de antes do aceite também cumpre sozinho.
  - Um treino de antes do aceite que cumpre salva a Jogada mesmo depois de uma falha do próximo treino.
  - Aceitar com um treino já verificado que cumpre conclui a Jogada na hora e avisa quem enviou (`JogadaConcluida`).
  - Antes de aceitar, a Mesa e a Chegada dizem se um treino já feito cumpre a carta (`jaCumpre`).
- **Mesa:** usa o mesmo painel de resposta, sem a confirmação de recusa.
- **Transição:** vale para todas as Jogadas da regra do próximo treino. Só acrescenta vitórias: uma Jogada `Aceita`, `Falhou` ou `Expirada` pode virar `AguardandoVerificacao` ou `Concluida`, e a Energia calculada na leitura só sobe.

## Impacto

- **Backend:** `ObjetivoCarta` (`GetVez` passa a juntar os treinos de antes do aceite com `GetProximoTreino`; `GetFalha` olha só o próximo treino; `JaCumpre`), `AtividadeJogada.AntesDoAceite`, o SQL da janela (`Jogadas.AntesDoAceiteSql` no lugar de `InicioJanelaSql`), `JogadasConcluidas`, `ResponderJogada`, `ListarJogadasPendentes`, o contrato `CartasSemResposta` (usado por `CriarAtividade`) e `NotificacoesJogada.MarcarLidasAsync`. `JogadaResponse` ganha `jaCumpre`. Sem migração.
- **Frontend:** `PortaoChegada` (no `AppLayout`), `ChegadaPagina`, `PainelChegada`, `PainelResposta` (na Chegada e na Mesa), `useFoco.receber`, a animação `carta-cresce`, o aviso do formulário (`getAvisoCarta`) e o erro `CARTA_SEM_RESPOSTA` no formulário.
- **Ritmo do jogo:** a resposta vem na próxima abertura do aplicativo, e o treino recém-feito vale na hora. Reavaliar com `metricas.bat` depois de duas semanas: cartas expiradas sem resposta devem cair perto de zero.
