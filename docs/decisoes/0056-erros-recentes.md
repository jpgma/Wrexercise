# 0056 — Erros recentes: referência no erro interno e erros.bat

**Status:** DECIDIDA. Complementa `0029-implantacao-nativa-no-termux.md`, `0032-a14-como-producao.md` e `0055-questoes-resolvidas-pela-implementacao.md` (Q007).

## Contexto

O primeiro relato de problema vai ser "deu erro". Os logs só vão para stdout, que o Termux grava em `~/wrexercise/log/app.log`. Até aqui:

- o erro interno devolvia só `ERRO_INTERNO`, sem nada que ligasse a captura de tela da pessoa a uma linha do log;
- as linhas do log não tinham horário;
- ver o log exigia abrir uma sessão SSH e procurar à mão; `stats.bat` só contava linhas com "error".

`arquitetura.md` §14 pede logs estruturados em stdout, sem serviços de observabilidade.

## Alternativas consideradas

- **Página de erros no próprio app:** gravar os erros numa tabela e mostrá-los a um operador, acessível de qualquer lugar pelo túnel. Exige uma identidade de operador, migração e retenção. Fica para depois, se o SSH de fora de casa não resolver.
- **Serviço externo de erros (Sentry e similares):** é observabilidade dedicada, vetada em `AGENTS.md`, e mandaria dados para fora.
- **Push para o mantenedor a cada erro:** ruído, e ainda não diz o que aconteceu.
- **Referência no erro interno e `erros.bat` por SSH:** escolhida.

## Decisão

- O erro interno (`500 ERRO_INTERNO`) traz `referencia`: 6 caracteres aleatórios, sem `0/O` e `1/I/L`. O app mostra "Erro interno do servidor. Código: 7F3K2Q". Os outros erros não têm referência.
- O tratador registra uma linha só, na categoria `Wrexercise.ErroInterno`: "Erro interno {Referencia} em {Metodo} {Caminho}", com a exceção. O log do `ExceptionHandlerMiddleware` fica desligado para a exceção não aparecer duas vezes. O caminho vai sem a query string, e o cliente nunca recebe a exceção.
- Os logs JSON ganham `Timestamp` em UTC (`yyyy-MM-ddTHH:mm:ssZ`).
- `remote-termux.sh erros [N|CODIGO]` lê `log/app.log` com as ferramentas do Termux (`grep` e `awk`, sem pacote novo): sem argumento, os 10 erros mais recentes (`Error` e `Critical`) com as 8 primeiras linhas da exceção; com um número, até 200; com uma referência, as linhas dela com a exceção inteira. Aceita só número ou letras e números.
- `erros.bat` roda esse comando pelo perfil SSH compartilhado (`%LOCALAPPDATA%\jpgma\a14.env`), como `stats.bat`.
- `stats.bat` conta só as linhas `Error` e `Critical` e aponta para `erros.bat`.

## Impacto

- O log guarda o último 1 MB a cada reinício do app (`iniciar_tarefa`). Um erro de dias atrás pode ter saído; peça o código logo.
- Ler os erros de fora de casa depende de SSH até o telefone. Hoje ele só responde por USB (`adb forward`) ou na rede local. Abrir esse acesso é um passo à parte, no `Environment`: uma regra `ssh://127.0.0.1:8022` no túnel `a14` da Cloudflare, protegida pelo Cloudflare Access, com `cloudflared access ssh` como ProxyCommand no Windows. Sem VPN nem serviço novo.
- `ProblemResponse` ganha o campo opcional `referencia`, omitido quando vazio. O formato do erro de Q007 não muda.
