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

## Klasör yapısı

```
EgitimPlatformu.slnx          Solution
src/EgitimPlatformu.Web/      Razor Pages uygulaması
  Data/                       DbContext
  Pages/                      Sayfalar
docker-compose.yml            PostgreSQL
```
