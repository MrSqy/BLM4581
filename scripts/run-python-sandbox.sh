#!/usr/bin/env bash

set -euo pipefail

image="egitim-python-sandbox:hafta-01"
sure=5
ek_docker_argumanlari=()
komut=()

kullanim() {
  cat >&2 <<'EOF'
Kullanim:
  bash scripts/run-python-sandbox.sh [secenekler] -- KOMUT [ARGUMAN...]

Secenekler:
  --image IMaj       Kullanilacak sandbox imaji
  --timeout SANIYE   Calisma suresi siniri (varsayilan: 5)
  --env AD=DEGER     Konteynere ortam degiskeni aktarir; tekrarlanabilir
  --volume BAGLANTI  Konteynere birim baglar; tekrarlanabilir
  --workdir DIZIN    Konteyner calisma dizini
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --image)
      [[ $# -ge 2 ]] || { kullanim; exit 2; }
      image="$2"
      shift 2
      ;;
    --timeout)
      [[ $# -ge 2 ]] || { kullanim; exit 2; }
      sure="$2"
      shift 2
      ;;
    --env|--volume|--workdir)
      [[ $# -ge 2 ]] || { kullanim; exit 2; }
      ek_docker_argumanlari+=("$1" "$2")
      shift 2
      ;;
    --)
      shift
      komut=("$@")
      break
      ;;
    *)
      echo "Bilinmeyen secenek: $1" >&2
      kullanim
      exit 2
      ;;
  esac
done

if [[ ! $sure =~ ^[1-9][0-9]*$ || ${#komut[@]} -eq 0 ]]; then
  kullanim
  exit 2
fi

for arac in docker timeout; do
  if ! command -v "$arac" >/dev/null 2>&1; then
    echo "Gerekli arac bulunamadi: $arac" >&2
    exit 125
  fi
done

sandbox_argumanlari=(
  --network none
  --cpus 0.5
  --memory 128m
  --memory-swap 128m
  --pids-limit 64
  --read-only
  --tmpfs /tmp:rw,noexec,nosuid,size=16m
  --cap-drop ALL
  --security-opt no-new-privileges
  --user 10001:10001
)

konteyner_adi="egitim-python-sandbox-${PPID}-$$-${RANDOM}"
calisma_etiketi="${SANDBOX_RUN_ID:-$konteyner_adi}"
aktif_konteyner=""

konteyneri_temizle() {
  local ad="$1"
  local liste

  if ! liste="$(docker container ls -a \
    --filter "name=^/${ad}$" \
    --format '{{.Names}}')"; then
    echo "Konteyner durumu sorgulanamadi: $ad" >&2
    return 1
  fi

  if [[ $liste == "$ad" ]]; then
    if ! docker container rm --force "$ad" >/dev/null; then
      echo "Konteyner temizlenemedi: $ad" >&2
      return 1
    fi
  fi

  if ! liste="$(docker container ls -a \
    --filter "name=^/${ad}$" \
    --format '{{.Names}}')"; then
    echo "Temizlik sonucu sorgulanamadi: $ad" >&2
    return 1
  fi
  if [[ -n $liste ]]; then
    echo "Konteyner temizlendikten sonra hala mevcut: $ad" >&2
    return 1
  fi

  aktif_konteyner=""
}

cikis_temizligi() {
  local kod=$?
  trap - EXIT
  if [[ -n $aktif_konteyner ]] && ! konteyneri_temizle "$aktif_konteyner"; then
    kod=125
  fi
  exit "$kod"
}

trap cikis_temizligi EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

if ! konteyner_id="$(docker create \
  --name "$konteyner_adi" \
  --label egitim.sandbox.runner=hafta-01 \
  --label "egitim.sandbox.run-id=$calisma_etiketi" \
  "${sandbox_argumanlari[@]}" \
  "${ek_docker_argumanlari[@]}" \
  "$image" \
  "${komut[@]}")"; then
  echo "Sandbox konteyneri olusturulamadi." >&2
  exit 125
fi
if [[ -z $konteyner_id ]]; then
  echo "Docker create konteyner kimligi dondurmedi." >&2
  exit 125
fi
aktif_konteyner="$konteyner_adi"

baslangic=$SECONDS
set +e
timeout --signal=TERM --kill-after=1s "${sure}s" \
  docker start --attach "$konteyner_adi"
baslatma_kodu=$?
set -e
gecen_sure=$((SECONDS - baslangic))

if [[ $baslatma_kodu -eq 124 || ( $baslatma_kodu -eq 137 && $gecen_sure -ge $sure ) ]]; then
  if ! konteyneri_temizle "$konteyner_adi"; then
    exit 125
  fi
  echo "Sandbox ${sure} saniyelik sure sinirini asti ve temizlendi." >&2
  exit 124
fi

if ! durum="$(docker container inspect \
  --format '{{.State.Status}} {{.State.ExitCode}}' \
  "$konteyner_adi")"; then
  echo "Sandbox cikis durumu okunamadi." >&2
  exit 125
fi
read -r calisma_durumu uygulama_kodu <<<"$durum"
if [[ $calisma_durumu != "exited" || ! $uygulama_kodu =~ ^[0-9]+$ ]]; then
  echo "Sandbox beklenmeyen durumda sonlandi: $durum (docker kodu: $baslatma_kodu)" >&2
  exit 125
fi

if ! konteyneri_temizle "$konteyner_adi"; then
  exit 125
fi
exit "$uygulama_kodu"
