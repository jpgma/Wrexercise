# 0044 — PostgreSQL e túnel compartilhados do Environment

**Status:** DECIDIDA · Backups do Wrexercise atualizados pela 0046 (o dump do banco também vai para o Google Drive).

## Contexto

O Galaxy A14 passa a hospedar também a API do Movr, outro projeto do mesmo operador. Com a pilha da 0029, cada app subiria o próprio PostgreSQL e o próprio `cloudflared`: dois clusters e dois conectores no mesmo aparelho de 3,4 GiB, com dois boots, dois backups de banco e dois túneis para manter. A 0028 já tinha listado (e rejeitado, por acoplamento) o reaproveitamento do PostgreSQL do repositório `Environment`. Agora o `Environment` roda nativo no Termux, como este repositório, e não mais no Podroid.

## Alternativas consideradas

- **Cada app com o próprio PostgreSQL e túnel:** sem acoplamento, mas ~150 MB e um conector a mais por app, e o mesmo código de banco e túnel duplicado nos repositórios.
- **Adotar o cluster do Wrexercise como compartilhado:** sem dump, mas o superusuário do cluster é o papel do app (`wrexercise`) e os dados ficariam na pasta de um app.
- **Cluster novo do `Environment`, Wrexercise migrado por dump e restore (escolhida).**

## Decisão

- O PostgreSQL 18 e o túnel único `a14` são do repositório `Environment` (`~/ambiente` no telefone, `termux/ambiente.sh`). O superusuário é `postgres`; a senha dele nunca sai do telefone.
- O Wrexercise usa o papel `wrexercise`, **sem superusuário**, dono do banco `wrexercise` e do schema `public`. As migrações não precisam de superusuário.
- `remote-termux.sh iniciar` espera o banco (`ambiente.sh aguardar`) e passa a própria `DB_PASSWORD` pelo stdin para `ambiente.sh provisionar wrexercise`, que cria ou atualiza papel, banco e senha. O script não roda mais `initdb`, `pg_ctl` nem `cloudflared`.
- O hostname `wx.jpgmaraujo.com.br` passa ao túnel `a14` (ingress em `Environment/termux/cloudflared.yml` → `127.0.0.1:8080`). O túnel `wx` deixa de ser usado.
- Backups: o `Environment` faz o dump de todos os bancos (`~/ambiente/backups/wrexercise-*.dump`, 7 dias, `run.bat ssh-backup` do `Environment`). Este repositório guarda só evidências e avatares (`BACKUP_BANCO=0` em `deploy/backup.sh`). A pilha local de Docker continua com o dump do banco.
- Perfil SSH compartilhado: `%LOCALAPPDATA%\jpgma\a14.env` (`run.bat --ssh`, `run.bat ssh-backup`, `stats.bat`).
- `run.bat --ssh --scripts` envia só `deploy/*.sh` e reaproveita o app que está no telefone, sem build.
- Boot hook renomeado para `~/.termux/boot/20-wrexercise.sh`, depois do `00-ambiente.sh`.

## Impacto

- Atualiza a 0029 (banco e túnel saem deste repositório) e a 0032 (túnel `wx` → `a14`; o `ssh-backup` daqui não leva mais o banco). Revê a alternativa rejeitada na 0028.
- Migração única no telefone: parar o app, `pg_dump` do cluster antigo (`~/wrexercise/dados/postgres`), `provisionar`, `ambiente.sh restore wrexercise <arquivo>`, implantar com `--scripts`, `run.bat dns` no `Environment`. O cluster antigo e o `app.antigo` ficam até a conferência; depois, apagar `~/wrexercise/dados/postgres` e o túnel `wx` (`cloudflared tunnel delete wx`).
- Ponto único de falha: PostgreSQL, túnel ou telefone fora do ar derrubam os dois apps.
- Pools somados (Wrexercise 10 + Movr 5) cabem em `max_connections=30`. Aumentar o pool daqui exige rever o orçamento em `Environment/docs/apis.md`.
- Todos os apps rodam no mesmo usuário Android: o papel sem superusuário e o `.env` com modo 600 separam os bancos, mas não são uma fronteira de segurança.
- Relacionadas: 0022, 0028, 0029, 0032.
