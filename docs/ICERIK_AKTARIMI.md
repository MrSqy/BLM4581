# İçerik aktarımı — sözleşme v1

`POST /api/icerik/aktar` JSON paketi kabul eder. Yönetici rolünde gerçek Identity
çerez oturumu ve `X-CSRF-TOKEN` başlığı gerekir. Token aynı oturumla
`GET /api/icerik/antiforgery` adresinden alınır. Anonim istek 401, Öğrenci 403,
eksik/yanlış antiforgery token 400 döndürür.

## Veri biçimi

Tam örnek: [icerik-paketi.json](../ornekler/icerik-paketi.json).
Alanlar camelCase'dir. `surum` zorunlu olarak 1'dir. Kimlikler gönderilmez.

- `kategoriler`: 1–50 öğe. `kisaltma` büyük ASCII harfle başlar; büyük harf,
  rakam ve tire içerebilir, en fazla 10 karakter. `ad` zorunlu, en fazla 100;
  `aciklama` en fazla 500; `sira` sıfır veya pozitif tamsayı.
- `egitimler`: kategori başına en fazla 500. `kod` aynı kısaltma karakterleriyle
  en fazla 20; `baslik` zorunlu, en fazla 150; `sira` sıfır veya pozitif.
  `icerikMarkdown` en fazla 200000, `baslangicKodu` ve `testKodu` 100000 karakter.
- `gorevler`: ders başına en fazla 100. `no` 1–100; `baslik` zorunlu, en fazla
  200; `aciklama` en fazla 50000 karakter.

Açıklama ve kod alanları boş olabilir. Listeler boş olabilir (üstteki
`kategoriler` hariç), null olamaz ve null öğe içeremez. İstek en fazla 5 MiB'dir.
Aynı pakette kategori kısaltması ve ders kodu tekrarlanamaz; görev numarası ders
başına benzersizdir. Geçersiz veri 400 döndürür ve hiçbir kayıt değişmez.

## Ekleme ve güncelleme

Kategori `kisaltma`, ders `kod`, görev `(ders, no)` ile eşleştirilir.
Var olan kayıtların kimlikleri korunur, gönderilen alanlar güncellenir;
yeni kayıtlar eklenir. Aynı paket tekrar gönderilince kopya kayıt oluşmaz.
Paket dışında bırakılan kategori, ders ve görevler **silinmez**. Bir ders farklı
kategoride gönderilirse yeni kategoriye taşınır. Kod değiştirme bir yeniden
adlandırma değildir; yeni bir ders oluşturur.

Aktarım tek veritabanı transaction'ıdır. Veritabanı yazma çakışması 409 döndürür
ve paket tamamen geri alınır. Başarılı yanıt pakette işlenen kategori, ders ve
görev sayılarını döndürür; bunlar yeni eklenen kayıt sayıları değildir.

İçerik çalıştırılmaz. Markdown görünümde mevcut temizleyiciden geçer; test kodu
öğrenci sayfasında yayımlanmaz. Göreli Markdown bağlantıları sunucuda dosya
okumaya veya URL indirmeye dönüştürülmez; içerik üreticisi bu bağlantıları
uygun ders adreslerine veya tam kaynak URL'lerine dönüştürmelidir.

BLM4587 içindeki `tools/egitim_icerik` şu anda ders klasörlerini listeliyor;
JSON üretimi bu araçta henüz yok. Bu belge araca uygulanacak aktarım
sözleşmesidir; mevcut araçla uçtan uca aktarım tamamlandı iddiası değildir.

## Linux'tan kullanma

Uygulama çalışırken, depo kökünden:

```bash
python3 scripts/icerik-aktar.py ornekler/icerik-paketi.json --eposta yonetici@example.com
```

Şifre terminalde gizli olarak sorulur; komut satırına veya dosyaya yazılmaz.
Başka port için `--url http://localhost:PORT` ekleyin. Betik standart Python
kütüphanesini kullanır, ek kurulum istemez. Örnek paket veritabanına ORNEK
kategorisini ve bir ders/görev ekler; mevcut Python örnek verilerini değiştirmez.
