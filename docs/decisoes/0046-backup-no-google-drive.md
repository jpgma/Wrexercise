# 0046 — Backup cifrado no Google Drive, diário e na implantação

**Status:** DECIDIDA. Atualiza `0022-backups.md` e `0032-a14-como-producao.md` (cópia externa) e o item de backups de `0044-postgres-e-tunel-do-environment.md`.

## Contexto

A produção é o Galaxy A14 (0032). Nenhum backup saía do telefone sozinho:

- O banco era copiado só pelo `Environment`, em `~/ambiente/backups`, no próprio telefone.
- As evidências e os avatares iam inteiros, todos os dias, para `~/wrexercise/dados/backups`, com 7 dias de retenção. Num grupo maior, isso são 7 cópias de todas as fotos no disco do telefone.
- A cópia externa dependia de o Windows rodar `run.bat ssh-backup` numa pasta sincronizada. Com o computador desligado, não havia cópia.
- `log/app.log` só era cortado quando o app reiniciava. Com o app no ar por semanas, crescia sem limite.

O orçamento é zero. Por isso, as 0022 e 0032 rejeitaram o rclone, que exige uma conta e uma credencial externas. Agora, porém, a perda do telefone apaga a produção e quase todos os backups juntos. Por esse motivo concreto, a alternativa volta.

## Estimativa de espaço

Premissas:

- Uma evidência tem cerca de 0,36 MB: 1600 px em JPEG 0,82, mais a miniatura.
- Cada pessoa registra cerca de 1 atividade por dia.
- O dump cresce cerca de 2 KB por pessoa por dia.

| Cenário | Fotos por dia | Fotos em 1 ano |
|---|---|---|
| 1 grupo de 4, 1 foto por dia | 1,4 MB | 0,5 GB |
| 1 grupo de 10, 1,5 foto por dia | 5,4 MB | 2,0 GB |
| 2 grupos de 10, 1,5 foto por dia | 10,8 MB | 3,9 GB |

Com um arquivo completo por dia e 31 dias guardados, o Drive guarda 31 cópias de todas as fotos. Os 15 GB gratuitos acabam:

- em cerca de 11 meses, com 4 pessoas;
- em 3 meses, com 10;
- em 6 semanas, com 20.

Com o arquivo diário mais o espelho das evidências (decisão abaixo), 20 pessoas ocupam cerca de 4,8 GB em 1 ano e cerca de 13 GB em 3 anos.

## Alternativas consideradas

- **Um arquivo completo por dia:** o mais simples de restaurar, mas esgota a cota, como mostra a estimativa.
- **restic com deduplicação:** cifra, deduplica e aplica `forget --keep-within 31d`. Em troca, acrescenta outra ferramenta e outra senha, e o Drive guarda pacotes que só o restic consegue ler.
- **Backblaze B2 ou Cloudflare R2:** 10 GB gratuitos e regras de ciclo de vida no servidor. O R2 exige cartão. Fica como plano B se o Drive encher.
- **Manter a cópia pelo Windows:** depende de o computador estar ligado.
- **Um sink de log em arquivo dentro do app:** exige um `ILoggerProvider` próprio, compatível com AOT, para o mesmo resultado de girar o arquivo no backup.

## Decisão

- **`deploy/nuvem.sh`** roda no Termux, chamado por `remote-termux.sh`. Envia ao Google Drive, pelo rclone, com o remoto cifrado `wx-cripto` (rclone crypt sobre `gdrive:wrexercise`, escopo `drive.file`). O conteúdo e os nomes chegam cifrados ao Drive.
- **Arquivo diário** `diarios/wrexercise-AAAAMMDD-HHMMSS[-implantacao].tar.gz`, com:
  - o `pg_dump` do banco `wrexercise`, feito pelo próprio papel `wrexercise`, que é dono do banco e não precisa de superusuário;
  - os avatares;
  - os logs;
  - `versao.txt`, com a imagem implantada.

  O dump é conferido com `pg_restore --list` e o arquivo com `gzip -t`. O telefone guarda os 3 mais novos.
- **Evidências:** `rclone sync` para `evidencias/`, um espelho de todas as evidências atuais, que só envia as novas. Um arquivo removido no telefone vai para `evidencias-removidas/<data>/`. O espelho é atualizado antes do envio do arquivo diário: assim, toda evidência citada pelo dump já está no Drive. O `--max-delete 100` impede que uma pasta vazia por engano esvazie o espelho.
- **Retenção:** 31 dias pela data no nome, não pela quantidade de arquivos. Vale para `diarios/` e `evidencias-removidas/`. A remoção só acontece depois de um envio bem-sucedido. Os 7 arquivos diários mais novos ficam sempre: uma sequência de falhas nunca esvazia o Drive. A remoção é definitiva (`--drive-use-trash=false`), porque a lixeira também ocupa a cota.
- **Quando roda:**
  - **Agendado:** a tarefa `backup` de `remote-termux.sh` usa o mesmo laço do app. Ela sobe no boot pelo Termux:Boot e roda `nuvem.sh agendado` a cada hora. Se o último backup tem 24 h ou mais, faz um novo. Senão, reenvia o que ficou pendente. Sem cron e sem job scheduler.
  - **Na implantação:** `remote-termux.sh implantar` grava o backup com o app parado, antes de trocar o app e de rodar as migrações. Uma falha ao gravar interrompe a implantação. Uma falha no envio só avisa, e o arquivo fica pendente.
- **Logs:** quem faz o backup gira os logs. Copia `log/app.log` e `log/backup.log` para o arquivo e esvazia os dois no lugar (`: > arquivo`). O app escreve com `>>` (O_APPEND), por isso continua no início do arquivo. O log local guarda só o que veio depois do último backup. Se os backups falharem, a tarefa corta o `app.log` em 20 MB e mantém os últimos 5 MB. O app não muda.
- **Restauração:** `remote-termux.sh restore [AAAAMMDD-HHMMSS]` faz isto, nesta ordem:
  1. para o app;
  2. baixa o arquivo diário;
  3. restaura o banco com `pg_restore --clean --if-exists --single-transaction`, como o papel `wrexercise`;
  4. restaura os avatares;
  5. sincroniza as evidências a partir do espelho;
  6. copia de volta as evidências de `evidencias-removidas/` com data posterior à do backup.

  Restaurar o backup mais recente traz todas as evidências, de qualquer idade. Sem o rclone, a restauração usa um arquivo local e não mexe nas evidências.
- **`run.bat configurar-backup`** faz toda a configuração, neste PC e no celular, com perguntas:
  - instala o rclone (`winget` e `pkg`);
  - faz o login no Google pelo navegador do PC;
  - cria o remoto cifrado com senhas aleatórias de 256 bits, em `%LOCALAPPDATA%\Wrexercise\rclone\rclone.conf`;
  - copia as seções `[gdrive]` e `[wx-cripto]` para o `rclone.conf` do celular (`nuvem.sh importar-config`);
  - sobe a tarefa de backup;
  - oferece um backup de teste e a cópia extra agendada.

  Pode rodar de novo. Se o celular já tiver a configuração, traz a dele (`exportar-config`). Se o token do Google expirou, faz `config reconnect`. Se a pasta do Drive já tiver backups, só cria uma chave nova depois de confirmar que vai apagá-los: uma chave nova não lê os backups antigos.
- **Sem rclone configurado,** o backup fica só no telefone, com um aviso, e a implantação continua. O rclone não é obrigatório em `exigir`.
- **`deploy/backup.sh`** passa a ser só da pilha local de Docker, sem a chave `BACKUP_BANCO`.

## Impacto

- **Credencial nova, só no telefone:** `~/.config/rclone/rclone.conf`, com modo 600, contém o token do Drive e as senhas do crypt. Guarde uma cópia do `rclone.conf` no gerenciador de senhas: sem as senhas do crypt, o backup não pode ser lido.
- **OAuth:** use o client ID padrão do rclone ou um client próprio publicado como "In production". Um client em "Testing" emite tokens que expiram em 7 dias, e o backup passa a falhar em silêncio.
- **Conta:** é melhor usar uma conta Google gratuita só para backups. Ela tem 15 GB próprios, separados do Gmail e do Fotos da conta pessoal.
- **Revisão:** ao passar de cerca de 12 GB, acrescente o Backblaze B2 como segundo destino ou reveja a retenção.
- **Dependência:** o rclone é um pacote do Termux (`pkg install rclone`), não uma dependência do app. Não entra em `PackageAllowlistTests`.
- **Duas camadas locais:** o `Environment` continua com o dump local de todos os bancos, por 7 dias, como segunda camada.
- **Situação local:** cada backup grava em `dados/backups` os arquivos `ultimo-resultado` (origem, tamanhos, durações e resultado do envio), `ultimo-erro` (etapa da última falha) e `drive` (uso no Drive, medido depois de cada envio bem-sucedido). `nuvem.sh status`, chamado por `remote-termux.sh status` e `stats.bat`, só lê esses arquivos e não acessa o Drive, porque `stats.bat --watch` repete a cada 3 s. Esse relatório avisa:
  - quando nenhum envio deu certo em 26 h;
  - quando há envio pendente;
  - quando há um erro mais novo que o último envio;
  - quando a cota passa de 80%.
- **Fora desta decisão:** um aviso externo quando um dia falha, como o healthchecks.io, exigiria decisão própria (observabilidade dedicada).
- Relacionadas: 0022, 0029, 0032, 0044.
