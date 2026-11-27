# Hafta 05 — Nisa

## Yaptıklarım

- **5.2:** `/Pano` altında kullanıcıya özel son beş gönderimi, boş ve hata durumlarını gösteren öğrenci panosu geliştirdim.
- **5.3:** Yönetici paneline sayfalı kullanıcı listesi, rol atama ve tanımları açıklanan gönderim istatistikleri ekledim.
- Yönetici yetkisi, CSRF, form doğrulama, son yönetici koruması ve rol değişikliğinde web/JWT oturumlarının geçersizliğini kontrol ettim. Rol yazmaları Serializable transaction kullanıyor.
- Derleme **0 hata/uyarıyla** tamamlandı. Yeni **11 test geçti**; tüm testlerde **178 başarılı, 4 mevcut ortam hatası** var.
- Eğitim gezinmesini geliştirdim: kategori kartından ilk derse geçiş, aynı kategorinin sıralı sağ menüsü, aktif ders/anchor bağlantıları ve önceki/sonraki dersler eklendi. Mobil açılır menü ve düzenlenmiş kod için ayrılma uyarısı hazırlandı.
- Gezinme sonrası derleme başarılı; ilgili **49 .NET ve 4 JavaScript testi geçti**. Güncel tüm .NET testlerinde **182 başarılı, aynı 4 ortam hatası** var.

## Commit'ler / PR

- Bu çalışma için henüz commit/PR bağlantısı yok.

## Karşılaştığım sorunlar

- Baran'ın **5.1** ilerleme modeli/servis sözleşmesi bulunmadığından ilerleme çubukları bağlanamadı; yüzde veya tamamlanma hesabı üretilmedi.
- **5.5:** Örnek veride TMA kategorisi var, bağlı ders yok. Sandbox imajı mevcut; Windows'ta `setsid` eksikliği ve Linux yol/komut bağımlılıkları sürüyor. Gerçek ortak uçtan uca deneme tamamlanamadı.
- Testler SQLite üzerinde yapıldı; PostgreSQL'de eşzamanlı rol değişikliği ayrıca doğrulanmalı.
- Tarayıcı aracı ortam hatasıyla başlatılamadığından yeni gezinmenin masaüstü/mobil görsel kontrolü yapılamadı.

## Gelecek hafta

- 5.1 sözleşmesiyle kullanıcıya ait ders ilerlemesini panoya bağlamak.
- TMA dersleri ve uyumlu sandbox ortamıyla gerçek çalıştırma, test, gönderim ve pano akışını ekipçe doğrulamak.
