# Hafta 05 — Ramazan Yalçın

**Ders:** BLM4581 · **Tarih:** 23–29 Kasım 2026 · **Lider:** Nisa

## Yaptıklarım

- **5.4 Jupyter altyapısı:** Jupyter Server ve ipykernel için Python sandbox'ını temel alan bir imaj hazırladım. Sunucu konteynerin kendi loopback adresinde çalışıyor.
- C# hizmeti ortak sandbox kuyruğuna bir hücre grubu veriyor. Konteyner çekirdeği REST üzerinden açıyor, hücreleri WebSocket üzerinden aynı çekirdeğe gönderiyor ve çıktılarını hücre bazında döndürüyor.
- Çalıştırma tamamlandığında çekirdek, konteyner ve geçici istek dosyası temizleniyor. Süre aşımı ve altyapı hatası ayrı durumlarla bildiriliyor.

## Doğrulama

- Güncel başlangıç sürümünde 171 test geçti.
- Docker testi dahil son çalıştırmada 176 test geçti. Gerçek çekirdekte bir hücrede tanımlanan değerden diğer hücrede `42` çıktısı alındı, `ValueError` ayrıştırıldı ve zaman aşımı doğrulandı. Test sonrasında çalışan veya durmuş sandbox konteyneri kalmadı.
- Mevcut sandbox temizlik ve altyapı hata testleri geçti.

## Commit'ler / PR

- Bu haftanın PR'ı `ramazan` → `main`.

## Gelecek hafta

- **6.3 ve 6.4:** Notebook dosyalarını C# modeline okuma ve yazma, notebook sayfasında hücre düzenleme.
