# Geri alınabilir içerik yükleme (Teknik Mülakat ve C#)

İki hazır paket `docs/ornekler/` altındadır. Paketler `scripts/zip-paket.py` ile ilgili zip'ten üretilmiştir:

| Paket | Kategori | İçerik |
|---|---|---|
| `tma-3-ders-v2.json` | TMA (mevcut kategori) | TMA-00..02, ders başına 7 görev |
| `cs-3-ders-v2.json` | CS (yeni kategori) | CS-01..03, ders başına 7 görev |

Bütün görevler `aciklama` türündedir. Mevcut çalıştırıcı yalnız tek dosyalı Python + stdout kontrolünü destekler. C# ve pytest tabanlı görevler bu yüzden çalıştırılamaz; başlangıç kodları görev metninde kod bloğu olarak gösterilir, Çalıştır/Test sunulmaz. `cozumler/` okunmaz.

## Komutlar

Yerel DB yönetim komutlarıdır; HTTP kimliği istemez, iş bitince kapanır. Açık web sunucusu durdurulmaz. Depo kökünde, bir kez derledikten sonra:

```bash
dotnet build EgitimPlatformu.slnx --no-restore
```

```bash
K="ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --no-restore --no-launch-profile --project src/EgitimPlatformu.Web --"
```

- **A** — Teknik Mülakat'a 3 ders ekle: `--icerik-aktar "$PWD/docs/ornekler/tma-3-ders-v2.json"`
- **B** — Bu 3 dersi sil, kategori kalsın: `--icerik-kaldir "$PWD/docs/ornekler/tma-3-ders-v2.json"`
- **C** — C# kategorisini 3 dersle aç: `--icerik-aktar "$PWD/docs/ornekler/cs-3-ders-v2.json"`
- **D** — C# kategorisini dersleriyle sil: `--icerik-kaldir "$PWD/docs/ornekler/cs-3-ders-v2.json" --kategori-dahil`

## Kaldırma kuralları

- Yalnız pakette kodu geçen dersler silinir; görevleri dersle birlikte gider. Başka kategoride aynı kodlu ders varsa işlem durur.
- `--kategori-dahil` kategoriyi yalnız pakette olmayan dersi yoksa siler; varsa hiçbir şey silinmez.
- Gönderim geçmişi kalır; silinen göreve bağlantısı boşalır.
- Tek transaction'dır; hata olursa hiçbir değişiklik kalmaz. Tekrar çalıştırmak hata vermez.
- Silme kalıcıdır. Geri almak için ilgili aktarım komutu (A veya C) tekrar çalıştırılır; dersler yeni kimliklerle oluşur.
