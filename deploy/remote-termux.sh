#!/bin/sh
# Executado no Termux (Android, sem root) por run.bat --ssh com SO "termux". POSIX sh.
# Roda PostgreSQL, app e túnel da Cloudflare como processos nativos, sem VM e sem Docker.
# Ver docs/decisoes/0029-implantacao-nativa-no-termux.md.
#
#   remote-termux.sh implantar <nome-tunel> <id-tunel>   troca o app, migra e (re)inicia tudo
#   remote-termux.sh iniciar                              sobe o que não estiver rodando (Termux:Boot usa este)
#   remote-termux.sh parar
#   remote-termux.sh backup | restore AAAAMMDD-HHMMSS     usa deploy/backup.sh
#   remote-termux.sh reset-senha <email>                  gera o link de redefinição de senha
#   remote-termux.sh status
#   remote-termux.sh stats                                CPU, memória, banco, disco, túnel e backups (stats.bat usa este)
set -eu

base=$HOME/wrexercise
dados=$base/dados
cd "$base"

# O binário Native AOT é glibc; o Termux é bionic. O grun (pacote glibc-runner) faz a ponte sem proot.
exigir() {
  faltam=
  for c in postgres initdb pg_ctl psql pg_dump cloudflared grun curl; do
    command -v "$c" >/dev/null 2>&1 || faltam="$faltam $c"
  done
  if [ -n "$faltam" ]; then
    echo "ERRO: faltam no Termux:$faltam" >&2
    echo "Instale e rode de novo:" >&2
    echo "  pkg install -y glibc-repo && pkg update -y && pkg install -y postgresql cloudflared glibc-runner curl" >&2
    exit 1
  fi
  # O diretório de dados só abre na mesma versão maior: um "pkg upgrade" para outra versão deixaria o banco ilegível.
  case $(postgres --version) in
    *" 18."*) ;;
    *)
      echo "ERRO: o PostgreSQL do Termux precisa ser o 18 (testado com 18.6), não: $(postgres --version)" >&2
      exit 1 ;;
  esac
  apt-mark showhold 2>/dev/null | grep -qx postgresql     || echo "AVISO: fixe a versão com: apt-mark hold postgresql" >&2
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

# Uma tarefa que reinicia sozinha se cair; a pausa evita laço apertado em erro de configuração.
iniciar_tarefa() {
  nome=$1
  shift
  if [ -f "run/$nome.pid" ] && kill -0 "$(cat "run/$nome.pid")" 2>/dev/null; then
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

iniciar_banco() {
  if [ ! -f "$dados/postgres/PG_VERSION" ]; then
    mkdir -p "$dados/postgres"
    (umask 077; printf '%s\n' "$DB_PASSWORD" > run/pw)
    initdb -D "$dados/postgres" --auth=scram-sha-256 -U wrexercise --pwfile=run/pw >/dev/null
    rm -f run/pw
  fi
  if ! pg_ctl -D "$dados/postgres" status >/dev/null 2>&1; then
    # synchronous_commit=off: um corte de energia perde no máximo ~0,5 s de commits, sem corromper o banco.
    pg_ctl -D "$dados/postgres" -l log/postgres.log -w -t 120 \
      -o "-c listen_addresses=127.0.0.1 -c max_connections=20 -c shared_buffers=128MB -c effective_cache_size=1GB \
-c work_mem=4MB -c maintenance_work_mem=64MB -c max_wal_size=512MB -c checkpoint_completion_target=0.9 \
-c synchronous_commit=off -c wal_compression=on" start >/dev/null
  fi
  if [ "$(PGPASSWORD=$DB_PASSWORD psql -h 127.0.0.1 -U wrexercise -d postgres -Atc "select 1 from pg_database where datname='wrexercise'")" != 1 ]; then
    PGPASSWORD=$DB_PASSWORD createdb -h 127.0.0.1 -U wrexercise wrexercise
  fi
}

# O app e o túnel leem a configuração do ambiente, como nos contêineres de compose.yaml.
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
  iniciar_banco
  ambiente_app
  iniciar_tarefa app grun "$base/app/Wrexercise"
  tunel=$(cat tunel.txt)
  iniciar_tarefa tunel cloudflared tunnel --no-autoupdate --url http://127.0.0.1:8080 run \
    --cred-file "$base/tunnel/${tunel%% *}.json" "${tunel##* }"
}

parar() {
  parar_tarefa tunel
  parar_tarefa app
  pkill -f "$base/app/Wrexercise" 2>/dev/null || true
  pkill -f "cloudflared tunnel" 2>/dev/null || true
  pg_ctl -D "$dados/postgres" -m fast -w stop >/dev/null 2>&1 || true
}

implantar() {
  exigir
  mkdir -p run log "$dados"
  echo "$1 $2" > tunel.txt
  chmod 600 tunel.txt "tunnel/$1.json"
  # Credenciais de túneis renomeados ou abandonados não ficam no telefone.
  find tunnel -maxdepth 1 -type f -name '*.json' ! -name "$1.json" -delete
  ler_env
  parar
  # run.bat envia o app novo em app.novo; o anterior fica em app.antigo para reverter.
  rm -rf app.antigo
  [ -d app ] && mv app app.antigo
  mv app.novo app
  chmod +x app/Wrexercise
  iniciar_banco
  ambiente_app
  mkdir -p "$dados/evidencias" "$dados/avatares"
  grun "$base/app/Wrexercise" migrate
  iniciar
  # Termux:Boot roda tudo em ~/.termux/boot/ ao ligar o telefone.
  mkdir -p "$HOME/.termux/boot"
  printf '#!/data/data/com.termux/files/usr/bin/sh\nexec sh %s iniciar\n' "$base/deploy/remote-termux.sh" > "$HOME/.termux/boot/wrexercise.sh"
  chmod 700 "$HOME/.termux/boot/wrexercise.sh"
}

status() {
  curl -fs --max-time 5 http://127.0.0.1:8080/health || echo "app sem resposta"
  echo
  pg_ctl -D "$dados/postgres" status | head -1 || true
  for p in app tunel; do
    if [ -f "run/$p.pid" ] && kill -0 "$(cat "run/$p.pid")" 2>/dev/null; then echo "$p: rodando"; else echo "$p: parado"; fi
  done
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
  # O PID do postmaster está em postmaster.pid; os demais processos do banco são filhos dele.
  pm=$(head -n 1 "$dados/postgres/postmaster.pid" 2>/dev/null)
  p_pg=
  if [ -n "$pm" ] && [ -d "/proc/$pm" ]; then p_pg="$pm $(pgrep -P "$pm" 2>/dev/null | tr '\n' ' ')"; fi
  p_tun=$(pids_de -x cloudflared)
  a_app=$(ticks_de $p_app); a_pg=$(ticks_de $p_pg); a_tun=$(ticks_de $p_tun)
  sleep "$amostra"
  d_app=$(ticks_de $p_app); d_pg=$(ticks_de $p_pg); d_tun=$(ticks_de $p_tun)
  linha_processo app "$p_app" "$a_app" "$d_app"
  linha_processo postgres "$p_pg" "$a_pg" "$d_pg"
  linha_processo cloudflared "$p_tun" "$a_tun" "$d_tun"
  echo "(RSS do PostgreSQL soma a memória compartilhada de cada processo; PSS divide-a, quando o Android deixa ler.)"

  titulo "Aplicação"
  for rota in health ready; do
    curl -s -o /dev/null --max-time 5 -w "/$rota: HTTP %{http_code} em %{time_total}s\n" "http://127.0.0.1:8080/$rota" || echo "/$rota: sem resposta"
  done
  if [ -f log/app.log ]; then
    echo "Linhas de erro nas últimas 500 do log: $(tail -n 500 log/app.log | grep -ciE 'fail|error|exception|crit')"
    echo "Última linha do log: $(tail -n 1 log/app.log | cut -c1-160)"
  fi

  titulo "Túnel"
  if [ -f log/tunel.log ]; then
    echo "Conexões registradas no log atual: $(grep -c 'Registered tunnel connection' log/tunel.log)"
    echo "Erros nas últimas 200 linhas: $(tail -n 200 log/tunel.log | grep -c ' ERR ')"
    echo "Último erro: $(grep ' ERR ' log/tunel.log | tail -n 1 | cut -c1-160)"
  else
    echo "Sem log do túnel."
  fi
  echo "Túnel configurado: $(cat tunel.txt 2>/dev/null | cut -d' ' -f1)"

  titulo "Banco de dados"
  PGPASSWORD=$DB_PASSWORD psql -h 127.0.0.1 -U wrexercise -d wrexercise -At -F ' | ' 2>&1 <<'SQL'
select 'Versão', split_part(version(), ' on ', 1);
select 'PostgreSQL ativo há', date_trunc('second', now() - pg_postmaster_start_time());
select 'Tamanho do banco', pg_size_pretty(pg_database_size('wrexercise'));
select 'Conexões', count(*) || ' de ' || current_setting('max_connections') from pg_stat_activity where datname = 'wrexercise';
select '  por situação', coalesce(state, 'sem situação'), count(*) from pg_stat_activity where datname = 'wrexercise' group by state;
select 'Acerto no cache', coalesce(round(100.0 * blks_hit / nullif(blks_hit + blks_read, 0), 2) || '%', 'sem dados') from pg_stat_database where datname = 'wrexercise';
select 'Transações', xact_commit || ' confirmadas, ' || xact_rollback || ' desfeitas, ' || deadlocks || ' deadlocks' from pg_stat_database where datname = 'wrexercise';
select 'Consulta ativa mais longa', coalesce(date_trunc('milliseconds', max(now() - query_start))::text, 'nenhuma') from pg_stat_activity where datname = 'wrexercise' and state = 'active' and pid <> pg_backend_pid();
select 'Maiores tabelas', relname, pg_size_pretty(pg_total_relation_size(relid)), n_live_tup || ' linhas' from pg_stat_user_tables order by pg_total_relation_size(relid) desc limit 5;
SQL

  titulo "Disco"
  df -h "$base" 2>/dev/null | tail -n 1 | awk '{printf "Partição do app: %s usados de %s (%s), %s livres\n", $3, $2, $5, $4}'
  for d in postgres evidencias avatares backups; do
    printf '%-10s %s\n' "$d" "$(du -sh "$dados/$d" 2>/dev/null | cut -f1)"
  done
  echo "Arquivos de evidência: $(find "$dados/evidencias" -type f 2>/dev/null | wc -l)"
  echo "Avatares importados: $(find "$dados/avatares" -type f 2>/dev/null | wc -l)"
  echo "Logs: $(du -sh log 2>/dev/null | cut -f1)"

  titulo "Backups"
  ultimo=$(ls -1t "$dados/backups"/wrexercise-*.dump 2>/dev/null | head -n 1)
  if [ -n "$ultimo" ]; then
    idade=$(( ($(date +%s) - $(stat -c %Y "$ultimo")) / 3600 ))
    echo "Último: $(basename "$ultimo") (há ${idade} h, $(du -h "$ultimo" | cut -f1))"
    echo "Total de bancos guardados: $(ls -1 "$dados/backups"/wrexercise-*.dump | wc -l)"
  else
    echo "Nenhum backup no telefone. Rode: run.bat ssh-backup"
  fi
}

backup_sh() {
  ler_env
  PGHOST=127.0.0.1 PGUSER=wrexercise PGDATABASE=wrexercise PGPASSWORD=$DB_PASSWORD \
    BACKUP_DESTINO=$dados/backups EVIDENCIAS_DIR=$dados/evidencias AVATARES_DIR=$dados/avatares \
    sh "$base/deploy/backup.sh" "$@"
}

case ${1:-} in
  implantar) implantar "$2" "$3" ;;
  iniciar) iniciar ;;
  parar) parar ;;
  status) status ;;
  stats) stats ;;
  backup) backup_sh "$@" ;;
  restore)
    # A restauração exige o app parado e o banco no ar.
    parar_tarefa app
    pkill -f "$base/app/Wrexercise" 2>/dev/null || true
    ler_env
    iniciar_banco
    backup_sh "$@"
    echo "Rode 'remote-termux.sh iniciar' para subir o app de novo." ;;
  reset-senha)
    ler_env
    ambiente_app
    grun "$base/app/Wrexercise" reset-senha "$2" ;;
  *) echo "Uso: remote-termux.sh implantar|iniciar|parar|status|backup|restore|reset-senha|stats" >&2; exit 2 ;;
esac
