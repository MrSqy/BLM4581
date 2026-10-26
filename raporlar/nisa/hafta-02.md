# Hafta 02 — Nisa | 2.3 ve 2.4

## İnceleme ve kapsam

- Başlangıçta yalnızca statik ana sayfa ve üç içerik tablosu vardı. Identity servisleri,
  kullanıcı/rol tabloları, hesap sayfaları, veritabanına bağlı katalog ve detay sayfaları yoktu.
- 2.1 Identity altyapısı, 2.2 toplu içerik aktarma ve 2.5 Docker yapılandırması geliştirilmedi.
- Modeller, DbContext, migration'lar, bağlantı ayarları ve gizli bilgiler değiştirilmedi.
- Çalışma ağacındaki mevcut tasarım korundu. Yerel Docker Compose değişikliğine dokunulmadı.
- Commit, push veya merge yapılmadı.

## 2.3 — Kayıt, giriş, çıkış

- `/Hesap/Kaydol`, `/Hesap/Giris`, `/Hesap/Cikis` Razor sayfaları ve PageModel'leri eklendi.
- Ayrı form modellerinde Required, EmailAddress, StringLength, Compare ve DataType kuralları var.
- Kayıt şifresi 8–128 karakter; şifre tekrarı eşit olmalı. Girişte mevcut hesapların şifre
  politikasını yeniden uygulamamak için yalnızca zorunluluk doğrulanır.
- Mevcut jQuery Validation / Unobtrusive partial'ı kullanıldı. Alan ve form hataları Türkçe.
- Formlar antiforgery korumalı POST kullanır. Çıkış GET isteği yalnızca onay gösterir.
- Dönüş adresleri `Url.IsLocalUrl` ve `LocalRedirect` ile sınırlandırıldı.
- Başarılı sonuçlar TempData ile gösterilir; başarısızlıklar ModelState'e eklenir.
- Navbar gerçek sayfalara gider. Kimliği doğrulanmış kullanıcıda ad ve POST çıkış formu gösterir.

### Mevcut Identity bağımlılığı — henüz gerçek kayıt/giriş yok

`Services/IHesapServisi.cs` entegrasyon sözleşmesidir. `BekleyenHesapServisi` açıkça
`Kullanilabilir = false` döndürür; hesap/parola saklamaz, cookie oluşturmaz, başarı üretmez.
Sayfalarda bekleme uyarısı ve devre dışı gönderim düğmesi gösterilir. Elle geçerli POST
gönderilirse 503 döner; hatalı formda sunucu doğrulama mesajları gösterilir.

Baran'ın 2.1 kapsamında tamamlaması gerekenler:

1. Identity kullanıcı tipi, store/tabloları, migration, roller ve başlangıç yönetici hesabı.
2. Identity authentication/cookie servisleri ve middleware. `UseAuthentication`,
   `UseAuthorization` öncesinde çalışmalı. Cookie giriş yolu `/Hesap/Giris` olmalı.
3. Gerçek kullanıcı tipiyle `UserManager` / `SignInManager` kullanan bir `IHesapServisi`
   adaptörü. Parola saklama/doğrulama tamamen Identity'ye bırakılmalı.
4. Program.cs'deki bekleyen kaydı adaptörle değiştirme. Örneğin, adaptör eklendikten sonra:

   ```csharp
   builder.Services.Replace(
       ServiceDescriptor.Scoped<IHesapServisi, IdentityHesapServisi>());
   ```

   `IdentityHesapServisi` burada entegrasyonda oluşturulacak sınıfın örnek adıdır;
   bu çalışmada sahte bir Identity implementasyonu eklenmedi.

5. `KaydolAsync` kullanıcı oluşturur, otomatik giriş yapmaz. Başarı mesajı kullanıcıyı
   girişe yönlendirir. E-posta onayı zorunluysa adaptör bu gerekliliği açıklamalı ve onay
   gönderme akışı ayrıca tamamlanmalı.
6. `GirisYapAsync` gerçek cookie oturumu açmalı, `beniHatirla` seçeneğini uygulamalı ve
   `lockoutOnFailure: true` kullanmalı. Hatalı kullanıcı/parola için hesap varlığını
   açıklamayan genel Türkçe hata döndürmeli. Kilitlenme, onaysız hesap ve gerekiyorsa
   2FA durumu başarı sayılmamalı.
7. `CikisYapAsync` Identity `SignOutAsync` tamamlandıktan sonra başarı döndürmeli.
8. Nihai parola politikası formun 8–128 karakter kuralıyla uyumlaştırılmalı.

Gerçek Identity cookie'siyle uçtan uca kayıt/giriş/çıkış testi bu entegrasyondan sonra
yapılmalıdır. Testlerdeki hesap servisi ve kullanıcı filtresi yalnızca test projesindedir;
üretim uygulamasına sahte kimlik doğrulama eklenmemiştir.

## 2.4 — Katalog ve detay

- `/Egitimler`: EF Core `AsNoTracking` sorguları, veritabanından kategoriler ve dersler,
  kategori filtresi ve ders adı/kodu/kategori araması. GET parametreleri paylaşılabilir;
  JavaScript kapalıyken de çalışır.
- Kartlarda ders başlığı, kodu, kategorisi, kategori açıklaması ve gerçek görev sayısı var.
  Modelde olmayan süre, seviye, puan veya ders özeti uydurulmadı.
- `/Egitimler/Detay/{id}`: ders metni, başlangıç kodu ve numarasına göre sıralı görevler.
  Görevler erişilebilir yerel `details/summary` öğeleriyle açılır. `TestKodu` yayımlanmaz.
- Sayfa bir kod çalıştırma editörü değildir; başlangıç kodu bölümünde bu açıkça belirtilir.
- Ana sayfa aynı kart partial'ıyla ilk dört gerçek dersi gösterir; kartlar ve keşfet bağlantısı
  gerçek katalog/detay sayfalarına gider. Önceki statik, veritabanında olmayan dersler kaldırıldı.
- Geçersiz filtre 400, bulunmayan kategori/ders 404, bağlantı hatası kontrollü 503 döndürür.
  Boş kategori ve içerik için açıklayıcı ekranlar var. Ana sayfa veritabanı yokken de açılır
  ve katalog erişim sorununu açıkça gösterir.

### Markdown güvenliği ve paketler

- **Markdig 1.3.2:** Markdown ayrıştırma ve HTML dönüşümü; ham HTML kapalı, tablo desteği açık.
- **HtmlSanitizer 9.2.1039:** Dönüşümden sonra dar etiket/öznitelik/protokol izin listesiyle temizlik.
  Script, olay işleyicileri, stil, iframe, SVG, resimler ve tehlikeli bağlantı şemaları çalışmaz.
- `Html.Raw` yalnızca `MarkdownServisi.Html` tarafından temizlenmiş ders/görev HTML'ini kullanır.
  Başlangıç kodu Razor tarafından normal metin olarak encode edilir.
- Örnek içerikteki `cozumler/CEVAPLAR.md` gibi göreli kaynak bağlantılarının dosyaları
  bu web deposunda yoktur. Modelde kaynak adresi de yoktur. Bu bağlantıların gerçek kaynak
  URL'lerine dönüştürülmesi, 2.2 içerik aktarımı sırasında kaynak bağlamıyla çözülmelidir.

Kaynaklar: [Markdig](https://www.nuget.org/packages/Markdig/1.3.2),
[HtmlSanitizer](https://www.nuget.org/packages/HtmlSanitizer/9.2.1039).

## Doğrulama

- Web projesi ayrı çıktı klasörüne derlendi: **0 hata, 0 uyarı**.
- **41 otomatik test başarılı:** gerçek Razor Page istekleri, SQLite ilişkisel sorguları,
  kategoriler/arama/boş sonuçlar/404, görevler, form doğrulaması, şifrenin geri yazılmaması,
  yönlendirmeler, navbar encoding, antiforgery, çıkışın GET ile yapılmaması, XSS örnekleri
  ve veritabanı erişim hatası.
- Test veritabanı bellekte SQLite'tır; mevcut modelin `HasData` örneklerini `EnsureCreated`
  ile yalnızca test ortamında kurar. Gerçek PostgreSQL'e veri yazılmaz veya migration uygulanmaz.
- Yerel PostgreSQL kontrolünde `127.0.0.1:5434` bağlantıyı reddetti. Docker/bağlantı ayarı
  değiştirilmedi. Gerçek PostgreSQL üzerinde canlı okuma kontrolü servis açıldıktan sonra
  tekrar yapılmalı; SQLite testi PostgreSQL bağlantısının çalıştığını kanıtlamaz.
- Tarayıcıda altı sayfa 320, 390, 768 ve 1440 pikselde kontrol edildi; yatay taşma ve
  JavaScript hatası yok. Katalog filtreleme, ders/görev akışı ve gerçek jQuery istemci
  doğrulaması denendi. Gerçek PostgreSQL kapalıyken kontrollü 503 yanıtı doğrulandı.

Testleri çalıştırma:

```powershell
dotnet test EgitimPlatformu.slnx
```

Çalışan uygulama normal çıktı dosyalarını kilitliyorsa oturumu kapatın veya ayrı çıktıyı kullanın:

```powershell
dotnet test tests/EgitimPlatformu.Web.Tests/EgitimPlatformu.Web.Tests.csproj --output "$env:TEMP/egitim-hafta2-tests" -p:UseAppHost=false
```

## Değişen dosya grupları

- `Pages/Hesap/`: formlar, PageModel'ler ve altyapı uyarısı.
- `Pages/Egitimler/`: katalog ve ders detayı.
- `Services/`: hesap sözleşmesi/bekleme servisi, güvenli Markdown dönüşümü.
- `ViewModels/EgitimKartModel.cs`, `Pages/Shared/_EgitimKarti.cshtml`: ortak gerçek veri kartları.
- `Pages/Index.cshtml(.cs)`, `Pages/Shared/_Layout.cshtml`, `wwwroot/css/site.css`,
  `wwwroot/js/site.js`: ana sayfa, bağlantılar ve ortak görünüm.
- `Program.cs`: servis kayıtları ve entegrasyon testinin erişim noktası.
- Web `.csproj`: iki Markdown paketi; solution ve `tests/`: test projesi.
