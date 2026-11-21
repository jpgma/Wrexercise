# 0081 — Treino ao vivo começado pelo Movr

**Status:** DECIDIDA. Amplia o token do app conectado da ADR 0065 e acrescenta à ADR 0078 o treino ao vivo começado por um app.

## Contexto

- A ADR 0078 trouxe o treino ao vivo: o grupo vê quem está treinando agora. Quem treina pelo Movr só aparecia depois, quando postava o rascunho (ADR 0065).
- O token do Movr só criava e consultava rascunhos. As rotas do treino ao vivo exigem o cookie da sessão do navegador.
- O treino ao vivo começa no grupo escolhido, mas a ADR 0065 dizia que o app não vê os grupos da pessoa.
- No Movr o treino tem pausas. O tempo que vale aqui é o que a pessoa informa ao terminar, e ele só diminui.

## Alternativas consideradas

- **Um segundo token ou um novo escopo:** obrigaria a reconectar o Movr e guardar escopos por conexão. Rejeitada pela pessoa.
- **O Movr termina o treino sozinho:** quebra a regra da ADR 0065, de que quem envia é sempre a pessoa, e tira a chance de pôr fotos.
- **Tabela própria `treinos_app` ligada a `atividades`:** mais uma escrita de outra fatia no começo do treino, sem ganho.
- **Colunas do app em `atividades`:** escolhida.

## Decisão

### O que o token passa a permitir

O mesmo token de `apps_conectados`, sem reconectar, também:

- lista o nome e o identificador dos grupos da pessoa (`GET /api/integracoes/grupos`);
- começa o treino ao vivo no grupo escolhido, com o identificador do treino no app (`PUT /api/integracoes/treinos/{externalId}`, com `grupoId`, `tipo` e, se a pessoa deixou, `latitude` e `longitude`);
- informa o fim do treino no app: a duração ativa, sem pausas, e o horário do fim, como **sugestão**, e opcionalmente a imagem do resumo como evidência (`PUT /api/integracoes/treinos/{externalId}/fim`);
- cancela o treino enquanto ele está `EmAndamento` (`DELETE /api/integracoes/treinos/{externalId}`);
- consulta a situação (`GET /api/integracoes/treinos/{externalId}`).

O token continua sem poder terminar, postar, ler atividades, torcidas ou comentários, ou apagar uma atividade que não esteja `EmAndamento`.

### Um treino ao vivo por treino do app

- `atividades` ganha `app`, `app_external_id`, `app_duracao_segundos` e `app_ended_at` (migração 042), com índice único em `(usuario_id, app, app_external_id)`. O vínculo fica depois de terminar: o app ainda consulta a situação.
- Repetir o começo com o mesmo identificador devolve o mesmo treino (200). Depois de terminado, `409 TREINO_NAO_EM_ANDAMENTO`.
- Um treino aberto no navegador, no mesmo grupo e sem app, passa a ser do app. Um treino aberto por outro treino do app, ou em outro grupo, devolve `409 TREINO_EM_ANDAMENTO`.
- As travas do começo são as do navegador (`AVALIACOES_PENDENTES`, `CARTA_SEM_RESPOSTA`), e o grupo recebe o mesmo aviso `ComecouTreinar`.

### O fim no app é sugestão

- O servidor guarda a duração ativa limitada ao tempo decorrido aqui, e o fim entre o início e agora. Repetir o envio atualiza a sugestão.
- A imagem do resumo entra uma vez só, como evidência com a origem `Integracao` e a data do fim, enquanto houver vaga (10). Os avisos de data são os do navegador durante o treino.
- O cronômetro mostra "Movr: treino concluído" com o tempo ativo. "Terminar treino" vem com os minutos sugeridos. A pessoa ainda pode diminuir, até o decorrido. A regra da ADR 0078 de que a duração só diminui não muda.
- 30 minutos depois do fim informado, a pessoa recebe uma vez o aviso `TreinoAppConcluido` ("Seu treino do Movr acabou. Põe as fotos e termine aqui"). Os 3 horas do `TreinoLongo` e a expiração de 24 horas continuam.

### Local

O app manda `latitude` e `longitude` só quando a pessoa ligou o treino ao vivo automático no Movr e permitiu a localização aproximada. É a escolha de mostrar ao grupo da ADR 0080. Coordenadas inválidas não impedem o começo.

## Impacto

- **Migração 042:** colunas do app em `atividades` e o tipo de notificação `TreinoAppConcluido`.
- `Features/Atividades/Contracts/TreinoAoVivoApp.cs` expõe à fatia Integracoes o começo, o fim, o cancelamento e a consulta. `ComecarTreino.ExecutarAsync` recebe o vínculo do app. `EvidenciaIntegracao.cs` grava a evidência `Integracao` do rascunho e do treino ao vivo.
- Rotas novas em `Features/Integracoes/`, com o mesmo limite de 20 por minuto por IP.
- A tela "Conectar o Movr" diz que o Movr vê o nome dos grupos e começa o treino ao vivo.
- **Risco aceito:** quem tiver o token do celular vê o nome dos grupos da pessoa e pode começar ou cancelar o treino ao vivo dela. Não pode terminar nem ler nada do grupo. Qualquer lado revoga o token.
- Relacionadas: 0065, 0078, 0080.
