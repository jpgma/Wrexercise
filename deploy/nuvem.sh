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

falhar() {
  echo "ERRO: $1" >&2
  exit 1
}

agora() { date -u +%Y%m%d-%H%M%S; }

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
  trap 'rmdir "$trava" 2>/dev/null' EXIT
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
    --format=custom --file="$tmp/wrexercise.dump"
  pg_restore --list "$tmp/wrexercise.dump" >/dev/null || falhar "o dump gerado não pode ser lido."

  cp "$base/app/versao.txt" "$tmp/versao.txt" 2>/dev/null || echo "desconhecida" > "$tmp/versao.txt"
  for l in app backup; do
    if [ -f "$base/log/$l.log" ]; then cp "$base/log/$l.log" "$tmp/log/$l.log"; fi
  done

  tar -czf "$backups/$nome.tmp" -C "$tmp" . -C "$dados" avatares
  gzip -t "$backups/$nome.tmp" || falhar "o arquivo gerado está corrompido."
  mv "$backups/$nome.tmp" "$backups/$nome"
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
    --backup-dir "$remoto:evidencias-removidas/$data" || return 1
  echo "Enviando os arquivos diários"
  # copy só envia o que ainda não está lá: um envio que falhou é refeito na próxima vez.
  rclone copy $rclone_opts "$backups" "$remoto:diarios" --include 'wrexercise-*.tar.gz' || return 1
  podar || echo "AVISO: a remoção dos backups antigos do Drive falhou; tenta de novo no próximo envio." >&2
  date -u '+%Y-%m-%d %H:%M:%S UTC' > "$backups/ultimo-envio"
  rm -f "$backups/pendente"
  echo "Envio ao Drive concluído."
}

# Falha ao criar o arquivo é erro (a implantação para). Falha no envio só avisa e fica pendente.
backup() {
  travar
  criar_arquivo "${1:-}"
  if ! (enviar); then
    touch "$backups/pendente"
    echo "AVISO: o envio ao Drive falhou ou não está configurado; o arquivo ficou no telefone e será reenviado." >&2
  fi
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
    backup
  elif [ -f "$backups/pendente" ] && configurado; then
    echo "== Reenvio pendente em $(agora)"
    travar
    enviar
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

status() {
  echo "Último backup no telefone: $(idade "$backups/ultimo-backup")"
  echo "Último envio ao Drive: $(idade "$backups/ultimo-envio")"
  if [ -f "$backups/ultimo-envio" ] && ! recente "$backups/ultimo-envio" 1560; then
    echo "AVISO: o último envio ao Drive tem mais de 26 h."
  fi
  [ -f "$backups/pendente" ] && echo "AVISO: há envio pendente (a tarefa tenta de novo a cada hora)."
  echo "Arquivos no telefone: $(ls -1 "$backups"/wrexercise-*.tar.gz 2>/dev/null | wc -l) ($(du -sh "$backups" 2>/dev/null | cut -f1))"
  if configurado; then
    echo "Arquivos diários no Drive: $(rclone lsf $rclone_opts --files-only "$remoto:diarios" 2>/dev/null | wc -l)"
    rclone about "$drive:" 2>/dev/null | sed 's/^/Drive: /' || echo "Drive: cota indisponível"
  else
    echo "Drive: rclone ou o remoto '$remoto' não está configurado."
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
