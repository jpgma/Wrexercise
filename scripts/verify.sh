#!/usr/bin/env bash
# Verificação local do repositório. Não há CI: este script é a única barreira.
#   scripts/verify.sh          rápido, sem Docker
#   scripts/verify.sh --full   também testes de integração e, se algo que a afeta mudou, imagem arm64
#   scripts/verify.sh --image  como --full, mas sempre constrói a imagem arm64
set -euo pipefail

cd "$(dirname "$0")/.."

full=false
image=false
case "${1:-}" in
  "") ;;
  --full) full=true ;;
  --image) full=true; image=true ;;
  *) echo "Uso: scripts/verify.sh [--full | --image]" >&2; exit 2 ;;
esac

step() { printf '\n==> %s\n' "$1"; }

# A publicação Native AOT é lenta. Ela só roda quando muda algo que o build comum não cobre:
# a receita da imagem, as dependências ou as versões das ferramentas. Compara com o que ainda
# não foi enviado (commits locais, alterações e arquivos novos); sem upstream, constrói sempre.
image_inputs_changed() {
  local base
  base=$(git rev-parse --verify --quiet '@{upstream}') || return 0
  { git diff --name-only "$base"; git ls-files --others --exclude-standard; } |
    grep -qE '^(deploy/Dockerfile|\.dockerignore|global\.json|Directory\.(Build|Packages)\.props|.*\.csproj|Wrexercise/webapp/package(-lock)?\.json)$'
}

step "Backend: formatação"
dotnet format Wrexercise.slnx --verify-no-changes

step "Backend: build (avisos de AOT são erros)"
dotnet build Wrexercise.slnx -c Release --nologo -v quiet

step "Backend: testes unitários e de arquitetura"
dotnet test --project Wrexercise.UnitTests -c Release --no-build

step "Frontend: dependências"
(cd Wrexercise/webapp && npm ci --no-audit --no-fund)

step "Frontend: Prettier"
(cd Wrexercise/webapp && npm run --silent format:check)

step "Frontend: ESLint"
(cd Wrexercise/webapp && npm run --silent lint)

step "Frontend: tipos"
(cd Wrexercise/webapp && npm run --silent typecheck)

step "Frontend: build"
(cd Wrexercise/webapp && npm run --silent build)

if [ "$full" = true ]; then
  step "Banco de testes"
  scripts/test-db.sh

  step "Backend: testes de integração"
  dotnet test --project Wrexercise.IntegrationTests -c Release --no-build

  step "Compose: pilha com túnel"
  DOMAIN=http://localhost DB_PASSWORD=x DB_CONNECTION=x TUNNEL_IMAGE=x TUNNEL_NAME=x TUNNEL_DIR=/tmp \
    docker compose -f deploy/compose.yaml -f deploy/compose.tunnel.yaml config -q
  for f in deploy/remote-termux.sh deploy/nuvem.sh deploy/backup.sh; do sh -n "$f"; done

  if [ "$image" = true ] || image_inputs_changed; then
    step "Imagem linux/arm64 (publicação Native AOT real)"
    docker buildx build --platform linux/arm64 -f deploy/Dockerfile -t wrexercise:verify-arm64 --load .
  else
    step "Imagem linux/arm64: pulada (nada que a afete mudou; use --image para forçar)"
  fi
fi

printf '\nTudo certo.\n'
