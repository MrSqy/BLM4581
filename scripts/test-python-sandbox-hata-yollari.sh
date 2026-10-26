#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
sahte_docker_dizini="$repo_root/scripts/test-fixtures"
runner="$repo_root/scripts/run-python-sandbox.sh"
dogrulama="$repo_root/scripts/test-python-sandbox.sh"
durum_dizini="$(mktemp -d)"

temizle() {
  command rm -rf -- "$durum_dizini"
}
trap temizle EXIT

set +e
PATH="$sahte_docker_dizini:$PATH" \
  FAKE_DOCKER_STATE="$durum_dizini" \
  FAKE_DOCKER_SCENARIO=temizlik_hatasi \
  bash "$runner" --image sahte --timeout 1 -- true >/dev/null 2>&1
temizlik_kodu=$?
set -e
if [[ $temizlik_kodu -ne 125 ]]; then
  echo "Temizlik hatasi 125 yerine $temizlik_kodu kodunu verdi." >&2
  exit 1
fi

command rm -f "$durum_dizini/name" "$durum_dizini/start-count"
set +e
PATH="$sahte_docker_dizini:$PATH" \
  FAKE_DOCKER_STATE="$durum_dizini" \
  FAKE_DOCKER_SCENARIO=ikinci_baslatma_altyapi_hatasi \
  bash "$dogrulama" sahte >/dev/null 2>&1
altyapi_kodu=$?
set -e
if [[ $altyapi_kodu -eq 0 ]]; then
  echo "Docker altyapi hatasi kasitli pytest basarisizligi olarak kabul edildi." >&2
  exit 1
fi

echo "Sandbox temizlik ve altyapi hata yollari dogrulandi."
