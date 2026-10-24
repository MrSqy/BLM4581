# Hafta 01 — Ramazan Yalçın

**Ders:** BLM4581 · **Tarih:** 19–25 Ekim 2026 · **Lider:** Ramazan

## Özet

Bu hafta 1.3 işini tamamlayarak öğrenci Python kodlarının pytest ile sınırlandırılmış bir Docker konteynerinde çalıştırılacağı sandbox imajını hazırladım. Ortak 1.4 işi kapsamında README'ye imaj oluşturma, güvenli çalıştırma ve doğrulama adımlarını ekledim.

## 1.3 Python ve pytest sandbox'ı

- Python 3.13.17 ve pytest 9.1.1 sürümleri sabitlendi.
- Kod, UID 10001 olan root dışı `calistirici` kullanıcısıyla çalışıyor.
- Öğrenci çalışma klasörü konteynere salt okunur bağlanıyor.
- Konteyner ağı tamamen kapalı.
- Kaynak sınırları 0.5 CPU, 128 MiB RAM ve 64 süreç olarak ayarlandı.
- Kök dosya sistemi salt okunur; `/tmp` için 16 MiB geçici alan veriliyor. Docker'ın `/dev/shm` gibi standart çalışma zamanı bağlamaları ayrıca bulunuyor.
- Linux yetenekleri kaldırıldı ve yeni ayrıcalık kazanımı engellendi.
- Tüm çalıştırmalar ortak yaşam döngüsü betiğinden geçiyor; süre aşımında konteyner zorla durdurulup siliniyor.

## 1.4 README katkısı

README'ye aşağıdaki bilgiler eklendi:

- Sandbox imajını oluşturma komutu
- Tek komutla çalışan güvenlik/doğrulama betiği
- Elle çalıştırılabilecek güvenli `docker run` örneği
- Her güvenlik ve kaynak sınırının amacı
- Hafta 2'de yapılacak C# çalıştırıcı entegrasyonunun kapsam sınırı

## Doğrulama sonuçları

`bash scripts/test-python-sandbox.sh` ile aşağıdakiler doğrulandı:

- Örnek pytest senaryosu: 2 test geçti.
- Kasıtlı başarısız pytest senaryosu sıfır dışı çıkış kodu üretti.
- Ağ bağlantısı reddedildi.
- Konteyner UID 10001 ile çalıştı.
- Kök dosya sistemine yazma reddedildi.
- Salt okunur bayrağı çıkarılan regresyon senaryosunda dosya sistemi denetimi hatayı yakaladı.
- Cgroup değerleri 128 MiB RAM, 0.5 CPU ve 64 süreç sınırını gösterdi.
- Sonsuz işlem beş saniyelik zaman aşımında durduruldu.
- Zaman aşımı sonrasında geride sandbox konteyneri kalmadığı doğrulandı.

`bash scripts/test-python-sandbox-hata-yollari.sh` ile Docker altyapı ve konteyner temizleme hatalarının yanlışlıkla başarılı bir negatif test sayılmadığı doğrulandı.

`dotnet build EgitimPlatformu.slnx` sonucu: 0 uyarı, 0 hata.

## Commit'ler / PR

- `739ec8a` — Sandbox davranış sözleşmesi ve örnek pytest dosyaları
- `413f77b` — Python/pytest sandbox imajı
- `f2a5776` — Ağ, kaynak, yetki ve zaman aşımı doğrulamaları
- `c67c8e4` — Güvenli konteyner yaşam döngüsü ve hata-yolu regresyonları
- PR: Henüz açılmadı; çalışma yerel `ramazan` dalındadır.

## Karşılaştığım sorunlar

- Salt-okunur çalışma alanında pytest önbellek oluşturmaya çalıştığı için uyarı verdi. Sandbox testlerinde cache eklentisini kapatarak yazma gereksinimini kaldırdım.
- Süre sınırı Docker imajının kendi özelliği değildir; `run-python-sandbox.sh` konteyner kimliğini izler, GNU `timeout` ile süreyi uygular ve her çıkış yolunda temizlik sonucunu denetler.

## Bilinen sınırlar

- Bu hafta yalnızca Python/pytest imajı ve güvenli çalıştırma sözleşmesi hazırlandı.
- ASP.NET Core uygulaması henüz konteyner başlatmıyor ve kullanıcı kodunu aktarmıyor.
- Kuyruklama, eşzamanlı istek ve çıktı ayrıştırma sonraki haftaların kapsamındadır.

## Gelecek hafta

- 2.5 işi kapsamında C# kod çalıştırma servisi yazılacak.
- Servis, bu haftaki güvenli Docker profilini kullanarak öğrenci kodu ile test dosyasını konteynere verecek ve çıktıyı geri alacak.
