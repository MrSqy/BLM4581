# Hafta 03 — Ramazan Yalçın

**Ders:** BLM4581 · **Tarih:** 2–8 Kasım 2026 · **Lider:** Ramazan

## Yaptıklarım

- **3.4 Kod çalıştırma ucu:** `POST /api/calistir` eklendi (gövde: `egitimId`, `kod`).
  2. haftada yazdığım `IKodCalistirmaServisi` artık bu uç üzerinden kullanılıyor.
  Öğrencinin kodu dersin veritabanındaki pytest testleriyle sandbox'ta çalışır;
  test kodu istemciden alınmaz. Uç Swagger'da görünür.
- **3.4 Görev bazlı ayrıştırma:** `PytestGorevAyristirici`, pytest çıktısındaki
  `test_gorev_N` satırlarını görev numarasına göre toplar. Her görev için
  `basarili / basarisiz / atlanmis / test_yok` döner. Aynı görevin birden çok testi varsa
  en kötü sonuç kalır (ör. `test_gorev_2` geçip `test_gorev_2_hata` kalırsa görev kalır).
  Durum adları mobil uçlarıyla aynıdır, istemciler tek biçim görür.
- Sandbox artık pytest'i `-v` ile çalıştırıyor. Böylece geçen testler de satır satır
  listeleniyor ve ayrıştırılabiliyor.
- Ders testlerinin kullandığı `cozum` fixture'ı için servis çalışma dizinine
  `conftest.py` yazıyor. Bu dosya olmadan PY-02/PY-03 testleri fixture hatası veriyordu.
- Erişim kuralları mevcut API ile aynı: misafir yalnız kategorinin ilk dersinde çalıştırır,
  diğer dersler Bearer ister. Görev bazlı (v2) derste 409 döner ve görev uçlarına
  yönlendirir. Sandbox yoksa 503 döner, zaman aşımında 200 + `zaman_asimi`.

## Doğrulama

- `dotnet build EgitimPlatformu.slnx`: 0 uyarı, 0 hata.
- `dotnet test EgitimPlatformu.slnx`: **143 geçti, 0 başarısız.**
- Yeni testler (`HaftaUcCalistirTests`): karışık geçti/kaldı/atlandı/hata satırları,
  parametreli test adları, özet satırının sonucu bozmaması, `test_gorev_12`'nin
  `test_gorev_1` ile karışmaması, çıkış kodu → genel durum, `conftest.py` yazımı,
  uç için 200 / 401 / 404 / 409 / 400 / 503 ve zaman aşımı.
- Gerçek Docker kabulü: geçici PostgreSQL konteynerinde migration'lar uygulandı,
  `egitim-python-sandbox:hafta-01` imajıyla `POST /api/calistir` çağrıldı.
  Yalnız ilk fonksiyonu yazılmış PY-01 kodunda görev 3 `basarili`, 4–8 `basarisiz`,
  testi olmayan 1–2 `test_yok` döndü (~1,9 sn).

## Commit'ler / PR

- Bu haftanın PR'ı: `ramazan` → `main`

## Karşılaştığım sorunlar

- `-q` çıktısında yalnız kalan testler listeleniyordu; geçenleri görev bazında saymak için
  `-v` çıktısına geçildi.
- Eski pytest dersleri `cozum` fixture'ına dayanıyor; bu fixture sandbox'ta yoktu.

## Gelecek hafta

- **4.4:** Kod çalıştırıcıda zaman aşımı, eşzamanlı istek kuyruğu ve hata durumları.
