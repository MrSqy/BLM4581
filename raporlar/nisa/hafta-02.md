# Hafta 02 — Nisa

## Yaptıklarım

- **2.3:** Kayıt, giriş ve çıkış sayfalarını; DataAnnotations ve istemci doğrulamalarını hazırladım. Güvenli POST/antiforgery akışları ve navbar bağlantıları eklendi.
- **2.4:** EF Core ile eğitim kataloğu, kategori filtresi, arama, ders/görev detayları ve gerçek ders kartları geliştirildi.
- Markdown, Markdig ile HTML'e dönüştürüldü; HtmlSanitizer ile güvenlik temizliği uygulandı.
- Geliştirme sırasında **41 test geçti**, derleme **0 hata/0 uyarı** ile tamamlandı; mobil ve masaüstü görünümler kontrol edildi.

## Commit'ler / PR

- Commit: `c36e6db` — Kullanıcı işlemleri ve eğitim sayfaları.
- PR: [#4](https://github.com/MrSqy/BLM4581/pull/4) — Birleştirildiği doğrulandı.

## Karşılaştığım sorunlar

- **Identity entegrasyonu bekliyor:** Gerçek kayıt/giriş/çıkış henüz çalışmıyor. Formlarda uyarı var; hesap testleri test servisiyle yapıldı.
- Docker'ın kapalı olması ve eksik tablolar nedeniyle eğitimlere erişilemedi. Kurulum sonrasında PostgreSQL'de **3 ders ve 24 görev** doğrulandı.
- Migration'ı engelleyen CRLF/LF farkı, LF dönüşümü ve `.gitattributes` kuralıyla giderildi. Çalışan uygulamanın derleme dosyalarını kilitlemesiyle de karşılaşıldı.
- Göreli Markdown kaynak bağlantıları içerik aktarımıyla çözülmeli. **2.1, 2.2 ve 2.5** altyapıları bu çalışmada geliştirilmedi.

## Gelecek hafta

- Identity altyapısına gerçek hesap servisini bağlayıp kayıt/giriş/çıkışı gerçek oturumla test etmek.
- İçerik aktarımı sonrası kategori, kaynak bağlantıları ve ders akışlarını PostgreSQL üzerinde yeniden doğrulamak. Otomatik veri testleri SQLite kullanıyor.
