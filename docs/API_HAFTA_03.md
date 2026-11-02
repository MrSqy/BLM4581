# Hafta 3 — ortak backend sözleşmesi

## Yerel başlatma

Mevcut PostgreSQL bağlantısı ve Identity hazırlığı README/UYELIK belgesindeki gibidir. JWT için tek ayar `Jwt:Anahtar`: Base64 biçiminde en az 32 rastgele byte. `MobilJwt:Anahtar` kullanılmaz. Anahtar yoksa web açılır fakat API giriş/kayıt uçları 503 döndürür; kayıt oluşturmaz. Anahtarı depoya eklemeyin.

Linux/Zsh terminalinde, depo kökünde:

```bash
export Jwt__Anahtar="$(openssl rand -base64 32)"
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --project src/EgitimPlatformu.Web -- --urls http://localhost:5080
unset Jwt__Anahtar
```

Bu komut her çalıştırıldığında yeni anahtar üretir ve önceki tokenlar geçersiz olur. Kalıcı ortamda anahtar gizli yapılandırmada sabit tutulmalıdır. HTTP adresi yalnız yerel geliştirme içindir; yayın ortamı HTTPS kullanmalıdır. Swagger yalnız Development ortamında `/swagger/index.html` adresindedir.

## Kimlik ve erişim

Tek Bearer şeması, HS256 imzası, issuer `EgitimPlatformu`, audience `EgitimPlatformu.Mobile`, 15 dakika süre; doğrulamada 30 saniye saat toleransı. Refresh token yoktur. Token imzası, süre, issuer, audience, hesabın varlığı, kilit durumu, security stamp ve güncel roller denetlenir. Cookie tek başına API yönetici yetkisi vermez. İki faktör açık hesaplara bu parola uçlarından token verilmez.

Misafir her kategorinin `sira`, ardından `id` ile belirlenen ilk dersini okuyabilir, çalıştırabilir ve test edebilir. Diğer derslerin listeleri/özetleri açıktır; detay ve kod uçları 401 verir. Geçerli token ile bütün derslere erişilir. Kategori ilişkisi olmayan eski dersler kendi gruplarında sıralanır. Bu kural `/api/egitimler/{id}` ve mobil API için ortaktır; mevcut Razor sayfalarının erişim politikasını değiştirmez.

Çıkış security stamp'i yeniler: hesaba ait tüm önceki JWT'ler iptal edilir. Web cookie'leri de Identity'nin stamp doğrulaması sırasında etkilenebilir. 401 alan istemci yerel oturumu temizlemeli, yeniden giriş sunmalıdır. 403, 429 veya 503 oturum silme nedeni değildir.

## Mobil API

Taban yol `/api/mobil/`; JSON alanları camelCase.

| Metot / yol | İstek | Yanıt |
|---|---|---|
| POST hesap/kayit | adSoyad, eposta, sifre | 200: token, bitis, kullanici |
| POST hesap/giris | eposta, sifre | 200: token, bitis, kullanici |
| GET hesap/ben | Bearer | id, eposta, adSoyad |
| POST hesap/cikis | Bearer | 204, gövde yok |
| GET kategoriler | — | id, ad, kisaltma, aciklama, sira dizisi |
| GET egitimler?kategoriId=1 | isteğe bağlı Bearer | Müfredat dizisi |
| GET egitimler/{id} | korunan derste Bearer | Ders detayı |
| POST egitimler/{id}/calistir | kod; korunan derste Bearer | Çalıştırma sonucu |
| POST egitimler/{id}/test | kod; korunan derste Bearer | Görev sonuçları |

Oturum: `bitis` UTC ISO tarih; `kullanici`: `{id,eposta,adSoyad}`. Kayıt doğrudan öğrenci rolüyle oturum açar. Şifre en az 8 karakter, büyük/küçük harf, rakam ve özel karakter içermelidir. Beş yanlış parola denemesinde hesap beş dakika kilitlenir.

Müfredat: `id,kategoriId,kod,baslik,sira,gorevSayisi,ozet,misafireAcik,erisilebilir`. Tam içerik ve kodlar listede bulunmaz. Ders: `id,kategoriId,kod,baslik,sira,icerikMarkdown,baslangicKodu,misafireAcik,erisilebilir,gorevler`; görev: `id,egitimId,no,baslik,aciklama`.

Kod en fazla 32000 karakterdir. Test kodu sunucudan alınır; istemci test kodu göndermez. Sonuç: `basarili,durum,cikti,hata,gorevler`. Genel durumlar `tamamlandi,basarisiz,hata,zaman_asimi,test_yok`; görev durumları `basarili,basarisiz,atlanmis,test_yok`. Görev eşlemesi pytest adındaki `test_gorev_N` üzerinden yapılır. Sonuçlar eğitim geri bildirimidir; aynı sandbox içindeki öğrenci koduna karşı güvenilir sınav/not kanıtı değildir.

Hata gövdesi mobil iş kurallarında `{mesaj}`, otomatik doğrulamada ProblemDetails (`title,errors`) olabilir. İstemci ikisini de desteklemeli; boş 401/403/429 için durum koduna göre mesaj vermelidir. Mobil uçlar ve genel API girişi aynı IP için dakikada 30 istek limiti paylaşır. Sandbox eşzamanlı iki iş kabul eder; altyapı yoksa/meşgulse 503, zaman aşımında 200 ve `basarili:false,durum:zaman_asimi` döner.

## Yönetim API'si

`GET /api/egitimler`: `kategoriId,q,sayfa,sayfaBoyutu` filtreleri; yanıt `toplam,sayfa,sayfaBoyutu,egitimler`. Sayfa 1'den başlar, boyut en fazla 100. Liste yalnız özet taşır. `GET /api/egitimler/{id}` görevlerle detay döndürür; test kodu DTO'da yoktur.

`POST /api/egitimler`, `PUT /api/egitimler/{id}`, `DELETE /api/egitimler/{id}` yalnız Yönetici rolündeki Bearer token ile kullanılabilir. Öğrenci 403, tokensiz istek 401 alır. Yazma DTO'sunun tüm alanları zorunlu: `kategoriId,kod,baslik,sira,icerikMarkdown,baslangicKodu,testKodu`. PUT tam değiştirmedir; eksik alan eski içeriği silmez, 400 döndürür. POST 201 ve Location, PUT/DELETE 204; bulunamayan 404, çakışan kod 409. Silme ilişkili görevleri de siler.

`POST /api/hesap/giris` aynı token altyapısını kullanan genel giriş uçudur; yanıt `accessToken,tokenType,expiresAt`. Mobil istemci mevcut `hesap/giris` yolunu kullanmaya devam edebilir. Swagger Authorize alanına ham token girilir.

## Doğrulama ve sınırlar

.NET entegrasyon testleri gerçek uygulama/Identity/JWT hattını bellek içi SQLite ile çalıştırır; kullanıcı PostgreSQL veritabanına dokunmaz. Mobil HTTP kod testlerinde runner taklidi kullanılır; runner servis testleri görev eşleme, hata yolları ve dosya temizliğini doğrular. `python3 scripts/test-mobile-runner.py` beş güvenilir fixture'ı yerel Python üzerinde sınar; Docker izolasyonunu kanıtlamaz.

Bu teslim sırasında `egitim-python-sandbox:hafta-01` imajı yoktu. Gerçek Docker, telefon ve Flutter uçtan uca kabulü henüz yapılmadı. İmaj hazırlığı: `docker build -t egitim-python-sandbox:hafta-01 sandbox/python`; ardından `bash scripts/test-python-sandbox.sh` ve mobil çalıştır/test akışları doğrulanmalıdır.

OpenAPI 2.3.0 bağımlılığı, yerel önbellekteki 2.12.0 sürümüne yükseltildi. Güvenlik duyurusu: https://github.com/advisories/GHSA-v5pm-xwqc-g5wc . Paket çözümlemesi yalnız yerel önbellekle yapıldı; yeni ağ tabanlı güvenlik taraması yapılmadı.

## Yerel kabul sonucu

103 .NET testi ve 5 güvenilir Python fixture testi geçti. Yerel Kestrel/PostgreSQL okuma ve anonim erişim kontrolleri yapıldı. Kullanıcı Swagger üzerinden giriş/token, korunan ders erişimi, çıkış sonrası token iptali ve ilk dersin anonim erişimini doğruladı. Ayrı yöneticiyle deneme dersi POST 201, PUT 204, GET 200, DELETE 204, son GET 404 olarak doğrulandı. Boş rol tablosuyla ilk web kaydı ve koşullu erişimli uçların Swagger Bearer tanımı regresyon kapsamına alındı. Docker/telefon sınırları yukarıdaki gibi devam ediyor.
