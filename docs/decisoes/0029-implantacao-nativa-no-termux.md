# 0029 — Implantação nativa no Termux (Android, sem VM)

**Status:** DECIDIDA · Banco e túnel atualizados pela 0044 (PostgreSQL e túnel do `Environment`).

## Contexto

A decisão 0028 colocou a pilha no Galaxy A14 dentro do Podroid: guest Alpine, QEMU em modo TCG (sem KVM no Android de fábrica) e Docker. No TCG cada instrução da CPU é emulada e cada escrita passa por disco virtual e pelo overlay do Docker. O armazenamento ficou lento demais: o `initdb` levava minutos, e a VM de 2 GiB fixos consumia a maior parte dos 3,4 GiB do aparelho.

## Alternativas consideradas

- **Ajustar o PostgreSQL no Podroid** (menos durabilidade, WAL menor): mexe só em `compose.baixa-memoria.yaml`, mas a CPU continua emulada. Ganho limitado.
- **`proot-distro` com Debian:** CPU nativa, mas cada chamada de sistema passa por `ptrace`, o que pesa no banco.
- **Chroot ou contêiner nativo:** exige root, que o aparelho não tem.
- **Termux nativo com `glibc-runner` (escolhida):** PostgreSQL e `cloudflared` vêm do repositório do Termux (bionic). O app é o mesmo binário Native AOT `linux-arm64` (glibc) do `deploy/Dockerfile`, executado por `grun`, sem camada de tradução.

## Decisão

- `run.bat --ssh` aceita o SO `termux`: porta 8022, usuário do Termux (resultado de `whoami`), arquitetura `arm64`. Não há Docker no alvo.
- O binário sai da imagem construída pelo `deploy/Dockerfile` (`docker create` e `docker cp`, sem mudar o Dockerfile) e vai por `tar` em pipe para `~/wrexercise/app.novo`. A credencial do túnel segue o mesmo caminho.
- `deploy/remote-termux.sh` roda no telefone, em `sh` POSIX, com os comandos `implantar`, `iniciar`, `parar`, `status`, `backup` e `restore`. Ele não instala pacotes: se faltarem, imprime o comando. `implantar` troca o app (o anterior fica em `app.antigo`), migra e reinicia.
- O PostgreSQL escuta só em `127.0.0.1`, com `scram-sha-256`, `synchronous_commit=off` (um corte de energia perde no máximo cerca de 0,5 s de commits e não corrompe o banco), `shared_buffers=128MB`, `max_connections=20` e `wal_compression=on`. A senha é gerada na primeira vez e fica em `~/wrexercise/.env` (modo 600).
- O app e o `cloudflared` rodam como tarefas que reiniciam sozinhas (laço com pausa de 3 s). `termux-wake-lock` impede a suspensão da CPU, e o Termux:Boot executa `iniciar` ao ligar o telefone.
- `deploy/backup.sh` passou a aceitar `BACKUP_DESTINO` e `EVIDENCIAS_DIR`; os padrões continuam os do contêiner.
- Dados em `~/wrexercise/dados/{postgres,evidencias,backups}`. Túnel próprio (`wx-a14`), cadastro sem convite fechado, como em 0028.

## Pré-requisitos no telefone

- Termux e Termux:Boot da F-Droid ou do GitHub (a versão da Play Store está obsoleta), bateria sem restrições, `sshd` ligado e chave SSH em `~/.ssh/authorized_keys`.
- Pacotes: `pkg install -y glibc-repo && pkg update -y && pkg install -y postgresql cloudflared glibc-runner curl openssh`.
- Android 12 ou mais novo mata processos em segundo plano. Desligue o monitor: `adb shell settings put global settings_enable_monitor_phantom_procs false` e `adb shell device_config put activity_manager max_phantom_processes 2147483647`.
- Para o Termux:Boot funcionar: conceda "Exibir sobre outros apps" ao Termux e ao Termux:Boot (`adb shell cmd appops set <pacote> SYSTEM_ALERT_WINDOW allow`), abra o Termux:Boot uma vez, e no Samsung desligue "Colocar apps não usados para dormir" e ponha os dois em "Apps que nunca dormem" (bateria Sem restrições).
- Depois de reiniciar o telefone, desbloqueie-o com o PIN: o armazenamento do Termux só abre no primeiro desbloqueio.
- Com `adb forward tcp:8022 tcp:8022`, o `run.bat` fala com o telefone por USB, sem depender do IP do Wi-Fi. Use `127.0.0.1` como host do perfil: o OpenSSH do Windows tenta IPv6 com `localhost`, e o `adb forward` só escuta em IPv4.

## Impacto

- Medido no A14: `initdb` em 1,6 s e 14 migrações em 0,25 s (no Podroid, minutos). O app usa cerca de 25 MB de RSS.
- O PostgreSQL passa a ser o 18.6 em toda a pilha (`compose.yaml`, `compose.dev.yaml` e Termux), como o projeto ainda não tem dados a preservar. No Termux ele é fixado com `apt-mark hold postgresql`, e `remote-termux.sh` recusa outra versão maior. A imagem 18 guarda os dados em `<volume>/18/docker`; por isso o volume agora monta `/var/lib/postgresql`, não `/var/lib/postgresql/data`. Diretórios de dados da versão 17 não abrem: apague-os (`run.bat reset`, `docker volume rm wrexercise-dev_dev_postgres`, `/srv/wrexercise/postgres`).
- Teste de reinicialização (12:09 a 12:16): depois do desbloqueio, o Termux:Boot iniciou o `BootJobService`, que subiu PostgreSQL, app e túnel sozinhos, e `/ready` voltou a responder 200 pelo túnel. O Android atrasou o job em cerca de 5 minutos; o tempo até a pilha voltar depende dele. O PostgreSQL se recuperou da parada abrupta sem perda. O `sshd` não sobe no boot: para implantar de novo, abra o Termux e rode `sshd`.
- Sem o isolamento do Docker: app, banco e túnel rodam como o mesmo usuário do Android. Aceitável para um grupo pequeno em rede privada com túnel.
- Os backups ficam no telefone; copiá-los para fora segue em aberto (0022).
- O fluxo com Docker da 0028 foi removido pela 0030.
- Relacionadas: 0005, 0028.
