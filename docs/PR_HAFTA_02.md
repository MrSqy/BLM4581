Üyelik sayfaları şimdi gerçek ASP.NET Core Identity hesapları ve çerez oturumuyla
çalışıyor. Normal kayıtlar Öğrenci rolü alıyor; roller ve başlangıç yöneticisi,
ortam değişkenleri veya user-secrets kullanan ayrı bir hazırlık komutuyla kuruluyor.
Mevcut ders verilerini koruyan Identity migration'ı eklendi.

Yöneticiye özel JSON aktarım ucu kategori, ders ve görevleri tek transaction içinde
ekliyor veya güncelliyor. Tekrar aktarım kimlikleri koruyor ve kayıt çoğaltmıyor;
paket dışında kalan kayıtlar silinmiyor. Antiforgery, alan/istek boyutu doğrulaması,
JSON sözleşmesi, örnek paket ve Linux aktarım betiği eklendi.

Doğrulama: 60 test geçti (mevcut 41 + yeni 19); derleme 0 hata/0 uyarı;
bekleyen model değişikliği yok. Geçici PostgreSQL 16 üzerinde migration sonrası
3 ders/24 görev korundu. Gerçek HTTP oturumu, rol yetkisi, tekrar aktarım,
güncelleme, boyut sınırı ve terminal betiği doğrulandı.

Kurulumda migration uygulanmalı ve başlangıç yöneticisi hazırlanmalı:
`docs/UYELIK.md`. Aktarım sözleşmesi: `docs/ICERIK_AKTARIMI.md`.

Sınırlar: Docker PostgreSQL 17 denenmedi; mevcut geliştirme veritabanına dokunulmadı.
Mobil içerik aracının JSON üretimi ve Ramazan'ın 2.5 çalıştırma servisi bu PR'ın
kapsamında değildir.
