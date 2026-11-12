## Değişiklik

Oturumlu öğrencinin test sonucu artık kişisel gönderim geçmişine yazılıyor.
Görev ve gönderim API’leri eklendi. Web geçmişi gerçek servise bağlandı.
V2 test yanıtı kayıt oluştuğunda `gonderimId` döndürüyor.

Eski mobil derste otomatik test yoksa istek 409 dönüyor. `test_yok`
sonuçları geçmişe eklenmiyor. Web editörü sunucunun kuyruk ve çalışma
sınırlarına göre bekliyor. Kayıt, kuyruk ve çalıştırıcı hataları ayrı
mesajlar gösteriyor. Geçmiş tablosu hazır olmadığında API ve web 503 veriyor.

Baran’ın 3. ve 4. hafta raporları ile Flutter devir metni eklendi/güncellendi.
4.4 birleşik kontrolü ayrı yerel dalda yapıldı.

## Doğrulama

- Baran dalı: 161 .NET testi geçti.
- 4.4 ile birleşik kod: 171 .NET testi geçti.
- Geçici PostgreSQL 17: migration, önceki verilerin korunması, sahiplik ve
  sayfalama doğrulandı. Chromium’da sonuç kartları ve ayrı 503 mesajları denendi.
- Gerçek Python sandbox kabulü yerel imaj bulunmadığı için bekliyor.
