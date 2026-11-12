#!/bin/sh
# Backup e restauração do banco e das evidências da pilha local de Docker. Ver docs/decisoes/0022-backups.md.
# Roda no serviço "backup" de compose.yaml, que fornece as variáveis PG* e os volumes:
#   docker compose ... run --rm backup backup
#   docker compose ... run --rm backup restore AAAAMMDD-HHMMSS   (com o app parado)
# No telefone, o backup é o deploy/nuvem.sh, que envia ao Google Drive (decisão 0046).
set -eu

destino=/backups
evidencias=/dados/evidencias
avatares=/dados/avatares
retencao_dias=7

falhar() {
  echo "ERRO: $1" >&2
  exit 1
}

backup() {
  data=$(date +%Y%m%d-%H%M%S)
  dump="$destino/wrexercise-$data.dump"
  arquivo="$destino/evidencias-$data.tar.gz"
  arquivo_avatares="$destino/avatares-$data.tar.gz"

  # Grava em .tmp e só renomeia depois de conferir: um backup pela metade nunca parece válido.
  pg_dump --format=custom --file="$dump.tmp"
  pg_restore --list "$dump.tmp" >/dev/null || falhar "o dump gerado não pode ser lido."

  tar -czf "$arquivo.tmp" -C "$evidencias" .
  gzip -t "$arquivo.tmp" || falhar "o arquivo de evidências gerado está corrompido."

  mkdir -p "$avatares"
  tar -czf "$arquivo_avatares.tmp" -C "$avatares" .
  gzip -t "$arquivo_avatares.tmp" || falhar "o arquivo de avatares gerado está corrompido."

  mv "$dump.tmp" "$dump"
  mv "$arquivo.tmp" "$arquivo"
  mv "$arquivo_avatares.tmp" "$arquivo_avatares"

  find "$destino" -maxdepth 1 -type f \
    \( -name 'wrexercise-*.dump' -o -name 'easyfit-*.dump' -o -name 'evidencias-*.tar.gz' -o -name 'avatares-*.tar.gz' \) \
    -mtime +"$retencao_dias" -delete
  rm -f "$destino"/*.tmp

  echo "Backup $data concluído:"
  ls -lh "$dump" "$arquivo" "$arquivo_avatares"
}

restore() {
  data=${1:-}
  [ -n "$data" ] || falhar "informe a data do backup, por exemplo 20261105-030000."
  dump="$destino/wrexercise-$data.dump"
  # Backups anteriores à troca de nome (decisão 0026) têm o prefixo antigo.
  [ -f "$dump" ] || [ ! -f "$destino/easyfit-$data.dump" ] || dump="$destino/easyfit-$data.dump"
  arquivo="$destino/evidencias-$data.tar.gz"
  [ -f "$dump" ] || falhar "$dump não existe."
  [ -f "$arquivo" ] || falhar "$arquivo não existe."
  # Backups anteriores aos avatares não têm este arquivo: os avatares ficam como estão.
  arquivo_avatares="$destino/avatares-$data.tar.gz"

  echo "Restaurando o banco a partir de $dump"
  pg_restore --clean --if-exists --no-owner --single-transaction --dbname="$PGDATABASE" "$dump"

  echo "Restaurando as evidências a partir de $arquivo"
  find "$evidencias" -mindepth 1 -delete
  # Como root, o tar preserva o dono dos arquivos (o usuário da aplicação).
  tar -xzf "$arquivo" -C "$evidencias"

  if [ -f "$arquivo_avatares" ]; then
    echo "Restaurando os avatares a partir de $arquivo_avatares"
    mkdir -p "$avatares"
    find "$avatares" -mindepth 1 -delete
    tar -xzf "$arquivo_avatares" -C "$avatares"
  fi

  echo "Restauração de $data concluída."
}

case "${1:-}" in
  backup) backup ;;
  restore) restore "${2:-}" ;;
  *) falhar "uso: backup.sh backup | restore AAAAMMDD-HHMMSS" ;;
esac
