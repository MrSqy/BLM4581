# Hafta 04 — Ramazan Yalçın

**Ders:** BLM4581 · **Tarih:** 9–15 Kasım 2026 · **Lider:** Nisa

## Yaptıklarım

- **4.4 Ortak sandbox kuyruğu:** Kod çalıştıran bütün yollar (`/api/calistir`, mobil görev ve ders
  uçları, ders sayfasındaki editör) sandbox'a `IHariciIslemCalistirici` üzerinden gidiyor. Kuyruğu
  (`KuyrukluIslemCalistirici`) bu noktaya koydum; toplam konteyner sayısı artık tek yerden sınırlanıyor.
  - Aynı anda en fazla **2** konteyner çalışıyor, en fazla **8** istek sırada bekliyor.
  - Sırada **15 saniyeden** fazla bekleyen istek ve kuyruğa sığmayan istek reddediliyor.
  - Değerler `appsettings.json` → `Sandbox` bölümünden değiştirilebiliyor; geçersiz değerde uygulama açılmıyor.
  - Önceden `GorevKodServisi` ve `MobilKodServisi` kendi içinde 2'şer yer tutup fazlasını hemen
    reddediyordu; bu sınırlar kaldırıldı, sırayı ortak kuyruk tutuyor.
- **Zaman aşımı:** Betiğin süre sınırı konteynerin içini kapsıyordu. Docker'ın kendisi takılırsa
  (create/start yanıt vermezse) **30 saniyelik** üst sınır işlemi durduruyor ve yeri boşaltıyor.
  İstemci vazgeçerse işlem durduruluyor, yer yine boşalıyor.
- **Hata durumları:** Kod çalıştırma uçları artık aynı 503 gövdesini döndürüyor:

  | `durum` | Anlamı | Ek |
  |---|---|---|
  | `kuyruk_dolu` | Sandbox yoğun | `Retry-After: 5` başlığı |
  | `altyapi_yok` | Docker/sandbox kullanılamıyor | — |

  Gövde `{ "mesaj": "...", "durum": "..." }`. Kodun kendisindeki zaman aşımı eskisi gibi 200 ile
  `durum: "zaman_asimi"` dönüyor. Durum kodları değişmedi; web editörü ve mobil uygulama aynen çalışıyor.

## Doğrulama

- `dotnet test`: **153 test geçti** (10'u yeni: sıra, kuyruk dolu, bekleme süresi, Docker takılması,
  istemci iptali, iç hata, ayarlar, DI bağlantısı ve iki uç yanıtı). Yeni testler 5 kez tekrar çalıştırıldı.
- Gerçek Docker ile 12 eşzamanlı sonsuz döngü: aynı anda 2 konteyner çalıştı; 6 istek sırayla
  çalışıp `zaman_asimi` aldı, 4'ü 15 sn bekleyip, 2'si hemen `503 kuyruk_dolu` + `Retry-After: 5` aldı.
  Sonunda hiç konteyner kalmadı. Normal çalıştır/test akışı aynen çalışıyor.

## Commit'ler / PR

- Bu haftanın PR'ı: `ramazan` → `main`

## Ekiple paylaşım

- **Baran:** `GorevKodServisi.cs` ve `MobilKodServisi.cs` içinde yalnız semaphore satırları kaldırıldı.
  4.1'de gönderim kaydını eklerken bu dosyalarda çakışma olursa sıra ortak kuyrukta, servislerde
  yeniden sınır eklemeye gerek yok.
- **Nisa:** 4.2 sonuç kartlarında 503 yanıtındaki `durum` alanı kullanılabilir: `kuyruk_dolu` için
  "birkaç saniye sonra yeniden dene", `altyapi_yok` için "servis kullanılamıyor".

## Gelecek hafta

- 5.4: Jupyter Server + ipykernel'i konteynerde çalıştırma; C# köprüsü.
