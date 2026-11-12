# Hafta 04 — Baran Demir

**Ders:** BLM4581 · **Tarih:** 9–15 Kasım 2026 · **Lider:** Nisa

## Yaptıklarım

- **4.1 Gönderimler:** Model ve migration eklendi. Oturumlu kullanıcının
  değerlendirme üreten testi kod, çıktı, hata, sonuç, süre ve UTC tarihle kaydediliyor.
  Misafir, yalnız çalıştırma ve otomatik test bulunmayan denemeler kaydedilmiyor.
- Görev liste/ayrıntı ve kişisel gönderim liste/ayrıntı API’leri eklendi.
  Kullanıcı kimliği oturumdan alınıyor. Başka kullanıcının gönderimi 404 veriyor.
- Nisa’nın gönderim geçmişi sayfaları `IGonderimGecmisi` üzerinden gerçek
  servise bağlandı. Flutter devir metninde uçlar, örnek yanıtlar ve sayfalama açıklandı.
- Eski mobil derslerde test kodu boşsa çalıştırıcı çağrılmadan 409 dönüyor.
  `test_yok` sonucunda gönderim oluşturulmuyor.
- Web editörünün istek süresi sandbox ayarlarına bağlandı. Kayıt hatası,
  kuyruk doluluğu ve sandbox erişim hatası ayrı mesajlar gösteriyor. Migration
  uygulanmamış ortamda geçmiş, boş liste yerine 503 ile durumu bildiriyor.

## Doğrulama

- `baran` üzerinde 161 .NET testi geçti. Testler başarılı/başarısız gönderim,
  kayıt dışı denemeler, sahiplik, sayfalama ve eski şemadaki hata yollarını kapsadı.
- Ramazan’ın 4.4 değişiklikleriyle üç controller çakışması ayrı yerel dalda
  çözüldü. Birleşik kodda 171 .NET testi geçti.
- Birleşik kod, geçici PostgreSQL 17 ve gerçek HTTP ile sınandı. Eski şemadaki
  503, migration sonrası mevcut verilerin korunması, kişisel sayfalama,
  hesaplar arası 404 ve görev silindiğinde geçmişin kalması doğrulandı.
- İzole uygulamada Chromium ile yönetici girişi, dört ekran genişliği,
  sonuç kartları ve farklı 503 mesajları doğrulandı. Çalıştırıcı yanıtları
  tarayıcı senaryosunda taklit edildi. Bu turda yerel imaj bulunmadığı için
  gerçek Python sandbox kabulü bekliyor.

## Commit’ler / PR

- `9e3179c` — 4.1 gönderim altyapısı ve geçmiş API’leri.
- `c77b835` — Mobil test doğrulaması, web istek süresi ve hata yanıtları.
- PR kapsamı 4.1 ve ilgili düzeltmeler. 4.4’ün birleşik kabulü ayrı yerel
  `baran-4.1-4.4-entegrasyon` dalında hazırlandı.

## Karşılaştığım sorunlar

- Web’deki 20 saniyelik iptal süresi 4.4 kuyruğunun bekleme ve çalışma sınırından
  kısaydı. Editör artık iki sunucu sınırına yanıt için 15 saniye ekliyor.
- Geçmiş tablosu olmadan yeni uygulama çalıştırıldığında geçmiş sorgusu hata
  veriyordu. README’ye hedef ortam migration adımı eklendi.

## Gelecek hafta

- **5.1:** İlerleme tablosu ve mobil ilerleme API’si.
