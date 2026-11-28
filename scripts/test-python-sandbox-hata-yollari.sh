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

wslpath() {
  [[ $1 == -u ]] || return 1
  case "$2" in
    'C:\Users\Ada Kaya\calisma') echo '/mnt/c/Users/Ada Kaya/calisma' ;;
    'C:/Users/Ada Kaya/istek.json') echo '/mnt/c/Users/Ada Kaya/istek.json' ;;
    *) return 1 ;;
  esac
}
export -f wslpath
export FAKE_DOCKER_STATE="$durum_dizini" PATH="$sahte_docker_dizini:$PATH"

for birim in 'C:\Users\Ada Kaya\calisma:/calisma:ro' 'C:/Users/Ada Kaya/istek.json:/istek.json:ro'; do
  coproc WSL { exec setsid --wait bash "$runner" --wsl --volume "$birim" -- true; }
  wait "$WSL_PID"
  case "$birim" in
    *:/calisma:ro) beklenen='/mnt/c/Users/Ada Kaya/calisma:/calisma:ro' ;;
    *) beklenen='/mnt/c/Users/Ada Kaya/istek.json:/istek.json:ro' ;;
  esac
  [[ $(cat "$durum_dizini/volume") == "$beklenen" ]]
done

gecersiz_yol_kodu=0
bash "$runner" --wsl --volume 'C:\gecersiz:/calisma:ro' -- true 2>"$durum_dizini/wsl-hata" || gecersiz_yol_kodu=$?
[[ $gecersiz_yol_kodu -eq 125 && ! -e "$durum_dizini/name" ]]

command rm -f "$durum_dizini/start-count"
coproc WSL { FAKE_DOCKER_SCENARIO=wsl_iptal exec setsid --wait bash "$runner" --wsl --timeout 25 -- true; }
wsl_pid=$WSL_PID
wsl_girdisi=${WSL[1]}
for ((deneme = 0; deneme < 100; deneme++)); do
  [[ ! -f "$durum_dizini/start-count" ]] || break
  sleep 0.02
done
exec {wsl_girdisi}>&-
iptal_kodu=0
wait "$wsl_pid" || iptal_kodu=$?
[[ $iptal_kodu -eq 143 && ! -e "$durum_dizini/name" ]]
echo "WSL dosya yollari ve iptal temizligi dogrulandi."
