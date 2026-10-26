# Hafta 02 — Baran Demir

**Ders:** BLM4581 · **Tarih:** 26 Ekim–1 Kasım 2026 · **Lider:** Ramazan

## Yaptıklarım

- **2.1:** ASP.NET Core Identity EF Core depolaması, kullanıcı/rol tabloları ve
  `UyelikVeRoller` migration'ı eklendi. Eski içerik migration'ı korunuyor.
- `Yonetici` ve `Ogrenci` rolleri ile açık komutla çalışan başlangıç yöneticisi
  hazırlığı eklendi. Yönetici bilgileri ortam değişkenleri/user-secrets üzerinden
  sağlanır; depoda gerçek hesap veya sabit yönetici şifresi yoktur.
- Normal kayıtlar yalnız Öğrenci olur. Kayıt ve rol ataması aynı transaction'dır.
  Parola özetleme, çerez oturumu ve başarısız giriş kilidi Identity'ye aittir.
- Nisa'nın `IHesapServisi` sözleşmesi gerçek UserManager/SignInManager ile
  uygulandı; kayıt/giriş/çıkış sayfaları korunarak bağlandı.
- **2.2:** Yöneticiye özel `POST /api/icerik/aktar` JSON ucu eklendi. Çerez oturumu
  ve antiforgery doğrulaması gerekir. JSON v1 sözleşmesi ve örnek paket belgelendi.
- Kategori kısaltması, ders kodu ve ders/görev numarasıyla ekleme/güncelleme
  yapılır. Tekrar aktarım kayıt çoğaltmaz; kimlikler korunur, pakette olmayan
  kayıtlar silinmez. Hatalı paketler yazılmadan reddedilir; veritabanı yazma
  hatasında transaction bütünüyle geri alınır.
- Linux'tan yönetici oturumu açıp JSON gönderen standart Python betiği eklendi;
  şifre komut argümanı olarak verilmez, gizli terminal girişinden okunur.

## Doğrulama — 26 Ekim

- `dotnet build EgitimPlatformu.slnx --no-restore -m:1`: 0 hata, 0 uyarı.
- `dotnet test tests/EgitimPlatformu.Web.Tests/EgitimPlatformu.Web.Tests.csproj --no-restore -m:1`:
  **60 geçti, 0 başarısız, 0 atlanan** (mevcut 41 + yeni 19).
- Yeni testlerde gerçek Identity/çerez ile kayıt–giriş–çıkış, rol ataması,
  parola özeti, tekrar kayıt, zayıf şifre, hesap kilidi ve tekrarlanabilir
  başlangıç yöneticisi doğrulandı. Öğrenci hesabının hazırlık komutuyla
  sessizce yöneticiye yükseltilmesi reddedildi.
- Aktarım testlerinde 401/403, antiforgery, ekleme/güncelleme, kimliklerin
  korunması, silmeme, null/bozuk/tekrarlı veriler, alan sınırları ve
  veritabanı hatasında geri alma doğrulandı. SQLite tetikleyicisiyle ikinci
  ders yazması kasıtlı başarısız kılındı; önceki değişiklikler de geri alındı.
- `dotnet ef migrations has-pending-model-changes --project src/EgitimPlatformu.Web --no-build`:
  bekleyen model değişikliği yok.
- Mevcut geliştirme veritabanı yerine `/tmp` altında **geçici PostgreSQL 16**
  açıldı. İlk migration'daki **3 ders / 24 görev**, Identity migration'ından
  sonra korundu; kullanıcı tablosu başlangıçta boştu.
- Geçici PostgreSQL ve gerçek Kestrel HTTP sunucusunda yönetici hazırlığı iki
  kez, gerçek öğrenci kaydı/oturumu, Öğrenci için 403, anonim için 401 ve
  tokensiz yönetici için 400 doğrulandı.
- Örnek paket iki kez aktarıldı: **4 ders / 25 görev**, kopya kayıt yok.
  Aynı dersin başlığı yeniden aktarımda güncellendi. Bozuk/null paketler 400,
  5 MiB üzeri Content-Length 413 verdi. Linux aktarım betiği gerçek oturumla
  başarıyla çalıştı; antiforgery korumalı çıkış oturumu kapattı.
- Test çalıştırıcısının ve geçici PostgreSQL'in yerel soket ihtiyacı nedeniyle
  kontroller sandbox dışında yürütüldü; paket indirme işlemleri kullanıcı
  tarafından yapıldı. Geçici sunucular kontrol sonrasında kapatıldı.

## Commit'ler / PR

- `f125395` — 2.1 Identity üyeliği, roller ve başlangıç yöneticisi.
- 2.2 içerik aktarımı, belgeler ve bu rapor ayrı checkpoint commit'inde.
- Push ve PR/birleştirme kullanıcı tarafından yapılacak; bu raporda yapılmış sayılmıyor.

## Sınırlar ve devam

- Otomatik testler SQLite, canlı geçici kontrol PostgreSQL 16 kullanır.
  README'deki Docker PostgreSQL 17 ortamı bu çalışmada çalıştırılmadı.
- Gerçek geliştirme veritabanına migration uygulanmadı. Kullanıcı kendi
  ortamında migration ve başlangıç yöneticisi hazırlığını yapmalı.
- BLM4587 içerik aracı henüz JSON üretmiyor. Web ucu örnek paketle hazır;
  araç sözleşme v1'i üretmeye başladığında uçtan uca kaynak aktarımı denenmeli.
  Kardeş mobil proje/araç değiştirilmedi.
- Göreli Markdown kaynak bağlantılarının URL'ye dönüştürülmesi içerik
  üreticisine aittir; aktarım servisi dosya veya uzak URL okumaz.
- Ramazan'ın **2.5** C# kod çalıştırma servisi bu çalışmada yapılmadı.
- Gelecek hafta: **3.1** Eğitim CRUD API ve **3.2** Swagger/JWT giriş ucu.
