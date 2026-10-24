# BLM4581 — Etkileşimli Eğitim Platformu (Web Portalı)

ASP.NET Core Razor Pages + PostgreSQL + EF Core ile geliştirilen eğitim platformu.

Ekip: Ramazan, Nisa, Baran

## Gereksinimler

- .NET 10 SDK (`dotnet --version` ile 10.0.1xx veya üstü)
- Docker ve Docker Compose (PostgreSQL için)

## Kurulum

1. Depoyu klonlayın ve klasöre girin.

2. Veritabanı şifresini oluşturun. `.env` dosyası depoya gönderilmez.

   ```bash
   cp .env.example .env
   ```

   `.env` içindeki `POSTGRES_PASSWORD` değerini kendinize göre değiştirin.

3. PostgreSQL'i başlatın (bilgisayarın 5433 portunda çalışır):

   ```bash
   docker compose up -d
   ```

4. Bağlantı dizesini user-secrets'a kaydedin. `SIFRE` yerine `.env`'deki şifreyi yazın:

   ```bash
   dotnet user-secrets set "ConnectionStrings:Varsayilan" "Host=localhost;Port=5433;Database=egitim;Username=egitim;Password=SIFRE" --project src/EgitimPlatformu.Web
   ```

5. Proje araçlarını (dotnet-ef) yükleyin:

   ```bash
   dotnet tool restore
   ```

6. Veritabanını migration'larla oluşturun:

   ```bash
   dotnet ef database update --project src/EgitimPlatformu.Web
   ```

7. Uygulamayı çalıştırın ve terminalde yazan adresi açın (http://localhost:5080):

   ```bash
   dotnet run --project src/EgitimPlatformu.Web --launch-profile http
   ```

## Yeni migration ekleme

Model sınıflarında değişiklik yaptıktan sonra:

```bash
dotnet ef migrations add DegisiklikAdi --project src/EgitimPlatformu.Web
dotnet ef database update --project src/EgitimPlatformu.Web
```

## Python kod çalıştırma sandbox'ı

Öğrenci kodlarını doğrudan ana makinede çalıştırmayın. Hafta 01 sandbox imajını depo kökünden oluşturun:

Bu araçlar Linux konteynerleri, cgroup v2, çalışan Docker Engine, Bash ve GNU `timeout` gerektirir. Doğrulama betiği eksik komutları başlamadan bildirir.

```bash
docker build -t egitim-python-sandbox:hafta-01 sandbox/python
```

Örnek pytest çalışmasını ve bütün güvenlik sınırlarını tek komutla doğrulayın:

```bash
bash scripts/test-python-sandbox-hata-yollari.sh
bash scripts/test-python-sandbox.sh
```

İlk betik Docker altyapı ve temizlik hatalarının başarı sayılmadığını sahte hata yollarıyla sınar. İkinci betik başarılı/başarısız pytest çıkış kodlarını; ağ, kullanıcı, dosya sistemi, CPU, RAM, süreç ve süre sınırlarını gerçek konteyner üzerinde denetler.

Paylaşılan çalıştırıcıyla aynı güvenli profilde örnek testleri elle çalıştırmak için:

```bash
SANDBOX_RUN_ID=elle-deneme bash scripts/run-python-sandbox.sh \
  --timeout 5 \
  --volume "$PWD/sandbox/python/ornek:/workspace:ro" \
  --workdir /workspace \
  -- \
  python -m pytest -q -p no:cacheprovider
```

Çalıştırıcı konteyneri önce oluşturur, süre aşımında açıkça zorla durdurup siler ve yalnızca temizlik doğrulandıktan sonra `124` koduyla çıkar. Normal tamamlanmada uygulamanın gerçek çıkış kodunu korur; Docker veya temizlik hatalarını `125` ile bildirir.

| Sınır | Amaç |
|---|---|
| `--network none` | Kullanıcı kodunun ağ erişimini kapatır. |
| `--cpus 0.5` | İşlemi yarım CPU çekirdeğiyle sınırlar. |
| `--memory 128m`, `--memory-swap 128m` | RAM'i 128 MiB ile sınırlar ve ek swap kullanımını engeller. |
| `--pids-limit 64` | Süreç çoğaltma saldırılarının etkisini sınırlar. |
| `--read-only` | Konteyner kök dosya sistemini salt okunur yapar. |
| `--tmpfs /tmp:...` | `/tmp` için 16 MiB geçici alan verir; çalıştırılabilir dosya ve ayrıcalık bitlerini engeller. Docker'ın `/dev/shm` gibi standart çalışma zamanı bağlamaları ayrıca bulunur. |
| `--cap-drop ALL` | Linux yeteneklerinin tamamını kaldırır. |
| `--security-opt no-new-privileges` | Sürecin yeni ayrıcalık kazanmasını engeller. |
| `--user 10001:10001` | Kodu root olmayan sabit kullanıcıyla çalıştırır. |
| `run-python-sandbox.sh --timeout 5` | Beş saniyeyi aşan çalışmayı durdurur, konteyneri siler ve geride çalışan iş bırakmadığını denetler. |

Bu hafta yalnızca imaj ve çalışma sözleşmesi hazırlanmıştır. ASP.NET Core tarafından konteyner başlatma, öğrenci kodu/testi aktarma ve çıktıyı API'ye döndürme işi 2.5 kapsamındadır.

## Klasör yapısı

```
EgitimPlatformu.slnx          Solution
src/EgitimPlatformu.Web/      Razor Pages uygulaması
  Models/                     Varlık sınıfları (Kategori, Egitim, Gorev)
  Data/                       DbContext ve örnek veri
  Migrations/                 EF Core migration dosyaları
  Pages/                      Sayfalar
docker-compose.yml            PostgreSQL
sandbox/python/               Python + pytest sandbox imajı ve örnek test
scripts/test-python-sandbox.sh Sandbox güvenlik ve davranış doğrulaması
scripts/run-python-sandbox.sh  Süre ve konteyner yaşam döngüsünü yöneten ortak çalıştırıcı
```

## Çalışma düzeni

- `main` korumalıdır; doğrudan push yapılamaz, değişiklikler yalnızca pull request ile girer.
- Her üyenin kendi dalı vardır: `baran`, `nisa`, `ramazan`. Herkes kendi dalında, kendi GitHub hesabıyla commit atar.
- Bir işi bitirince kendi dalından `main`'e pull request açın. Birleştirme yöntemi "Create a merge commit"tir;
  böylece her commit kendi yazarı ve tarihiyle `main`'de görünür.
- Başkasının işinin üzerine çalışmadan önce `main`'i kendi dalınıza alın:

  ```bash
  git switch <adiniz>
  git pull
  git fetch origin
  git merge origin/main
  ```

- Her hafta `raporlar/<adiniz>/hafta-NN.md` dosyasına kısa bir rapor yazın (şablon: [raporlar/README.md](raporlar/README.md)).
