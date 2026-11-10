#!/bin/sh
# Executado no Termux (Android, sem root) por run.bat --ssh. POSIX sh.
# Roda o app como processo nativo, sem VM e sem Docker. O PostgreSQL e o túnel da Cloudflare são do
# repositório Environment (~/ambiente), compartilhados com os outros apps do telefone.
# Ver docs/decisoes/0029-implantacao-nativa-no-termux.md e 0044-postgres-e-tunel-do-environment.md.
#
#   remote-termux.sh implantar                troca o app (se veio app.novo), migra e (re)inicia
#   remote-termux.sh iniciar                  sobe o app se não estiver rodando (Termux:Boot usa este)
#   remote-termux.sh parar
#   remote-termux.sh backup                   evidências e avatares (o banco fica com o Environment)
#   remote-termux.sh restore AAAAMMDD-HHMMSS  evidências e avatares; o banco: ambiente.sh restore wrexercise
#   remote-termux.sh reset-senha <email>      gera o link de redefinição de senha
#   remote-termux.sh status
#   remote-termux.sh stats                    CPU, memória, banco, disco e backups (stats.bat usa este)
set -eu

base=$HOME/wrexercise
dados=$base/dados
ambiente="sh $HOME/ambiente/deploy/ambiente.sh"
cd "$base"

# O binário Native AOT é glibc; o Termux é bionic. O grun (pacote glibc-runner) faz a ponte sem proot.
exigir() {
  faltam=
  for c in psql grun curl; do
    command -v "$c" >/dev/null 2>&1 || faltam="$faltam $c"
  done
  if [ -n "$faltam" ]; then
    echo "ERRO: faltam no Termux:$faltam" >&2
    echo "Instale e rode de novo:" >&2
    echo "  pkg install -y glibc-repo && pkg update -y && pkg install -y postgresql glibc-runner curl" >&2
    exit 1
  fi
  if [ ! -f "$HOME/ambiente/deploy/ambiente.sh" ]; then
    echo "ERRO: o Environment (PostgreSQL e túnel) não está instalado. No repositório Environment: run.bat implantar" >&2
    exit 1
  fi
}

# Preserva a senha do banco entre implantações; ela nunca sai do telefone.
ler_env() {
  if [ ! -f .env ]; then
    (
      umask 077
      printf 'DB_PASSWORD=%s\n' "$(tr -dc 'a-f0-9' </dev/urandom | head -c 64)" > .env
    )
  fi
  DB_PASSWORD=$(sed -n 's/^DB_PASSWORD=//p' .env)
}

# Verdadeiro se o PID gravado ainda é o laço desta tarefa. Depois de um reinício, o PID antigo pode
# pertencer a outro processo do Termux (como o script de boot de outro app): só kill -0 não basta.
tarefa_rodando() {
  pid=$(cat "run/$1.pid" 2>/dev/null) || return 1
  [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null && tr '\0' ' ' <"/proc/$pid/cmdline" 2>/dev/null | grep -qF "$base/"
}

# Uma tarefa que reinicia sozinha se cair; a pausa evita laço apertado em erro de configuração.
iniciar_tarefa() {
  nome=$1
  shift
  if tarefa_rodando "$nome"; then
    return 0
  fi
  [ -f "log/$nome.log" ] && tail -c 1048576 "log/$nome.log" > "log/$nome.log.tmp" && mv "log/$nome.log.tmp" "log/$nome.log"
  setsid nohup sh -c 'while :; do "$@"; sleep 3; done' sh "$@" >>"log/$nome.log" 2>&1 </dev/null &
  echo $! > "run/$nome.pid"
}

parar_tarefa() {
  nome=$1
  if [ -f "run/$nome.pid" ]; then
    kill "$(cat "run/$nome.pid")" 2>/dev/null || true
    rm -f "run/$nome.pid"
  fi
}

# Espera o PostgreSQL do Environment (no boot ele pode subir depois) e garante papel, banco e senha.
preparar_banco() {
  $ambiente aguardar
  printf '%s\n' "$DB_PASSWORD" | $ambiente provisionar wrexercise >/dev/null
}

# O app lê a configuração do ambiente, como nos contêineres de compose.yaml.
ambiente_app() {
  export ConnectionStrings__Database="Host=127.0.0.1;Port=5432;Database=wrexercise;Username=wrexercise;Password=$DB_PASSWORD;Maximum Pool Size=10"
  export Evidencias__Diretorio=$dados/evidencias
  export Avatares__Diretorio=$dados/avatares
  export ASPNETCORE_URLS=http://127.0.0.1:8080
  # Sem isto o ASP.NET usa o diretório atual (o pai de app/) e não acha wwwroot nem appsettings.json.
  export ASPNETCORE_CONTENTROOT=$base/app
}

iniciar() {
  exigir
  mkdir -p run log "$dados/evidencias" "$dados/avatares" "$dados/backups"
  ler_env
  # Sem o wake lock, o Android suspende a CPU com a tela apagada.
  command -v termux-wake-lock >/dev/null 2>&1 && termux-wake-lock || true
  preparar_banco
  ambiente_app
  iniciar_tarefa app grun "$base/app/Wrexercise"
}

parar() {
  parar_tarefa app
  pkill -f "$base/app/Wrexercise" 2>/dev/null || true
}

implantar() {
  exigir
  mkdir -p run log "$dados"
  ler_env
  parar
  # run.bat envia o app novo em app.novo; o anterior fica em app.antigo para reverter.
  # Sem app.novo (run.bat --ssh --scripts), só os scripts mudaram e o app atual continua.
  if [ -d app.novo ]; then
    rm -rf app.antigo
    [ -d app ] && mv app app.antigo
    mv app.novo app
  fi
  chmod +x app/Wrexercise
  preparar_banco
  ambiente_app
  mkdir -p "$dados/evidencias" "$dados/avatares"
  grun "$base/app/Wrexercise" migrate
  iniciar
  # Termux:Boot roda ~/.termux/boot/ em ordem alfabética, depois do 00-ambiente.sh do Environment.
  mkdir -p "$HOME/.termux/boot"
  rm -f "$HOME/.termux/boot/wrexercise.sh"
  printf '#!/data/data/com.termux/files/usr/bin/sh\nexec sh %s iniciar\n' "$base/deploy/remote-termux.sh" > "$HOME/.termux/boot/20-wrexercise.sh"
  chmod 700 "$HOME/.termux/boot/20-wrexercise.sh"
  # O túnel agora é do Environment: a credencial antiga não fica aqui.
  rm -rf tunnel tunel.txt
}

status() {
  curl -fs --max-time 5 http://127.0.0.1:8080/health || echo "app sem resposta"
  echo
  if tarefa_rodando app; then echo "app: rodando"; else echo "app: parado"; fi
  if pg_isready -q -h 127.0.0.1 -p 5432 2>/dev/null; then echo "PostgreSQL (Environment): aceita conexões"; else echo "PostgreSQL (Environment): sem resposta"; fi
  free -m | head -2
}

# Estatísticas de uso, só leitura, sem root: lê /proc, consulta o PostgreSQL e os logs. Usado por stats.bat.
titulo() { printf '\n== %s ==\n' "$1"; }

# PIDs reais dos processos (sem o "sh" do laço de reinício).
pids_de() {
  for p in $(pgrep "$1" "$2" 2>/dev/null); do
    [ "$p" = "$$" ] && continue
    [ "$(cat "/proc/$p/comm" 2>/dev/null)" = sh ] && continue
    printf '%s ' "$p"
  done
}

# Soma utime+stime (em ticks) dos PIDs. Remove tudo até o ")" do nome, que pode ter espaços.
ticks_de() {
  t=0
  for p in "$@"; do
    [ -r "/proc/$p/stat" ] || continue
    v=$(sed 's/^.*) //' "/proc/$p/stat" 2>/dev/null | awk '{print $12 + $13}')
    t=$((t + ${v:-0}))
  done
  echo "$t"
}

linha_processo() {
  nome=$1 pids=$2 antes=$3 depois=$4
  if [ -z "$pids" ]; then
    printf '%-11s PARADO\n' "$nome"
    return
  fi
  rss=0 pss=0 pico=0 thr=0 n=0 tem_pss=0 inicio=
  for p in $pids; do
    r=$(awk '/^VmRSS:/ {print $2}' "/proc/$p/status" 2>/dev/null)
    h=$(awk '/^VmHWM:/ {print $2}' "/proc/$p/status" 2>/dev/null)
    th=$(awk '/^Threads:/ {print $2}' "/proc/$p/status" 2>/dev/null)
    s=$(awk '/^Pss:/ {print $2}' "/proc/$p/smaps_rollup" 2>/dev/null)
    rss=$((rss + ${r:-0})); pico=$((pico + ${h:-0})); thr=$((thr + ${th:-0})); n=$((n + 1))
    if [ -n "$s" ]; then pss=$((pss + s)); tem_pss=1; fi
    [ -n "$inicio" ] || inicio=$(ps -o etime= -p "$p" 2>/dev/null | tr -d ' ')
  done
  awk -v nome="$nome" -v n="$n" -v rss="$rss" -v pss="$pss" -v tp="$tem_pss" -v pico="$pico" -v thr="$thr" \
    -v dt="$((depois - antes))" -v hz="$hz" -v s="$amostra" -v ini="${inicio:--}" 'BEGIN {
      pssf = tp ? sprintf("%.1f MB", pss / 1024) : "indisponível"
      printf "%-11s %d proc | RSS %.1f MB | PSS %s | pico %.1f MB | threads %d | CPU %.1f%% de 1 núcleo | ativo há %s\n",
        nome, n, rss / 1024, pssf, pico / 1024, thr, dt * 100 / (hz * s), ini }'
}

stats() {
  set +e
  ler_env
  hz=$(getconf CLK_TCK 2>/dev/null || echo 100)
  amostra=2

  titulo "Sistema"
  echo "Data: $(date '+%Y-%m-%d %H:%M:%S %z')"
  echo "Ligado há: $(uptime -p 2>/dev/null || uptime)"
  if [ -r /proc/loadavg ]; then
    echo "Carga (1/5/15 min): $(cut -d' ' -f1-3 /proc/loadavg)"
  else
    echo "Carga (1/5/15 min): indisponível (o Android bloqueia /proc/loadavg)"
  fi
  echo "Núcleos: $(nproc 2>/dev/null || echo ?) | Kernel: $(uname -r) $(uname -m)"
  if [ -r /proc/meminfo ]; then
    awk '/^MemTotal:/ {t=$2} /^MemAvailable:/ {a=$2} /^SwapTotal:/ {st=$2} /^SwapFree:/ {sf=$2} /^Cached:/ {c=$2}
      END { printf "Memória: %.0f MB no total | %.0f MB disponíveis (%.0f%% livre) | cache %.0f MB\n", t/1024, a/1024, a*100/t, c/1024
            printf "Swap: %.0f MB no total | %.0f MB em uso\n", st/1024, (st-sf)/1024 }' /proc/meminfo
  fi
  if command -v termux-battery-status >/dev/null 2>&1; then
    bat=$(timeout 5 termux-battery-status 2>/dev/null | tr -d '\n ' )
    [ -n "$bat" ] && echo "Bateria: $bat" || echo "Bateria: sem resposta do Termux:API"
  else
    echo "Bateria: indisponível (instale o pacote termux-api e o app Termux:API)"
  fi
  for z in /sys/class/thermal/thermal_zone*/temp; do
    [ -r "$z" ] || continue
    printf 'Temperatura %s: %s\n' "$(cat "$(dirname "$z")/type" 2>/dev/null)" "$(awk '{printf "%.1f °C", $1 / 1000}' "$z" 2>/dev/null)"
  done | head -4

  titulo "Processos (amostra de ${amostra}s)"
  p_app=$(pids_de -f "$base/app/Wrexercise")
  # O PostgreSQL é compartilhado com os outros apps; o PID do postmaster está no cluster do Environment.
  pm=$(head -n 1 "$HOME/ambiente/postgres/postmaster.pid" 2>/dev/null)
  p_pg=
  if [ -n "$pm" ] && [ -d "/proc/$pm" ]; then p_pg="$pm $(pgrep -P "$pm" 2>/dev/null | tr '\n' ' ')"; fi
  p_tun=$(pids_de -x cloudflared)
  a_app=$(ticks_de $p_app); a_pg=$(ticks_de $p_pg); a_tun=$(ticks_de $p_tun)
  sleep "$amostra"
  d_app=$(ticks_de $p_app); d_pg=$(ticks_de $p_pg); d_tun=$(ticks_de $p_tun)
  linha_processo app "$p_app" "$a_app" "$d_app"
  linha_processo postgres "$p_pg" "$a_pg" "$d_pg"
  linha_processo cloudflared "$p_tun" "$a_tun" "$d_tun"
  echo "(PostgreSQL e túnel são compartilhados com os outros apps. Detalhes: sh ~/ambiente/deploy/ambiente.sh stats)"

  titulo "Aplicação"
  for rota in health ready; do
    curl -s -o /dev/null --max-time 5 -w "/$rota: HTTP %{http_code} em %{time_total}s\n" "http://127.0.0.1:8080/$rota" || echo "/$rota: sem resposta"
  done
  if [ -f log/app.log ]; then
    echo "Linhas de erro nas últimas 500 do log: $(tail -n 500 log/app.log | grep -ciE 'fail|error|exception|crit')"
    echo "Última linha do log: $(tail -n 1 log/app.log | cut -c1-160)"
  fi

  titulo "Banco de dados"
  PGPASSWORD=$DB_PASSWORD psql -X -h 127.0.0.1 -U wrexercise -d wrexercise -At -F ' | ' 2>&1 <<'SQL'
select 'Versão', split_part(version(), ' on ', 1);
select 'PostgreSQL ativo há', date_trunc('second', now() - pg_postmaster_start_time());
select 'Tamanho do banco', pg_size_pretty(pg_database_size('wrexercise'));
select 'Conexões', count(*) || ' do app; teto do servidor ' || current_setting('max_connections') from pg_stat_activity where datname = 'wrexercise';
select '  por situação', coalesce(state, 'sem situação'), count(*) from pg_stat_activity where datname = 'wrexercise' group by state;
select 'Acerto no cache', coalesce(round(100.0 * blks_hit / nullif(blks_hit + blks_read, 0), 2) || '%', 'sem dados') from pg_stat_database where datname = 'wrexercise';
select 'Transações', xact_commit || ' confirmadas, ' || xact_rollback || ' desfeitas, ' || deadlocks || ' deadlocks' from pg_stat_database where datname = 'wrexercise';
select 'Consulta ativa mais longa', coalesce(date_trunc('milliseconds', max(now() - query_start))::text, 'nenhuma') from pg_stat_activity where datname = 'wrexercise' and state = 'active' and pid <> pg_backend_pid();
select 'Maiores tabelas', relname, pg_size_pretty(pg_total_relation_size(relid)), n_live_tup || ' linhas' from pg_stat_user_tables order by pg_total_relation_size(relid) desc limit 5;
SQL

  titulo "Disco"
  df -h "$base" 2>/dev/null | tail -n 1 | awk '{printf "Partição do app: %s usados de %s (%s), %s livres\n", $3, $2, $5, $4}'
  for d in evidencias avatares backups; do
    printf '%-10s %s\n' "$d" "$(du -sh "$dados/$d" 2>/dev/null | cut -f1)"
  done
  echo "Arquivos de evidência: $(find "$dados/evidencias" -type f 2>/dev/null | wc -l)"
  echo "Avatares importados: $(find "$dados/avatares" -type f 2>/dev/null | wc -l)"
  echo "Logs: $(du -sh log 2>/dev/null | cut -f1)"

  titulo "Backups"
  ultimo=$(ls -1t "$HOME/ambiente/backups"/wrexercise-*.dump 2>/dev/null | head -n 1)
  if [ -n "$ultimo" ]; then
    idade=$(( ($(date +%s) - $(stat -c %Y "$ultimo")) / 3600 ))
    echo "Banco (Environment): $(basename "$ultimo") (há ${idade} h)"
  else
    echo "Banco: nenhum dump em ~/ambiente/backups. No Environment: run.bat ssh-backup"
  fi
  ultimo=$(ls -1t "$dados/backups"/evidencias-*.tar.gz 2>/dev/null | head -n 1)
  if [ -n "$ultimo" ]; then
    idade=$(( ($(date +%s) - $(stat -c %Y "$ultimo")) / 3600 ))
    echo "Evidências: $(basename "$ultimo") (há ${idade} h, $(du -h "$ultimo" | cut -f1))"
  else
    echo "Evidências: nenhum backup no telefone. Rode: run.bat ssh-backup"
  fi
}

# O banco é copiado pelo Environment; aqui só os arquivos do app.
backup_sh() {
  BACKUP_BANCO=0 BACKUP_DESTINO=$dados/backups EVIDENCIAS_DIR=$dados/evidencias AVATARES_DIR=$dados/avatares \
    sh "$base/deploy/backup.sh" "$@"
}

case ${1:-} in
  implantar) implantar ;;
  iniciar) iniciar ;;
  parar) parar ;;
  status) status ;;
  stats) stats ;;
  backup) backup_sh "$@" ;;
  restore)
    # A restauração exige o app parado.
    parar
    backup_sh "$@"
    echo "Para o banco: sh ~/ambiente/deploy/ambiente.sh restore wrexercise <AAAAMMDD-HHMMSS>"
    echo "Depois, rode 'remote-termux.sh iniciar' para subir o app de novo." ;;
  reset-senha)
    ler_env
    ambiente_app
    grun "$base/app/Wrexercise" reset-senha "$2" ;;
  *) echo "Uso: remote-termux.sh implantar|iniciar|parar|status|backup|restore|reset-senha|stats" >&2; exit 2 ;;
esac
