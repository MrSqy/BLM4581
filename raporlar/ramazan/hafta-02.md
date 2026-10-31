# Hafta 02 — Ramazan Yalçın

**Ders:** BLM4581 · **Tarih:** 26 Ekim–1 Kasım 2026 · **Lider:** Ramazan

## Yaptıklarım

- **2.5 Kod çalıştırma servisi:** C# tarafından 1. haftada hazırladığım Python/pytest
  sandbox'ını çalıştıran servis katmanı eklendi.
- `IKodCalistirmaServisi` / `PythonKodCalistirmaServisi`: öğrenci kodunu `calisma.py`,
  görev testlerini `test_alistirmalar.py` olarak her istek için ayrı geçici dizine yazar,
  sandbox'ı çalıştırır, işlem bitince (hata olsa bile) dizini siler.
- `BashSandboxIslemCalistirici`: `scripts/run-python-sandbox.sh` betiğini 1. haftanın
  güvenli profiliyle çağırır — ağ kapalı, salt okunur `/calisma` bağlaması, CPU/RAM/süreç
  sınırları, 5 saniyelik süre sınırı, `python -m pytest -q`.
- `HariciIslemCalistirici`: standart çıktı ve hata akışlarını ayrı ayrı toplar;
  64 KiB çıktı sınırı aşılırsa veya istek iptal edilirse süreç grubunu önce TERM ile,
  yanıt vermezse zorla durdurur.
- Sonuç `KodCalistirmaSonucu` kaydıyla döner: çıkış kodu, çıktı, hata ve
  `Basarili` / `ZamanAsimi` (124) / `AltyapiHatasi` (125) bayrakları.
- Sandbox betiği build/publish çıktısına kopyalanıyor; servisler `Program.cs`'te
  bağımlılık enjeksiyonuna kaydedildi.

## Doğrulama

- `dotnet build EgitimPlatformu.slnx`: 0 uyarı, 0 hata.
- `dotnet test EgitimPlatformu.slnx`: **72 geçti, 0 başarısız, 0 atlanan**
  (Baran ve Nisa'nın testleri dahil; 2.5 için yeni testler eklendi).
- Yeni testler: kod ve test dosyalarının sandbox'a verilip çıktının dönmesi, boş kod/test
  reddi (sandbox başlatılmadan), hata durumunda bile geçici dosya temizliği, çıkış kodu →
  durum bayrakları, güvenli profil argümanları, çıktı sınırı aşımında ve iptalde sürecin
  durdurulması.

## Commit'ler / PR

- `9518c3c` — 2.5 sandbox kod çalıştırma servisi
- PR: [#7](https://github.com/MrSqy/BLM4581/pull/7)

## Karşılaştığım sorunlar

- Yalnızca bash sürecini durdurmak, arkasındaki Docker istemcisini açıkta bırakabilir.
  Bu yüzden süreç `setsid` ile kendi grubunda başlatılıyor ve durdurma sinyali tüm gruba gidiyor.
- Çıktı sınırı aşıldığında akış okuması beklemede takılmasın diye sınır aşımı ayrı bir
  sinyalle (`TaskCompletionSource`) bildiriliyor.

## Gelecek hafta

- **3.4:** pytest çıktısını görev bazında (`test_gorev_N`) ayrıştırmak ve kod çalıştırma
  ucunu (`/api/calistir`) bu servise bağlamak.
