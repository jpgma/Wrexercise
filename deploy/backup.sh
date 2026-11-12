#!/bin/sh
# Backup e restauração do banco e das evidências da pilha local de Docker. Ver docs/decisoes/0022-backups.md.
# Roda no serviço "backup" de compose.yaml, que fornece as variáveis PG* e os volumes:
#   docker compose ... run --rm backup backup
#   docker compose ... run --rm backup restore AAAAMMDD-HHMMSS   (com o app parado)
#   docker compose ... run --rm backup restore-diario wrexercise-AAAAMMDD-HHMMSS.tar.gz
#   docker compose ... run --rm backup verificar
# No telefone, o backup é o deploy/nuvem.sh, que envia ao Google Drive (decisão 0046).
# restore-diario e verificar são usados por "run.bat testar-restauracao": restauram um arquivo diário do
# telefone (com as evidências já copiadas do Drive) e conferem o resultado.
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

# Arquivo diário de deploy/nuvem.sh: wrexercise.dump, versao.txt, log/ e avatares/. As evidências não
# vêm nele: run.bat testar-restauracao as copia do espelho no Drive antes.
restore_diario() {
  nome=${1:-}
  [ -n "$nome" ] || falhar "informe o arquivo diário, por exemplo wrexercise-20261105-030000.tar.gz."
  arquivo="$destino/$nome"
  [ -f "$arquivo" ] || falhar "$arquivo não existe."
  gzip -t "$arquivo" || falhar "$arquivo está corrompido."
  tmp=$(mktemp -d)
  tar -xzf "$arquivo" -C "$tmp"
  [ -f "$tmp/wrexercise.dump" ] || falhar "$nome não tem o wrexercise.dump."
  echo "Versão implantada quando o backup foi feito: $(cat "$tmp/versao.txt" 2>/dev/null || echo desconhecida)"

  echo "Restaurando o banco a partir de $nome"
  pg_restore --clean --if-exists --no-owner --single-transaction --dbname="$PGDATABASE" "$tmp/wrexercise.dump"

  echo "Restaurando os avatares"
  mkdir -p "$avatares"
  find "$avatares" -mindepth 1 -delete
  if [ -d "$tmp/avatares" ]; then cp -R "$tmp/avatares/." "$avatares/"; fi
  rm -rf "$tmp"
}

# Confere uma restauração: o app responde, e todo arquivo citado pelo banco existe. Cada evidência também
# tem o SHA-256 conferido com o gravado no envio. Sai com erro se algo faltar.
verificar() {
  problemas=0
  tmp=$(mktemp -d)

  echo "Aplicação"
  i=0
  until wget -q -O /dev/null http://app:8080/ready 2>/dev/null; do
    i=$((i + 1))
    if [ "$i" -ge 30 ]; then
      echo "  PROBLEMA: /ready não respondeu em 30 s"
      problemas=$((problemas + 1))
      break
    fi
    sleep 1
  done
  [ "$i" -ge 30 ] || echo "  /ready respondeu"

  echo "Conteúdo do banco"
  psql -X -At -F ': ' <<'SQL' | sed 's/^/  /'
select 'Usuários', count(*) from usuarios;
select 'Grupos', count(*) from grupos;
select 'Desafios', count(*) from desafios;
select 'Atividades', count(*) from atividades;
select 'Reações', count(*) from reacoes_atividade;
select 'Evidências', count(*) from evidencias_atividade;
select 'Avatares importados', count(*) from avatares where arquivo is not null;
select 'Atividade mais recente', coalesce(max(created_at)::text, 'nenhuma') from atividades;
SQL

  echo "Evidências"
  psql -X -At -F ' ' -c "select caminho_relativo, lower(sha256), coalesce(caminho_miniatura, '-') from evidencias_atividade order by created_at" \
    >"$tmp/evidencias" || falhar "não foi possível ler as evidências do banco."
  : >"$tmp/citados"
  ok=0 faltam=0 diferentes=0 miniaturas=0
  while read -r caminho hash miniatura; do
    echo "$caminho" >>"$tmp/citados"
    if [ ! -f "$evidencias/$caminho" ]; then
      faltam=$((faltam + 1))
      [ "$faltam" -gt 10 ] || echo "  FALTA: $caminho"
    elif [ "$(sha256sum "$evidencias/$caminho" | cut -d' ' -f1)" != "$hash" ]; then
      diferentes=$((diferentes + 1))
      [ "$diferentes" -gt 10 ] || echo "  SHA-256 DIFERENTE: $caminho"
    else
      ok=$((ok + 1))
    fi
    if [ "$miniatura" != - ]; then
      echo "$miniatura" >>"$tmp/citados"
      if [ ! -f "$evidencias/$miniatura" ]; then
        miniaturas=$((miniaturas + 1))
        [ "$miniaturas" -gt 10 ] || echo "  FALTA A MINIATURA: $miniatura"
      fi
    fi
  done <"$tmp/evidencias"
  sort "$tmp/citados" >"$tmp/citados.ord"
  sobras=$(find "$evidencias" -type f | sed "s|^$evidencias/||" | sort | comm -23 - "$tmp/citados.ord" | wc -l | tr -d ' ')
  echo "  $ok íntegras, $faltam faltando, $diferentes com SHA-256 diferente, $miniaturas miniaturas faltando"
  echo "  $sobras arquivos no disco sem registro no banco (não é erro: envios que falharam no meio, por exemplo)"
  problemas=$((problemas + faltam + diferentes + miniaturas))

  echo "Avatares"
  psql -X -At -c "select arquivo from avatares where arquivo is not null" >"$tmp/avatares" ||
    falhar "não foi possível ler os avatares do banco."
  ok=0 faltam=0
  while read -r arquivo; do
    if [ -f "$avatares/$arquivo" ]; then
      ok=$((ok + 1))
    else
      faltam=$((faltam + 1))
      [ "$faltam" -gt 10 ] || echo "  FALTA: $arquivo"
    fi
  done <"$tmp/avatares"
  echo "  $ok presentes, $faltam faltando"
  problemas=$((problemas + faltam))

  rm -rf "$tmp"
  [ "$problemas" -eq 0 ] || falhar "a restauração tem $problemas problemas (veja acima)."
  echo "Restauração conferida: nenhum problema."
}

case "${1:-}" in
  backup) backup ;;
  restore) restore "${2:-}" ;;
  restore-diario) restore_diario "${2:-}" ;;
  verificar) verificar ;;
  *) falhar "uso: backup.sh backup | restore AAAAMMDD-HHMMSS | restore-diario ARQUIVO | verificar" ;;
esac
