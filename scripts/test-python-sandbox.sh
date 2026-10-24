#!/usr/bin/env bash

set -euo pipefail

image="${1:-egitim-python-sandbox:hafta-01}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fixture_dir="$repo_root/sandbox/python/ornek"
runner="$repo_root/scripts/run-python-sandbox.sh"
export SANDBOX_RUN_ID="dogrulama-$$"

for arac in docker timeout date; do
  if ! command -v "$arac" >/dev/null 2>&1; then
    echo "Gerekli arac bulunamadi: $arac" >&2
    exit 1
  fi
done

if [[ ! -f "$runner" ]]; then
  echo "Sandbox calistiricisi bulunamadi: $runner" >&2
  exit 1
fi

if ! docker image inspect "$image" >/dev/null 2>&1; then
  echo "Sandbox imaji bulunamadi: $image" >&2
  exit 1
fi

sandbox_calistir() {
  set +e
  sandbox_ciktisi="$(bash "$runner" --image "$image" --timeout 5 "$@" 2>&1)"
  sandbox_kodu=$?
  set -e
}

guvenlik_hatasi=0

sandbox_calistir \
  --volume "$fixture_dir:/workspace:ro" \
  --workdir /workspace \
  -- python -m pytest -q -p no:cacheprovider
if [[ $sandbox_kodu -ne 0 ]]; then
  echo "$sandbox_ciktisi" >&2
  echo "FAIL: Ornek pytest calismasi $sandbox_kodu koduyla sonlandi." >&2
  exit 1
fi
echo "$sandbox_ciktisi"

sandbox_calistir \
  --env KASITLI_HATA=1 \
  --volume "$fixture_dir:/workspace:ro" \
  --workdir /workspace \
  -- python -m pytest -q -p no:cacheprovider
if [[ $sandbox_kodu -ne 1 ]]; then
  echo "$sandbox_ciktisi" >&2
  echo "FAIL: Kasitli pytest hatasi 1 yerine $sandbox_kodu kodunu verdi." >&2
  exit 1
fi
echo "Sandbox pytest cikis kodu sozlesmesi dogrulandi."

sandbox_calistir -- python -c \
  'import errno, socket, sys
s = socket.socket()
s.settimeout(2)
try:
    s.connect(("192.0.2.1", 80))
except OSError as hata:
    sys.exit(0 if hata.errno in {errno.ENETDOWN, errno.ENETUNREACH, errno.EHOSTUNREACH} else 2)
sys.exit(3)'
if [[ $sandbox_kodu -eq 0 ]]; then
  echo "PASS: Ag ad alani baglanti denemesini beklenen hatayla reddetti."
else
  echo "$sandbox_ciktisi" >&2
  echo "FAIL: Ag yalitimi denetimi $sandbox_kodu koduyla sonlandi." >&2
  guvenlik_hatasi=1
fi

sandbox_calistir -- id -u
if [[ $sandbox_kodu -eq 0 && $sandbox_ciktisi == "10001" ]]; then
  echo "PASS: Konteyner root olmayan UID $sandbox_ciktisi ile calisiyor."
else
  echo "$sandbox_ciktisi" >&2
  echo "FAIL: Beklenen UID 10001 dogrulanamadi; cikis kodu $sandbox_kodu." >&2
  guvenlik_hatasi=1
fi

kok_dosyasi_deneyi='import errno, os, sys
if not (os.statvfs("/").f_flag & os.ST_RDONLY):
    sys.exit(3)
try:
    open("/sandbox-yazma-denemesi", "w").write("x")
except OSError as hata:
    sys.exit(0 if hata.errno == errno.EROFS else 2)
sys.exit(4)'
sandbox_calistir -- python -c "$kok_dosyasi_deneyi"
if [[ $sandbox_kodu -eq 0 ]]; then
  echo "PASS: Kok baglama noktasi salt okunur ve yazma EROFS ile reddedildi."
else
  echo "$sandbox_ciktisi" >&2
  echo "FAIL: Salt okunur kok dosya sistemi dogrulanamadi; cikis kodu $sandbox_kodu." >&2
  guvenlik_hatasi=1
fi

set +e
docker run --rm --network none --user 10001:10001 \
  "$image" python -c "$kok_dosyasi_deneyi" >/dev/null 2>&1
yazilabilir_kok_kodu=$?
set -e
if [[ $yazilabilir_kok_kodu -eq 3 ]]; then
  echo "PASS: Salt okunur bayragi kaldirildiginda kok denetimi hatayi yakaladi."
else
  echo "FAIL: Yazilabilir kok regresyonu 3 yerine $yazilabilir_kok_kodu kodunu verdi." >&2
  guvenlik_hatasi=1
fi

sandbox_calistir -- cat /sys/fs/cgroup/memory.max
if [[ $sandbox_kodu -eq 0 && $sandbox_ciktisi == "134217728" ]]; then
  echo "PASS: Bellek siniri 128 MiB."
else
  echo "FAIL: Bellek siniri beklenen 134217728 yerine '$sandbox_ciktisi'; cikis kodu $sandbox_kodu." >&2
  guvenlik_hatasi=1
fi

sandbox_calistir -- cat /sys/fs/cgroup/cpu.max
if [[ $sandbox_kodu -eq 0 && $sandbox_ciktisi == "50000 100000" ]]; then
  echo "PASS: CPU siniri 0.5 cekirdek."
else
  echo "FAIL: CPU siniri beklenen '50000 100000' yerine '$sandbox_ciktisi'; cikis kodu $sandbox_kodu." >&2
  guvenlik_hatasi=1
fi

sandbox_calistir -- cat /sys/fs/cgroup/pids.max
if [[ $sandbox_kodu -eq 0 && $sandbox_ciktisi == "64" ]]; then
  echo "PASS: Surec siniri 64."
else
  echo "FAIL: Surec siniri beklenen 64 yerine '$sandbox_ciktisi'; cikis kodu $sandbox_kodu." >&2
  guvenlik_hatasi=1
fi

baslangic="$(date +%s)"
sandbox_calistir -- python -c 'while True: pass'
gecen_sure=$(( $(date +%s) - baslangic ))
if [[ $sandbox_kodu -eq 124 && $gecen_sure -ge 4 && $gecen_sure -le 10 ]]; then
  echo "PASS: Sonsuz islem ${gecen_sure} saniyede durduruldu."
else
  echo "$sandbox_ciktisi" >&2
  echo "FAIL: Zaman asimi kodu $sandbox_kodu, gecen sure ${gecen_sure}s." >&2
  guvenlik_hatasi=1
fi

kalan_konteynerler="$(docker container ls -a \
  --filter "label=egitim.sandbox.run-id=$SANDBOX_RUN_ID" \
  --format '{{.Names}}')"
if [[ -z $kalan_konteynerler ]]; then
  echo "PASS: Dogrulama sonrasinda sandbox konteyneri kalmadi."
else
  echo "FAIL: Temizlenmeyen sandbox konteynerleri: $kalan_konteynerler" >&2
  guvenlik_hatasi=1
fi

if [[ $guvenlik_hatasi -ne 0 ]]; then
  exit 1
fi

echo "Sandbox guvenlik ve kaynak sinirlari dogrulandi."
