# 0022 — Backups

**Status:** DECIDIDA · Cópia externa no telefone atualizada pela 0046 (Google Drive, automática).

## Contexto

Q002 pedia backup diário do banco e das evidências, retenção, cópia externa e restauração testada (`arquitetura.md` §15). A hospedagem definitiva continua indefinida (ver 0005): hoje a pilha roda no Windows por `run.bat` e talvez rode numa VM Linux depois. A solução precisa funcionar nos dois casos sem serviços nem credenciais novas.

## Alternativas consideradas

- **Backup físico (`pg_basebackup` e WAL):** permite restaurar até um instante exato, mas é desproporcional para um banco de poucos megabytes.
- **`pg_dump` e armazenamento de objetos (R2, B2) via rclone:** a cópia externa é automática, mas acrescenta uma imagem, uma credencial e uma conta externa.
- **`pg_dump` numa pasta, com a cópia externa feita por sincronização de pasta:** sem serviços novos. A cópia externa depende de a pasta estar sincronizada ou de alguém copiar os arquivos.

## Decisão

- `deploy/backup.sh` roda no serviço `backup` de `deploy/compose.yaml`. Ele usa a mesma imagem do PostgreSQL e fica no perfil `backup`, por isso o `up -d` nunca o inicia.
- `backup`:
  - grava `wrexercise-AAAAMMDD-HHMMSS.dump` (`pg_dump --format=custom`) e `evidencias-AAAAMMDD-HHMMSS.tar.gz` em `BACKUP_DIR`;
  - a data e a hora estão em UTC;
  - cada arquivo é gravado como `.tmp` e só ganha o nome final depois de passar pela verificação (`pg_restore --list` e `gzip -t`);
  - arquivos com mais de 7 dias são apagados.
- `restore AAAAMMDD-HHMMSS`, sempre com o app parado:
  - restaura o banco com `pg_restore --clean --if-exists --single-transaction`;
  - substitui o conteúdo de `/dados/evidencias` pelo do arquivo, preservando os donos.
- `BACKUP_DIR`:
  - na VM, vale `/srv/wrexercise/backups` por padrão;
  - no `run.bat`, vale `%LOCALAPPDATA%\Wrexercise\backups`, fora da pasta que `run.bat reset` apaga;
  - para ter cópia externa, aponte-o para uma pasta sincronizada (OneDrive, Google Drive) ou copie os arquivos para fora da máquina regularmente.
- O agendamento fica com o sistema: `cron` no Linux e o Agendador de Tarefas no Windows. Os comandos estão no README.
- O teste de restauração é manual e periódico, conforme o roteiro do README.

## Impacto

- Q002 fica decidida.
- **Compromisso aceito:** a cópia externa não é automática se `BACKUP_DIR` não estiver numa pasta sincronizada. Um backup só na mesma máquina não protege contra perda do disco.
- **Compromisso aceito:** as evidências são copiadas por inteiro todos os dias. Com 10 pessoas e imagens comprimidas no navegador, o volume é pequeno. Se crescer, troca-se por cópia incremental.
- Os backups contêm todas as evidências e os hashes de senha: a pasta de destino precisa ser privada.
