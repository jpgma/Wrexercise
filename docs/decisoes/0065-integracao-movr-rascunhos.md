# 0065 — Integração com o Movr por rascunhos

**Status:** DECIDIDA. Tira de `docs/arquitetura.md` §19 só esta integração; Strava e wearables continuam fora.

## Contexto

- O mantenedor também mantém o Movr, um app Android de treino de calistenia (Avalonia, offline-first). Ao terminar um treino no Movr, a pessoa abria o Wrexercise e digitava de novo tipo, duração e horário, e muitas vezes tirava um print da tela de resumo como comprovante.
- O pedido: vincular a conta do Movr à do Wrexercise e, no fim do treino, tocar em "Postar Atividade" para abrir o registro do Wrexercise já preenchido e com a imagem do resumo, pronto para a pessoa juntar uma selfie, ajustar e enviar. Se o PWA estiver instalado, ele abre.
- O Wrexercise só aceitava escritas com o cookie de sessão e a mesma origem (`OriginCheck`), não tinha tokens de API nem preenchimento do formulário por link, e um link protegido perdia o destino quando a pessoa não estava logada.
- Uma URL não carrega uma imagem.

## Alternativas consideradas

- **O app posta a atividade direto:** tira a pessoa do processo e pula a revisão do que vai para o grupo. A verificação comunitária parte de quem envia; um app não decide por ela. Descartada.
- **Web Share Target no manifest:** o Movr compartilharia a imagem e um link. Só funciona com o PWA instalado e obriga a escolher o Wrexercise na folha de compartilhamento a cada treino. O "vincular conta" seria só uma preferência local. Descartada.
- **Servidor do Movr guarda o token e repassa:** mais peças (chaves de proteção de dados no celular servidor do Movr) e dependência dos dois servidores ao mesmo tempo. Descartada.
- **OAuth 2.0 completo com biblioteca:** dependência nova, refresh tokens e escopos genéricos para um único app conhecido. Descartada.
- **Rascunho criado pelo app com um token restrito, conexão com PKCE:** escolhida.

## Decisão

- **Conexão (PKCE S256, RFC 7636):**
  - O Movr abre `/integracoes/movr/autorizar?code_challenge&state&redirect_uri`. Sem sessão, a pessoa entra e volta ao mesmo link: o guard agora guarda `redirectTo` com o caminho e a busca.
  - A tela explica o que o Movr pode e o que não pode. "Permitir" chama `POST /api/integracoes/autorizacoes` (com cookie), que devolve um código de uso único, válido por 5 minutos, e o navegador abre `movr://integracoes/wrexercise?code&state`. Um botão "Voltar ao Movr" cobre o navegador que não abre o app sozinho.
  - O endereço de retorno é de uma lista fechada: `movr://integracoes/wrexercise` e, para o computador de desenvolvimento, `http://127.0.0.1:{porta}/wx-callback` (RFC 8252).
  - O app troca o código em `POST /api/integracoes/token` com o verificador. Um verificador errado queima o código. Conectar de novo revoga a conexão anterior do mesmo app.
- **Token do app:** opaco (`OpaqueToken`), guardado só como hash em `apps_conectados`, sem validade até ser revogado. Vale apenas em `/api/integracoes/*` e nunca junto do cookie de sessão. Só permite criar e consultar rascunhos e revogar a si mesmo.
- **Proteção de origem:** `OriginCheck` deixa passar as escritas em `/api/integracoes/*` que chegam **sem** o cookie de sessão. Sem cookie, o navegador não empresta a identidade de ninguém; com cookie, a regra de mesma origem continua valendo.
- **Rascunho (`Rascunho`):**
  - `PUT /api/integracoes/rascunhos/{externalId}` (multipart): tipo, duração, início, fim, a imagem do resumo (só foto, até 5 MB) e a miniatura JPEG (até 100 KB). O identificador do treino no app torna o envio idempotente: repetir troca os dados e a imagem do mesmo rascunho.
  - O rascunho vale até 48 horas depois do início, o mesmo prazo do registro (ADR 0052). Cada envio apaga os rascunhos vencidos e não postados de quem envia, sem serviço periódico.
  - `GET /api/integracoes/rascunhos/{externalId}` devolve `Pendente`, `Postada` ou `Expirada` e o caminho a abrir. O app mostra "Postada no WX ✓" sem criar outro rascunho.
- **Importar e postar:** o app abre `/importar/{id}`. Com um grupo só, a tela segue direto para `/grupos/{id}/atividades/nova?rascunho={id}`; com mais de um, a pessoa escolhe o grupo. O formulário vem preenchido, mostra a imagem do resumo travada na primeira vaga com o selo do app e deixa duas vagas para fotos e vídeos. A pessoa ainda pode mudar tudo. O rascunho local do formulário (ADR 0058) tem uma chave própria para cada importação.
- **Primeira evidência:** `POST /api/grupos/{id}/atividades` aceita `rascunhoId` (com `Fotografia`). Na mesma transação, o servidor copia a imagem e a miniatura do rascunho para a atividade, grava a evidência com a origem `Integracao` e a data do fim do treino, calcula os avisos como para `Exif` e liga o rascunho à atividade. Apagar a atividade (ADR 0054) devolve o rascunho a `Pendente`; a cópia da atividade sai com ela.
- **Origem `Integracao`:** só o servidor a atribui. O navegador não consegue enviá-la em `AnexarEvidencia`.
- **PWA:** `launch_handler.client_mode = navigate-existing` reaproveita a janela aberta quando o Movr abre outro link. O Android abre os links do escopo no PWA instalado (WebAPK); sem ele, no navegador.
- **Configurações:** a seção "Apps conectados" lista as conexões ativas e permite desconectar.
- **Grupo:** o app não vê os grupos da pessoa; a escolha do grupo é sempre no Wrexercise.

## Impacto

- **Migração 037:** `codigos_integracao`, `apps_conectados`, `rascunhos_atividade` e a origem `Integracao` em `evidencias_atividade`.
- Fatia nova `Features/Integracoes/`. `Features/Atividades/EvidenciaRascunho.cs` lê e liga o rascunho por SQL explícito.
- Novas telas: `IntegracaoAutorizarPage` e `ImportarRascunhoPage`. O guard de sessão guarda o destino de qualquer link protegido.
- **Risco aceito:** o token não expira. Ele só cria rascunhos, que a pessoa revê antes de postar, e qualquer lado o revoga. Quem tiver o token do celular pode criar rascunhos falsos, mas não postar nem ler dados do grupo.
- **Risco aceito:** no celular, outro app poderia registrar o esquema `movr://` e receber o código. Sem o verificador do PKCE, que só o Movr tem, o código não vale nada.
- A imagem do resumo é dado pessoal como qualquer evidência: não entra em log e vai para o backup com a pasta de evidências.
