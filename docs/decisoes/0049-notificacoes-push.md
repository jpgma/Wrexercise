# 0049 — Notificações push

**Status:** DECIDIDA. Revisa a `0020-notificacoes-no-aplicativo.md` (que não tinha push) e o item de `arquitetura.md` §19.

## Contexto

As notificações só aparecem no sino quando a pessoa abre o app. Treinos esperam avaliação por até 48 horas, a fila bloqueia o registro de quem não avaliou (`0047`) e uma carta tem prazo. Sem aviso no telefone, quase tudo isso é descoberto tarde. O Wrexercise já é um PWA com Service Worker (`0021`), e o Web Push funciona no Android (navegador ou app instalado) e no iOS 16.4 ou mais novo (só no app da Tela de Início).

Antes do push, a `0048` tirou os avisos que só confirmavam o caminho esperado.

## Alternativas consideradas

**Como enviar**

- **Biblioteca `WebPush` (NuGet):** traz o BouncyCastle, grande e sem garantia de Native AOT.
- **Firebase Cloud Messaging direto:** só Android, com conta e credencial do Google.
- **Web Push com VAPID escrito à mão:** a cifra (RFC 8291) e o JWT (RFC 8292) cabem em cerca de 150 linhas com `ECDiffieHellman`, `ECDsa`, `HKDF` e `AesGcm` do .NET. Sem dependência, compatível com AOT, e um teste confere o exemplo da RFC.

**Quando enviar**

- **Na transação do evento:** atrasa a resposta e, se o serviço de push demora, segura a transação.
- **Sinal em memória depois do commit:** imediato, mas exige ligar todas as fatias que geram avisos ao serviço.
- **Varredura periódica da tabela `notificacoes`:** a coluna `push_enviado_em` marca o que já foi. Sem fila, nada se perde num reinício, e 30 segundos de atraso bastam para um grupo de amigos.

**Preferências**

- **Por tipo de aviso:** a `0020` decidiu não ter preferências, e a `0048` já cortou o que não interessa.
- **Uma chave por aparelho:** suficiente; o sistema do telefone cuida do "não perturbe".

## Decisão

- **O que vai ao telefone:** `NovaAtividade`, `Reacao`, `Comentario`, `SituacaoAlterada` (`Rejeitada` e `EmRevisao`), `Disputa`, `CartaRecebida` e `RivalEscolhido`. `CartaAceita`, `CartaRecusada` e `JogadaConcluida` ficam só na lista do app. A lista em `Features/Notificacoes/TextosPush.cs`.
- **Conteúdo:** o título é o nome do grupo; o corpo é o mesmo texto da lista do app (`textos.ts`, espelhado em `TextosPush.cs`). A mensagem vai cifrada de ponta a ponta, mas aparece na tela bloqueada.
- **Inscrições:** tabela `push_subscriptions`, uma linha por aparelho, no máximo 10 por pessoa. O mesmo aparelho passa para quem entrar por último. Só endpoints `https` dos serviços conhecidos (Google, Apple, Mozilla, Microsoft) são aceitos, para o servidor não fazer requisições a endereços quaisquer. Uma resposta 404 ou 410 do serviço apaga a inscrição.
- **Envio:** `EnvioPushBackgroundService` varre a cada 30 segundos. Marca o lote antes de enviar (no máximo uma tentativa por mudança) e só envia o que não foi lido, mudou há menos de 1 hora, é de um grupo do qual a pessoa ainda participa e tem inscrição. Quando uma reação ou um comentário soma na mesma notificação, a coluna volta a nulo e o aviso vai de novo.
- **No telefone:** a `tag` e o cabeçalho `Topic` são o id da notificação, então o aviso somado substitui o anterior sem tocar de novo. `TTL` de 1 dia, `Urgency: high`. Tocar no aviso marca a notificação como lida e abre o mesmo destino da lista. O ícone do app instalado mostra o total de não lidas (`setAppBadge`), onde houver suporte.
- **Interface:** "Notificações neste aparelho" em Configurações, com Ativar e Desativar. No app instalado, um aviso único convida a ativar; "X" o dispensa por 1 ano (cookie `wx_push_dispensado`). Sair da conta desativa o push no aparelho. Ao abrir o app, a inscrição é reenviada, e trocada se as chaves do servidor mudaram.
- **Chaves VAPID:** configuração externa `Vapid:PublicKey` e `Vapid:PrivateKey`, e `Vapid:Subject` (padrão `https://wx.jpgmaraujo.com.br`). Sem as chaves, o push fica desligado e só a lista funciona. `Wrexercise gerar-vapid` imprime um par novo; o `deploy/remote-termux.sh` o gera na primeira implantação e o guarda no `.env` do telefone.

## Impacto

- Migração 025: `push_subscriptions` e `notificacoes.push_enviado_em`. As notificações existentes ficam marcadas, para não irem todas de uma vez.
- Rotas novas: `GET`, `PUT` e `DELETE /api/notificacoes/push`.
- O servidor faz requisições de saída aos serviços de push.
- Nenhuma dependência nova.
- **Compromisso aceito:** uma falha de rede no envio não tenta de novo; o aviso continua na lista do app.
- **Compromisso aceito:** no iOS, só quem instalou o app recebe push; os outros dependem do sino.
- **Compromisso aceito:** trocar as chaves VAPID (por exemplo, ao restaurar o telefone sem o `.env`) desfaz as inscrições até cada pessoa abrir o app.
