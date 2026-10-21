# Hafta 01 — Baran Demir

**Ders:** BLM4581 · **Tarih:** 19–25 Ekim 2026 · **Lider:** Ramazan

## Özet

Bu hafta depo altyapısını kurdum ve 1.1 işini (veri modeli, ilk migration, örnek veri) tamamladım.
Proje artık PostgreSQL'e bağlanıyor ve veritabanında gerçek derslerle dolu üç tablo var.

## Kurulum ve depo düzeni

- GitHub deposu (public), Ramazan ve Nisa collaborator olarak eklendi.
- ASP.NET Core Razor Pages (.NET 10) projesi: `src/EgitimPlatformu.Web`.
- PostgreSQL, Docker Compose ile çalışıyor (port 5433). Şifre `.env` dosyasında, bağlantı dizesi
  user-secrets'ta; ikisi de depoya girmiyor.
- EF Core + Npgsql eklendi; `dotnet-ef` aracı depoda tanımlı (`dotnet tool restore`).
- Herkese ayrı dal açıldı: `baran`, `nisa`, `ramazan`. `main` korumalı, yalnızca pull request ile
  değişiyor ve birleştirme yöntemi merge commit.
- `raporlar/` klasörü ve rapor şablonu eklendi; README'ye kurulum ve çalışma düzeni yazıldı.

## 1.1 Veri modeli, migration ve örnek veri

| Tablo | İçerik | İlişki |
|---|---|---|
| Kategoriler | Eğitim alanı (Python dili, Teknik Mülakat Algoritmalar) | 1 kategori → N ders |
| Egitimler | Ders: kod, başlık, ders metni, başlangıç kodu, test kodu | 1 ders → N görev |
| Gorevler | Alıştırma: görev no, başlık, açıklama | Derse bağlı |

- Formlarda kullanılacak doğrulama kuralları model sınıflarına eklendi.
- Ders kodu, kategori kısaltması ve "bir derste aynı görev no" için tekrar engeli (tekil indeks) var.
- `IlkOlusturma` migration'ı veritabanına uygulandı.
- Örnek veri: eğitim deposundan PY-01, PY-02, PY-03 dersleri ve 24 görev.

## Durum

- Derleme: 0 uyarı, 0 hata.
- Değişiklikler `baran` dalında; `main`'e birleştirme için PR açık:
  https://github.com/MrSqy/BLM4581/pull/1

## Gelecek hafta

- 2.1 Üyelik (ASP.NET Core Identity), Yönetici ve Öğrenci rolleri
- 2.2 Toplu içerik aktarma ucu
