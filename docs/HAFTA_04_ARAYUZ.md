# Hafta 4 — Nisa: 4.2 ve 4.3

## 4.2 Sonuç kartları

Monaco'nun mevcut Test Et akışına görev değerlendirme kartları eklendi. Yeni bir
çalıştırıcı veya sonuç API'si oluşturulmadı. `GorevKodSonucu` ve `MobilKodSonucu`
alanları kullanılır: görev numarası/başlığı, durum, varsa hata ve çıktı/çıkış kodu
eşleşmeleri. Eski pytest yanıtındaki görev sonuçları da desteklenir.

API tek tek pytest fonksiyon adlarını veya her görev için ayrı hata metnini
döndürmüyor. Arayüz bunları uydurmaz; görev adını mevcut ders bilgisinden alır,
ayrıntı gelmediyse ortak çıktı paneline yönlendirir. Başarı, başarısızlık, atlama,
test bulunamaması, süre/çıktı sınırı ve hata durumları ayrılır. Eski sonuçlar yeni
işlemde temizlenir; tüm dinamik içerik düz metin olarak gösterilir.

## Gönderim geçmişi: 4.1 entegrasyonu bekleniyor

İnceleme sırasında `Gonderimler` modeli/DbSet'i, gönderim migration'ı veya geçmiş
API'si yoktu. Bu görev kapsamında tablo, kayıt endpoint'i veya sandbox sonucunu
kalıcılaştırma mekanizması geliştirilmedi.

- `/Gonderimler`: oturum gerektirir; 20 kayıtlık sayfalama görünümü hazır.
- `/Gonderimler/{id}`: kod, standart çıktı ve hata metni için ayrıntı görünümü hazır.
- Varsayılan servis kullanılamaz durumdadır; sayfalar HTTP 503 ile açık bir entegrasyon
  uyarısı gösterir. Boş veritabanı varmış gibi veya sonuç kaydedilmiş gibi davranmaz.
- Hazır bir adaptör bağlandığında liste/ayrıntı sorgusuna kullanıcı kimliği query/formdan
  değil, gerçek Identity `NameIdentifier` claim'inden verilir. Ayrıntıda dönen kaydın
  sahibi ve ID'si tekrar kontrol edilir; uyuşmazlık 404 döndürür. Listeye başka kullanıcı
  kaydı karışırsa liste de gösterilmez. Yanıtlar önbelleğe alınmaz.

`Services/IGonderimGecmisi.cs` yalnız web görünümünün entegrasyon sözleşmesidir;
Baran'ın 4.1 veri modeli/API DTO'su değildir. 4.1 tamamlandığında gerçek alanlar bu
görünüme eşlenmeli; olmayan bilgiler için alan/arayüz birlikte uyarlanmalıdır.
Liste servisi kullanıcıya göre filtrelemeyi, toplam sayıyı ve kararlı sıralamayı
**sayfalama öncesinde** uygulamalıdır. Ayrıntı sorgusu hem kullanıcı hem ID ile
sınırlandırılmalıdır. Gerçek adaptör Program.cs'deki bekleyen kaydın yerine geçmelidir.
Gönderimlerin ne zaman ve hangi servis tarafından kaydedildiği 4.1 ile kesinleştirilmeli;
mevcut Test Et akışı henüz geçmiş kaydı oluşturmaz.

Sahiplik ve sayfalama testleri, yalnız test projesine ait bellek servisi ve gerçek
Identity cookie'siyle yapıldı. Bunlar gerçek gönderim tablosunun kabul testi değildir.

## 4.3 Yönetici paneli

- `/Yonetim`: panel; `/Yonetim/Kategoriler`, `/Yonetim/Egitimler`, `/Yonetim/Gorevler`:
  liste, ekleme, düzenleme ve ayrı onay alanıyla silme.
- Ortak `YonetimPageModel` üzerindeki `Authorize(Roles = Roller.Yonetici)` tüm GET/POST
  işlemlerini korur. Menü görünürlüğü ek kolaylıktır; asıl kontrol sunucudadır.
- Formlar dar giriş modelleriyle bağlanır; varlıkların navigation/ID alanları doğrudan
  bind edilmez. Türkçe DataAnnotations, mevcut Unobtrusive doğrulaması, antiforgery,
  kayıt sonrası bildirim ve benzersizlik/ilişki kontrolleri kullanılır.
- Mevcut eğitim CRUD controller'ı yalnız JWT yönetici politikasına bağlı ve ortak CRUD
  servisi sunmuyor. Web cookie'sini Bearer'a dönüştürmek veya controller çağırmak yerine
  aynı EF modelleri ve kuralları Razor PageModel'lerinde kullanıldı. Controller/API
  sözleşmeleri değiştirilmedi; yeni repository veya sandbox katmanı eklenmedi.
- Eğitim içeren kategori silinmez. Eğitim silme onayı, görevlerin cascade silineceğini
  açıklar. Görev silme kalıcıdır; pasifleştirme ayrıca Aktif seçeneğiyle yapılabilir.
  Veritabanındaki sonradan oluşabilecek ilişki/benzersizlik hataları anlaşılır gösterilir.
- Görevlerde mevcut v2 `IcerikAktarimServisi.TutarlilikHatasi` doğrulaması yeniden
  kullanılır. Güvenli `gorevler/ad.py` yolu, kod/açıklama ayrımı, kurallar, görev
  numarası/dosya tekilliği ve aktif v1/v2 karışmaması kontrol edilir. Eski pytest
  derslerinin test kodu eğitim formunda yönetilir.
- Web formu yeni çıktı/girdi metinlerini LF ile kaydeder; boşlukları kırpmaz ve
  değişmemiş kontrol metinlerinin özgün satır sonlarını korur. İçerik aktarım API'sinin
  birebir metin sözleşmesi değiştirilmedi.

## Doğrulama

- Web derlemesi: 0 hata, 0 uyarı.
- Değişiklik öncesi temiz HEAD kopyası: **139 başarılı, 4 başarısız** test.
- Geliştirme sonrası tam paket: **148 başarılı, 4 başarısız** test; yeni 9 test geçti.
- Aynı dört eski sandbox testi Windows'taki `setsid`/Bash eksikliği ve Linux yol
  beklentileri nedeniyle başarısız. Çalıştırıcıya veya bu testlere müdahale edilmedi.
- Yeni testler: yönetici/öğrenci/anonim erişimi, kategori/eğitim/görev CRUD, silme
  onayı, CSRF, ilişki/cascade/tekillik, güvenli görev dosyası, eksik gönderim altyapısı,
  sahiplik kontrolü, sayfalama ve geçmişte HTML encoding.
- `tests/browser/hafta-dort.cjs`: izole SQLite + gerçek Identity oturumunda kategori
  ekleme/düzenleme/onaylı silme; beş yönetim/geçmiş sayfasında 1440/768/390/320 piksel
  taşma kontrolü; sonuç kartlarının başarı/başarısızlık/süre aşımı/boş durumları ve
  XSS metinleri doğrulandı. Sunucu sonuçları test fixture'ı/HTTP yanıt taklidi kullanır;
  gerçek sandbox veya gönderim kalıcılığı doğrulanmış sayılmaz.

```powershell
dotnet test tests/EgitimPlatformu.Web.Tests/EgitimPlatformu.Web.Tests.csproj --output "$env:TEMP/blm4581-w4-tests" -p:UseAppHost=false
```

Tarayıcı betiği izole test sunucusunu `WEEK4_TEST_URL` ile alır; Playwright yolu için
`PLAYWRIGHT_MODULE_PATH`, tarayıcı yolu için `EDITOR_BROWSER_PATH` kullanılabilir.
Test CRUD işlemleri yaptığı için gerçek kullanıcı veritabanına yönlendirilmemelidir.

## Değişen dosyalar

- `Pages/Yonetim/`: panel, üç CRUD sayfası/PageModel'i, ortak yetkili taban ve partial'lar.
- `Pages/Gonderimler/`: liste/ayrıntı sayfaları ve PageModel'leri.
- `Services/IGonderimGecmisi.cs`, `Program.cs`: bekleyen görünüm adaptörü ve DI kaydı.
- `Pages/Egitimler/_KodEditoru.cshtml`, `wwwroot/js/kod-editoru.js`: sonuç kartları.
- `Pages/Shared/_Layout.cshtml`, `wwwroot/css/site.css`: menüler ve responsive stiller.
- `tests/EgitimPlatformu.Web.Tests/HaftaDortTests.cs`, `tests/browser/hafta-dort.cjs`.
- Bu entegrasyon belgesi.

Modeller, migration'lar, bağlantı ayarları, `docker-compose.yml`, çalıştırıcı altyapısı
ve diğer ekip üyelerinin raporları değiştirilmedi. Gerçek PostgreSQL'e kayıt veya
migration uygulanmadı. Commit, push, merge veya branch değişikliği yapılmadı.
