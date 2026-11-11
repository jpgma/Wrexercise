#!/usr/bin/env bash
# Sobe o PostgreSQL dos testes de integração e aguarda até que esteja pronto.
set -euo pipefail

cd "$(dirname "$0")/.."

docker compose -f deploy/compose.dev.yaml up -d --wait db-test
