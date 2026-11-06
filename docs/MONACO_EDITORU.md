# Görev 3.3 — Ders sayfasında Monaco

## Kullanım

- `/Egitimler/Detay/{id}` içindeki **Kod editörü** bağlantısı çalışma alanına gider.
- Güncel görevli derslerde görev seçilir; o görevin başlangıç kodu yüklenir. Açıklama
  görevlerinde otomatik çalıştırma/test kapalıdır. Eski pytest derslerinde dersin tek
  başlangıç şablonu kullanılır; test yoksa yalnız Çalıştır etkinleşir.
- Python renklendirme, satır numarası, dört boşluk girinti, otomatik girinti, arama,
  yorum, çoklu imleç ve geri alma özellikleri vardır. Ctrl+M, Tab ile odak geçişini açar.
- Görev taslakları yalnız açık sayfanın belleğinde korunur. Kalıcı kayıt eklenmedi;
  yenileme veya sayfadan ayrılma öncesinde öğrenci kodunu kopyalamalıdır.
- İşlem sürerken iki düğme, görev seçimi ve düzenleme kilitlenir. Önceki sonuçlar
  temizlenir. Boş çıktı, stderr, kontrol eşleşmeleri ve eski pytest görev sonuçları ayrılır.
- Sonuçlar `textContent`/metin düğümleriyle gösterilir; öğrenci çıktısı HTML olarak çalışmaz.
- Monaco yüklenemezse uyarı ve düz metin editörü gösterilir. JavaScript kapalıysa
  çalıştırma düğmeleri devre dışı kalır; ders okuma işlevi devam eder.

## Mevcut backend ile bağlantı

Mevcut sözleşmeler incelendi: `MobilKodIstegi`, `GorevKodSonucu`, `MobilKodSonucu`,
`IGorevKodServisi`, `IMobilKodServisi`, `API_HAFTA_03.md` ve `GOREV_AKTARIMI_V2.md`.
Mobil controller'lar Bearer kullanır; web ise Identity cookie oturumuyla çalışır.

Bu nedenle mevcut `DetayModel` içinde iki ince Razor AJAX handler'ı eklendi:

```text
POST /Egitimler/Detay/{id}?handler=Calistir&no={gorevNo}
POST /Egitimler/Detay/{id}?handler=Test&no={gorevNo}
Content-Type: application/json
X-CSRF-TOKEN: sayfanın antiforgery token'ı
{"kod":"print('Merhaba, dünya!')"}
```

Adresler istemcide tahmin edilmez; `Url.Page` ile üretilir. Eski pytest derslerinde
`no` gönderilmez. Handler mevcut `MobilKodIstegi` DTO'sunu kabul eder; güncel görevde
`IGorevKodServisi`, eski derste `IMobilKodServisi` çağrılır ve servis sonucu aynen JSON'a
çevrilir. Python kodu tarayıcıda veya PageModel içinde çalıştırılmaz.

Her kategorinin ilk dersinde misafir çalıştırma yapabilir; sonraki derslerde gerçek web
cookie oturumu gerekir. İlk ders seçimi mevcut `DersErisimi.IlkDers` metodunu kullanır.
Razor ders okuma politikası ve mobil/JWT controller'ları değiştirilmedi.

İstek en fazla 256 KiB, kod en fazla 32000 karakterdir. Razor antiforgery doğrulaması
ve mevcut `mobil` rate limit politikası uygulanır (sayfanın GET isteği de bu politikaya
dahildir). Aktif görev, görev türü, başlangıç dosyası ve kontrol verileri sunucudan
okunur; istemciden test/ölçüt/dosya yolu alınmaz. Bilinmeyen veya pasif görev 404,
açıklama görevi 409, oturum eksikliği 401, limit 429, altyapı sorunu 503 döndürür.
İstemci 20 saniyede yanıt alamazsa beklemeyi keser; otomatik tekrar göndermez.

## Monaco dosyaları

**Monaco 0.57.0** yerel ESM paketi olarak sunulur; CDN ve AMD yükleyici kullanılmaz.
Yalnız Python dili ve temel düzenleme katkıları eklenmiştir. Worker aynı kaynaktan
yüklenir. MIT lisansı ve üçüncü taraf bildirimleri `wwwroot/lib/monaco/` içindedir.

Hazır JS/CSS/worker dosyaları depoda bulunur; normal .NET çalıştırma/yayınlama için
Node gerekmez. Dosyaları yeniden üretmek için:

```powershell
cd tools/editor-assets
npm ci --ignore-scripts
npm run build
```

Monaco ve yalnız paketleme için kullanılan esbuild 0.28.2 sürümleri `package.json` ve
`package-lock.json` ile sabittir. `node_modules` depoya alınmaz. Üretilen dosyalar,
kilit dosyası ve kaynak build betiği birlikte tutulmalıdır.

## Doğrulama ve sınırlar

- Son derleme: **0 hata / 0 uyarı**. Yeni editör testlerinin **13'ü geçti**. Tam pakette
  **129 başarılı / 4 başarısız / toplam 133** test var. Başarısız olan mevcut
  `KodCalistirmaServisiTests` testleri Windows'taki `setsid` eksikliği, Bash çıkış kodu
  ve Linux/Windows yol biçimi farklarına takıldı; bu servis/testler değiştirilmedi.
- Gerçek Monaco tarayıcı kabul betiği geçti. Görsel çıktılar masaüstü ve mobilde incelendi.
- .NET testleri mevcut SQLite/gerçek Identity fixture'ını ve taklit runner'ları kullanır.
  Kod/görev yönlendirmesi, v1/v2 ayrımı, web cookie, antiforgery, kod boyutu, görev türü,
  güvenli JSON, boş/başarısız sonuçlar, platform/servis hatası ve rate limit sınanır.
- `tests/browser/monaco-editor.cjs`, izole SQLite + runner fixture'ında gerçek Monaco'yu
  yükler; Python modu, başlangıç kodu, girinti, Ctrl+M, taslaklar, POST gövdesi/CSRF,
  yinelenen istek engeli, düz metin XSS kontrolü, HTTP hataları ve dört ekran genişliğini
  doğrular. Yanıt fixture'ları gerçek sandbox başarısını kanıtlamaz.
- Tarayıcı betiği `EDITOR_TEST_URL`, isteğe bağlı `PLAYWRIGHT_MODULE_PATH`,
  `EDITOR_BROWSER_PATH` ve `EDITOR_SCREENSHOT_DIR` ortam değişkenlerini kullanır.
  Test URL'si gerçek öğrenci verileri yerine izole fixture'a verilmelidir.
- Mevcut runner `setsid`, Bash ve Linux sandbox ortamı ister. Windows'ta yalnız Docker'ın
  açık olması yeterli değildir. Bu eksiklikte arayüz başarı göstermez; altyapı uyarısı verir.
- Gerçek uçtan uca Python çalıştırma/test kabulü, Ramazan'ın desteklenen Linux/WSL sunucu
  ortamında sandbox imajı ve servis hazırken yapılmalıdır. Bu çalışmada runner, Docker,
  veri modeli, migration, bağlantı ayarı ve Baran'ın API/JWT görevleri değiştirilmedi.

Kaynaklar: [Monaco](https://github.com/microsoft/monaco-editor),
[ESM entegrasyonu](https://github.com/microsoft/monaco-editor/blob/main/docs/integrate-esm.md).
