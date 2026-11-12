# Hafta 03 — Baran Demir

**Ders:** BLM4581 · **Tarih:** 2–8 Kasım 2026 · **Lider:** Ramazan

## Yaptıklarım

- **3.1 Eğitim API’si:** Eğitim listeleme, ayrıntı ve yöneticiye özel ekleme,
  güncelleme ve silme uçlarını ekledim. Liste kategori, arama ve sayfa
  parametrelerini destekliyor. API yanıtlarına sunucu test kodu eklenmiyor.
- **3.2 Swagger ve JWT:** Swagger’a Bearer desteği ekledim ve web API ile
  mobil istemci için ortak JWT doğrulamasını kurdum. Korunan dersler geçerli
  token istiyor. Kategorinin ilk dersine misafir erişebiliyor.
- **Python içeriği:** PY-01’i güncel kaynağa göre 5 kod ve 2 açıklama göreviyle
  eşledim. Kod görevleri kendi başlangıç dosyasına ve kontrol bilgilerine sahip.
  V2 aktarımı görev kimliklerini koruyor ve eski 8. görevi silmeden pasifleştiriyor.
- Görev bazlı çalıştırma ve test uçları mevcut Docker sandbox’ını kullanıyor.
  Beklenen çıktı ve çıkış kodu sunucuda karşılaştırılıyor.

## Doğrulama

- 3.1/3.2 tesliminde 103 .NET testi ve 5 yerel Python fixture testi geçti.
  Yerel PostgreSQL ve Swagger’da JWT erişimi, çıkış sonrası token iptali ve
  yöneticiyle CRUD doğrulandı.
- Görev bazlı v2 tesliminde 120 .NET testi ve 11 gerçek Docker senaryosu geçti.
  Ayrı PostgreSQL veritabanında migration ve tekrar aktarım denendi. Eski görev
  ve kullanıcı kayıtları korundu.

## Commit’ler / PR

- `a1aea65` — Eğitim API’si ve ortak JWT. PR #9, `0c9709c` ile birleşti.
- `a432305` — Python görev modeli ve v2 aktarım. PR #10, `0ff6cfc` ile birleşti.

## Karşılaştığım sorunlar

- Önceki tek dosyalı pytest modeli güncel PY-01 kaynağına uymuyordu.
  Görev başına başlangıç dosyası ve kontrol alanları eklendi.
- Swagger, misafir erişimli uçlarda Bearer token’ı göndermiyordu.
  Koşullu JWT tanımı eklendi.

## Gelecek hafta

- **4.1:** Gönderim modeli, migration, görev uçları ve kişisel geçmiş API’si.
