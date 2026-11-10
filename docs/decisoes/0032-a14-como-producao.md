# 0032 — Galaxy A14 como produção, em `wx.jpgmaraujo.com.br`

**Status:** DECIDIDA · Banco e túnel atualizados pela 0044 (PostgreSQL e túnel do `Environment`).

## Contexto

A VM da Oracle (0005) nunca foi criada por falta de capacidade, e `deploy.bat` nunca rodou. O Galaxy A14 roda a pilha nativa no Termux (0029, 0030), com túnel da Cloudflare, e já serve o grupo. Manter dois destinos de produção, um deles inexistente, só confunde.

## Alternativas consideradas

- **Manter a VM como destino dormente:** sem uso previsto, e `deploy.bat`, `remote.sh`, `cloud-init.yaml` e `migrar-nome.sh` ficariam sem teste.
- **Manter o túnel `wx-a14` e só trocar o host:** reaproveita a credencial, mas o nome do túnel destoa do host.
- **Cópia externa por serviço (rclone, R2, B2):** automática, mas acrescenta credencial e conta externa (rejeitada na 0022).

## Decisão

- **Produção:** o A14, por `run.bat --ssh`, em `https://wx.jpgmaraujo.com.br`. O túnel se chama `wx`. Os padrões do perfil são o túnel `wx`, o host `wx.jpgmaraujo.com.br` e o IP `127.0.0.1` (com `adb forward tcp:8022 tcp:8022`, como na 0029).
- **Removidos:** `deploy.bat`, `deploy/remote.sh`, `deploy/cloud-init.yaml` e `deploy/migrar-nome.sh`. `deploy/compose.yaml`, o Caddyfile e `.env.example` ficam: são a pilha local do `run.bat`.
- **`remote-termux.sh implantar`** apaga as credenciais de túneis antigos em `~/wrexercise/tunnel`, para que um túnel renomeado não deixe a credencial velha no telefone.
- **`stats.bat` e `remote-termux.sh stats`:** relatório só de leitura do uso do app, do PostgreSQL e do túnel (CPU, memória), do aparelho (carga, memória, bateria, temperatura), da saúde do app, do banco, do disco e dos backups. Lê o mesmo perfil SSH do `run.bat --ssh` e pergunta os dados do telefone se o perfil faltar ou não responder. `--watch` repete a leitura.
- **`remote-termux.sh reset-senha <email>`** substitui o comando de `docker compose` na produção.
- **Cópia dos backups para fora do telefone:** `run.bat ssh-backup` roda o backup no telefone e copia a pasta `~/wrexercise/dados/backups` para `BACKUP_DIR` do Windows por `tar` em pipe sobre SSH. Para a cópia sair da máquina, aponte `BACKUP_DIR` para uma pasta sincronizada (0022). O agendamento fica no Agendador de Tarefas.

## Impacto

- Substitui a 0005 e fecha a Q001 (e a parte de VM da Q003).
- Atualiza a 0022: a pasta `BACKUP_DIR` do Windows passa a ser a cópia externa dos backups do telefone.
- Passos manuais depois da troca de nome: responder `n` ao perfil salvo em `%LOCALAPPDATA%\Wrexercise\ssh\a14.env` (ou apagá-lo), rodar `run.bat --ssh`, e depois remover o túnel e o CNAME antigos: `cloudflared tunnel delete wx-a14` e apagar `wx-a14` no DNS da Cloudflare. O cookie da sessão é do host: quem usava o endereço antigo precisa entrar de novo.
- A pasta local `%LOCALAPPDATA%\Wrexercise\cloudflared\wx-a14.json` também pode ser apagada.
- Os backups ficam em dois lugares, mas só um deles sai do telefone se `BACKUP_DIR` for sincronizada. O teste de restauração continua manual.
- Relacionadas: 0005, 0022, 0029, 0030.
