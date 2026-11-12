#!/bin/sh
# Backup do Wrexercise no Google Drive, executado no Termux. POSIX sh. Ver docs/decisoes/0046-backup-no-google-drive.md.
# Chamado por deploy/remote-termux.sh (implantar, backup, restore, stats e a tarefa "backup" que roda de hora em hora).
#
#   nuvem.sh backup [implantacao]   grava o arquivo do dia (banco, avatares, logs), gira os logs e envia ao Drive
#   nuvem.sh agendado               faz o backup se o último tem 24 h ou mais; senão, reenvia o que ficou pendente
#   nuvem.sh enviar                 envia ao Drive o que está em dados/backups e espelha as evidências
#   nuvem.sh restore [AAAAMMDD-HHMMSS]   com o app parado; sem data, o mais recente
#   nuvem.sh status
#
# No Drive, pelo remoto cifrado $remoto (rclone crypt sobre $drive:wrexercise):
#   diarios/wrexercise-AAAAMMDD-HHMMSS[-implantacao].tar.gz   31 dias, nunca menos que os 7 mais novos
#   evidencias/                     espelho de todas as evidências atuais, enviado aos poucos
#   evidencias-removidas/AAAAMMDD-HHMMSS/   o que saiu do espelho naquele envio, por 31 dias
set -eu

base=$HOME/wrexercise
dados=$base/dados
backups=$dados/backups
ambiente="sh $HOME/ambiente/deploy/ambiente.sh"
remoto=${WX_REMOTO:-wx-cripto}
drive=${WX_DRIVE:-gdrive}
retencao_dias=31
minimo_diarios=7
locais=3
# Se os backups falharem por muito tempo, o log do app não cresce sem limite.
log_max=$((20 * 1024 * 1024))
log_resto=$((5 * 1024 * 1024))
# Envio ao Drive com exclusão definitiva: a lixeira também ocupa a cota.
rclone_opts="--drive-use-trash=false --stats=0"
cd "$base"

# Arquivos de situação em dados/backups, lidos por "status" (e por stats.bat) sem acessar o Drive:
#   ultimo-backup      quando o último arquivo foi gravado
#   ultimo-envio       quando o último envio ao Drive terminou bem
#   ultimo-resultado   origem, arquivo, tamanhos, durações e resultado do envio do último backup
#   ultimo-erro        a última falha, com a etapa
#   drive              uso no Drive, medido depois de cada envio bem-sucedido
#   pendente           o último envio falhou; a tarefa tenta de novo a cada hora
registrar_erro() {
  erro_registrado=1
  mkdir -p "$backups"
  printf '%s | %s\n' "$(date -u '+%Y-%m-%d %H:%M:%S UTC')" "$1" > "$backups/ultimo-erro" 2>/dev/null || true
}

falhar() {
  echo "ERRO: $1" >&2
  registrar_erro "$1"
  exit 1
}

agora() { date -u +%Y%m%d-%H%M%S; }

bytes_de() { wc -c <"$1" 2>/dev/null | tr -d ' ' || echo 0; }

# 1536 → "1.5 KB". Vazio vira "?".
humano() {
  [ -n "${1:-}" ] || { echo "?"; return; }
  awk -v b="$1" 'BEGIN { split("B KB MB GB TB", u); i = 1
    while (b >= 1024 && i < 5) { b /= 1024; i++ }
    if (i == 1) printf "%d %s", b, u[i]; else printf "%.1f %s", b, u[i] }'
}

# Valor de "chave=valor" num arquivo de situação.
valor() { sed -n "s/^$1=//p" "$2" 2>/dev/null | head -n 1; }

# Número de um campo de um JSON simples do rclone ({"count":1,"bytes":2}).
json_num() { printf '%s' "$2" | sed -n "s/.*\"$1\":\([0-9]*\).*/\1/p"; }

configurado() {
  command -v rclone >/dev/null 2>&1 && rclone listremotes 2>/dev/null | grep -qx "$remoto:"
}

aviso_sem_rclone() {
  echo "AVISO: rclone ou o remoto '$remoto' não está configurado. O backup ficou só no telefone." >&2
  echo "Configure seguindo o README (seção Backups)." >&2
}

# Um backup por vez: a implantação e a tarefa agendada podem coincidir. A trava some com o processo;
# uma trava de mais de 2 h é de um processo morto (reinício do telefone, por exemplo).
travar() {
  mkdir -p "$base/run"
  trava=$base/run/nuvem.lock
  if [ -n "$(find "$trava" -maxdepth 0 -mmin +120 2>/dev/null)" ]; then rmdir "$trava" 2>/dev/null || true; fi
  tentativas=0
  until mkdir "$trava" 2>/dev/null; do
    tentativas=$((tentativas + 1))
    [ "$tentativas" -lt 60 ] || falhar "outro backup está em andamento ($trava)."
    sleep 2
  done
  # Uma saída com erro que não passou por falhar (set -e) também fica registrada.
  trap 'st=$?; rmdir "$trava" 2>/dev/null; [ "$st" = 0 ] || [ -n "${erro_registrado:-}" ] || registrar_erro "saiu com o código $st; veja log/backup.log"' EXIT
  trap 'exit 1' INT TERM
}

senha_banco() {
  [ -f "$base/.env" ] || falhar "$base/.env não existe. Implante o app antes."
  sed -n 's/^DB_PASSWORD=//p' "$base/.env"
}

# Esvazia o arquivo sem trocá-lo: o app escreve nele com >> (O_APPEND) e continua no início.
esvaziar() {
  [ -f "$1" ] && : > "$1"
  return 0
}

# Arquivo do dia, só no telefone. Os logs vão junto e depois são esvaziados.
criar_arquivo() {
  sufixo=${1:+-$1}
  data=$(agora)
  nome=wrexercise-$data$sufixo.tar.gz
  tmp=$backups/tmp-$data
  # Sobra de um backup interrompido.
  rm -rf "$backups"/tmp-*
  mkdir -p "$backups" "$dados/avatares" "$tmp/log"

  $ambiente aguardar
  # O papel wrexercise é dono do banco: não precisa de superusuário. Grava em .tmp e confere antes.
  PGPASSWORD=$(senha_banco) pg_dump -h 127.0.0.1 -p 5432 -U wrexercise -d wrexercise \
    --format=custom --file="$tmp/wrexercise.dump" || falhar "pg_dump falhou (banco fora do ar ou senha errada)."
  pg_restore --list "$tmp/wrexercise.dump" >/dev/null || falhar "o dump gerado não pode ser lido."

  cp "$base/app/versao.txt" "$tmp/versao.txt" 2>/dev/null || echo "desconhecida" > "$tmp/versao.txt"
  for l in app backup; do
    if [ -f "$base/log/$l.log" ]; then cp "$base/log/$l.log" "$tmp/log/$l.log"; fi
  done

  tar -czf "$backups/$nome.tmp" -C "$tmp" . -C "$dados" avatares
  gzip -t "$backups/$nome.tmp" || falhar "o arquivo gerado está corrompido."
  mv "$backups/$nome.tmp" "$backups/$nome"
  dump_bytes=$(bytes_de "$tmp/wrexercise.dump")
  logs_bytes=$(cat "$tmp"/log/*.log 2>/dev/null | wc -c | tr -d ' ')
  avatares_bytes=$(( $(du -sk "$dados/avatares" | cut -f1) * 1024 ))
  arquivo_bytes=$(bytes_de "$backups/$nome")
  rm -rf "$tmp"

  esvaziar "$base/log/app.log"
  esvaziar "$base/log/backup.log"
  date -u '+%Y-%m-%d %H:%M:%S UTC' > "$backups/ultimo-backup"

  # Só os mais novos ficam no telefone; o Drive guarda 31 dias.
  ls -1t "$backups"/wrexercise-*.tar.gz 2>/dev/null | tail -n +$((locais + 1)) | while read -r f; do rm -f "$f"; done
  # Arquivos do formato anterior (evidencias-*, avatares-*): mesma retenção de 7 dias de antes.
  find "$backups" -maxdepth 1 -type f \( -name 'evidencias-*.tar.gz' -o -name 'avatares-*.tar.gz' \) -mtime +7 -delete
  rm -f "$backups"/*.tmp

  echo "Arquivo $nome gravado ($(du -h "$backups/$nome" | cut -f1))."
}

# Data limite no formato dos nomes: o que for anterior a ela tem mais de 31 dias.
limite() { date -u -d "$retencao_dias days ago" +%Y%m%d-%H%M%S; }

# Verdadeiro se a data $1 (AAAAMMDD-HHMMSS) vem antes de $2. Outros nomes nunca vêm antes.
antes() {
  case $1 in [0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9]) ;; *) return 1 ;; esac
  [ "$(echo "$1" | tr -d -)" -lt "$(echo "$2" | tr -d -)" ]
}

podar() {
  corte=$(limite)
  # Os 7 mais novos ficam sempre: uma sequência de falhas nunca esvazia o Drive.
  rclone lsf $rclone_opts --files-only "$remoto:diarios" | grep '^wrexercise-.*\.tar\.gz$' | sort -r |
    tail -n +$((minimo_diarios + 1)) | while read -r f; do
      d=$(echo "$f" | sed 's/^wrexercise-\([0-9]\{8\}-[0-9]\{6\}\).*/\1/')
      if antes "$d" "$corte"; then
        rclone deletefile $rclone_opts "$remoto:diarios/$f" && echo "Removido do Drive: diarios/$f"
      fi
    done
  rclone lsf $rclone_opts --dirs-only "$remoto:evidencias-removidas" 2>/dev/null | while read -r d; do
    d=${d%/}
    if antes "$d" "$corte"; then
      rclone purge $rclone_opts "$remoto:evidencias-removidas/$d" && echo "Removido do Drive: evidencias-removidas/$d"
    fi
  done
}

# Espelha as evidências antes de enviar os arquivos: todo arquivo citado pelo dump já está no Drive.
# Cada passo confere o próprio erro: dentro de "if !", o set -e não vale.
enviar() {
  if ! configurado; then
    aviso_sem_rclone
    return 1
  fi
  data=$(agora)
  echo "Espelhando as evidências no Drive"
  # --max-delete: uma pasta de evidências vazia por engano não esvazia o espelho.
  rclone sync $rclone_opts --max-delete 100 "$dados/evidencias" "$remoto:evidencias" \
    --backup-dir "$remoto:evidencias-removidas/$data" ||
    { registrar_erro "envio: espelho das evidências (rede, token do Drive ou mais de 100 remoções)"; return 1; }
  echo "Enviando os arquivos diários"
  # copy só envia o que ainda não está lá: um envio que falhou é refeito na próxima vez.
  rclone copy $rclone_opts "$backups" "$remoto:diarios" --include 'wrexercise-*.tar.gz' ||
    { registrar_erro "envio: arquivos diários (rede, token do Drive ou cota cheia)"; return 1; }
  podar || registrar_erro "envio: remoção dos backups antigos do Drive (tenta de novo no próximo envio)"
  date -u '+%Y-%m-%d %H:%M:%S UTC' > "$backups/ultimo-envio"
  rm -f "$backups/pendente"
  medir_drive || echo "AVISO: não foi possível medir o uso do Drive." >&2
  echo "Envio ao Drive concluído."
}

# Uso no Drive, guardado para status e stats.bat lerem sem acessar a rede (stats --watch repete a cada 3 s).
medir_drive() {
  {
    echo "medido=$(date -u '+%Y-%m-%d %H:%M:%S UTC')"
    for d in diarios evidencias evidencias-removidas; do
      j=$(rclone size $rclone_opts --json "$remoto:$d" 2>/dev/null) || j=
      echo "$d.arquivos=$(json_num count "$j")"
      echo "$d.bytes=$(json_num bytes "$j")"
    done
    lista=$(rclone lsf $rclone_opts --files-only "$remoto:diarios" 2>/dev/null | grep '^wrexercise-' | sort)
    echo "diarios.mais_antigo=$(echo "$lista" | head -n 1)"
    echo "diarios.mais_novo=$(echo "$lista" | tail -n 1)"
    j=$(rclone about $rclone_opts --json "$drive:" 2>/dev/null) || j=
    for k in total used trashed free; do echo "cota.$k=$(json_num "$k" "$j")"; done
  } > "$backups/drive.tmp"
  mv "$backups/drive.tmp" "$backups/drive"
}

# Falha ao criar o arquivo é erro (a implantação para). Falha no envio só avisa e fica pendente.
backup() {
  travar
  t0=$(date +%s)
  criar_arquivo "${1:-}"
  t1=$(date +%s)
  if ! configurado; then
    aviso_sem_rclone
    envio="não configurado"
    touch "$backups/pendente"
  elif (enviar); then
    envio=ok
  else
    envio=falhou
    touch "$backups/pendente"
    echo "AVISO: o envio ao Drive falhou; o arquivo ficou no telefone e a tarefa tenta de novo a cada hora." >&2
  fi
  t2=$(date +%s)
  {
    echo "inicio=$(date -u -d "@$t0" '+%Y-%m-%d %H:%M:%S UTC')"
    echo "origem=${origem:-${1:-manual}}"
    echo "arquivo=$nome"
    echo "arquivo.bytes=$arquivo_bytes"
    echo "dump.bytes=$dump_bytes"
    echo "avatares.bytes=$avatares_bytes"
    echo "logs.bytes=$logs_bytes"
    echo "evidencias.arquivos=$(find "$dados/evidencias" -type f 2>/dev/null | wc -l | tr -d ' ')"
    echo "duracao.gravacao=$((t1 - t0))"
    echo "duracao.envio=$((t2 - t1))"
    echo "envio=$envio"
  } > "$backups/ultimo-resultado"
}

recente() { [ -n "$(find "$1" -mmin -"$2" 2>/dev/null)" ]; }

# Laço da tarefa "backup" (remote-termux.sh), de hora em hora.
agendado() {
  if [ -f "$base/log/app.log" ] && [ "$(wc -c <"$base/log/app.log")" -gt "$log_max" ]; then
    tail -c "$log_resto" "$base/log/app.log" > "$base/log/app.log.tmp"
    cat "$base/log/app.log.tmp" > "$base/log/app.log"
    rm -f "$base/log/app.log.tmp"
  fi
  if ! recente "$backups/ultimo-backup" 1440; then
    echo "== Backup agendado em $(agora)"
    origem=agendado
    backup
  elif [ -f "$backups/pendente" ] && configurado; then
    echo "== Reenvio pendente em $(agora)"
    travar
    enviar
    sed -i 's/^envio=.*/envio=ok (reenviado em '"$(date -u '+%Y-%m-%d %H:%M UTC')"')/' "$backups/ultimo-resultado" 2>/dev/null || true
  fi
}

# Restaura banco, avatares e evidências. Sem o rclone, usa um arquivo local e mantém as evidências do telefone.
restore() {
  data=${1:-}
  travar
  destino=$backups/restauracao
  rm -rf "$destino"
  mkdir -p "$destino/conteudo"

  local_=$(ls -1 "$backups"/wrexercise-"$data"*.tar.gz 2>/dev/null | sort | tail -n 1)
  if configurado; then
    nome=$(rclone lsf $rclone_opts --files-only "$remoto:diarios" | grep "^wrexercise-$data.*\.tar\.gz$" | sort | tail -n 1)
    [ -n "$nome" ] || falhar "nenhum backup ${data:+de $data }no Drive."
    echo "Baixando $nome"
    rclone copy $rclone_opts "$remoto:diarios/$nome" "$destino"
    arquivo=$destino/$nome
  elif [ -n "$local_" ]; then
    aviso_sem_rclone
    arquivo=$local_
    nome=$(basename "$arquivo")
  else
    falhar "nenhum backup ${data:+de $data }no telefone, e o rclone não está configurado."
  fi
  data=$(echo "$nome" | sed 's/^wrexercise-\([0-9]\{8\}-[0-9]\{6\}\).*/\1/')
  tar -xzf "$arquivo" -C "$destino/conteudo"

  $ambiente aguardar
  echo "Restaurando o banco a partir de $nome"
  PGPASSWORD=$(senha_banco) pg_restore -h 127.0.0.1 -p 5432 -U wrexercise -d wrexercise \
    --clean --if-exists --no-owner --single-transaction "$destino/conteudo/wrexercise.dump"

  echo "Restaurando os avatares"
  mkdir -p "$dados/avatares"
  find "$dados/avatares" -mindepth 1 -delete
  cp -R "$destino/conteudo/avatares/." "$dados/avatares/"

  if configurado; then
    echo "Restaurando as evidências do espelho no Drive"
    mkdir -p "$dados/evidencias"
    rclone sync $rclone_opts "$remoto:evidencias" "$dados/evidencias"
    # Um backup antigo pode citar evidências removidas depois dele: elas voltam de evidencias-removidas.
    rclone lsf $rclone_opts --dirs-only "$remoto:evidencias-removidas" 2>/dev/null | while read -r d; do
      d=${d%/}
      if antes "$data" "$d"; then
        rclone copy $rclone_opts --ignore-existing "$remoto:evidencias-removidas/$d" "$dados/evidencias"
      fi
    done
  else
    echo "AVISO: as evidências do telefone ficaram como estão." >&2
  fi

  rm -rf "$destino"
  echo "Restauração de $nome concluída."
}

idade() {
  if [ -f "$1" ]; then
    echo "$(cat "$1") (há $(( ($(date +%s) - $(stat -c %Y "$1")) / 3600 )) h)"
  else
    echo "nunca"
  fi
}

mtime() { stat -c %Y "$1" 2>/dev/null || echo 0; }

# Só lê arquivos locais (rápido, funciona sem rede). O uso do Drive é o medido no último envio.
status() {
  r=$backups/ultimo-resultado
  dv=$backups/drive

  echo "Último backup: $(idade "$backups/ultimo-backup")"
  if [ -f "$r" ]; then
    g=$(valor duracao.gravacao "$r"); e=$(valor duracao.envio "$r")
    echo "  origem $(valor origem "$r") | $(valor arquivo "$r") ($(humano "$(valor arquivo.bytes "$r")"))"
    echo "  conteúdo: dump $(humano "$(valor dump.bytes "$r")") | avatares $(humano "$(valor avatares.bytes "$r")") | logs $(humano "$(valor logs.bytes "$r")")"
    echo "  duração: gravação ${g:-?} s + envio ${e:-?} s = $(( ${g:-0} + ${e:-0} )) s | envio: $(valor envio "$r")"
  fi
  if [ -f "$backups/ultimo-backup" ]; then
    echo "Próximo backup agendado: a partir de $(date -d "@$(( $(mtime "$backups/ultimo-backup") + 86400 ))" '+%Y-%m-%d %H:%M %Z') (a tarefa confere a cada hora)"
  fi
  if [ -d "$base/run/nuvem.lock" ]; then
    echo "Backup em andamento há $(( ($(date +%s) - $(mtime "$base/run/nuvem.lock")) / 60 )) min."
  fi

  echo "Último envio ao Drive: $(idade "$backups/ultimo-envio")"
  if [ -f "$backups/ultimo-backup" ] && ! recente "$backups/ultimo-envio" 1560; then
    echo "AVISO: nenhum envio ao Drive nas últimas 26 h."
  fi
  if [ -f "$backups/pendente" ]; then
    echo "AVISO: há envio pendente (a tarefa tenta de novo a cada hora)."
  fi
  # Um erro mais novo que o último envio bem-sucedido ainda vale.
  if [ -f "$backups/ultimo-erro" ] && [ "$(mtime "$backups/ultimo-erro")" -ge "$(mtime "$backups/ultimo-envio")" ]; then
    echo "ÚLTIMO ERRO: $(cat "$backups/ultimo-erro")"
    if [ -s "$base/log/backup.log" ]; then
      echo "  fim de log/backup.log:"
      tail -n 5 "$base/log/backup.log" | cut -c1-160 | sed 's/^/    /'
    fi
  fi

  echo "No telefone: $(ls -1 "$backups"/wrexercise-*.tar.gz 2>/dev/null | wc -l | tr -d ' ') arquivos diários, $(du -sh "$backups" 2>/dev/null | cut -f1) em dados/backups"
  if command -v rclone >/dev/null 2>&1; then
    v=$(rclone version 2>/dev/null | head -n 1)
    if configurado; then echo "rclone: $v, remoto '$remoto' configurado"; else echo "rclone: $v, SEM o remoto '$remoto' (README, seção Backups)"; fi
  else
    echo "rclone: não instalado (pkg install rclone); o backup fica só no telefone"
  fi

  if [ -f "$dv" ]; then
    db=$(valor diarios.bytes "$dv"); eb=$(valor evidencias.bytes "$dv"); rb=$(valor evidencias-removidas.bytes "$dv")
    echo "No Drive (medido em $(valor medido "$dv")): $(humano "$(( ${db:-0} + ${eb:-0} + ${rb:-0} ))") no total"
    echo "  diários: $(valor diarios.arquivos "$dv") arquivos, $(humano "$db"), de $(valor diarios.mais_antigo "$dv" | cut -c12-26) a $(valor diarios.mais_novo "$dv" | cut -c12-26)"
    em=$(valor evidencias.arquivos "$dv")
    el=$(find "$dados/evidencias" -type f 2>/dev/null | wc -l | tr -d ' ')
    echo "  evidências: $em arquivos, $(humano "$eb") (no telefone agora: $el)"
    echo "  evidências removidas (31 dias): $(valor evidencias-removidas.arquivos "$dv") arquivos, $(humano "$rb")"
    t=$(valor cota.total "$dv"); u=$(valor cota.used "$dv")
    if [ -n "$t" ] && [ -n "$u" ] && [ "$t" -gt 0 ]; then
      echo "  cota da conta: $(humano "$u") de $(humano "$t") ($((u * 100 / t))%), livres $(humano "$(valor cota.free "$dv")"), lixeira $(humano "$(valor cota.trashed "$dv")")"
      if [ $((u * 100 / t)) -ge 80 ]; then echo "AVISO: a cota do Drive passou de 80%. Ver docs/decisoes/0046 (Impacto)."; fi
    fi
  fi
}

case ${1:-} in
  backup) backup "${2:-}" ;;
  agendado) agendado ;;
  enviar) travar; enviar ;;
  restore) restore "${2:-}" ;;
  status) status ;;
  *) echo "Uso: nuvem.sh backup [implantacao] | agendado | enviar | restore [AAAAMMDD-HHMMSS] | status" >&2; exit 2 ;;
esac
