# Hafta 05 — Baran Demir

**Ders:** BLM4581 · **Tarih:** 23–29 Kasım 2026 · **Lider:** Nisa

- **5.1:** PostgreSQL migration'ı dördüncü haftanın gönderim kayıtlarını korur ve mevcut başarıları ilerlemeye taşır. Aynı görev bir kez sayılır. Aktif kod görevleri tamamlandığında veya okumaya ayrılmış ders öğrenci tarafından işaretlendiğinde tamamlanma zamanı saklanır.
- Öğrenci panosu ile mobil istemci aynı servisi kullanır. `GET /api/mobil/ilerlemeler` listeyi, `GET /api/mobil/ilerlemeler/{id}` ders ayrıntısını verir. `POST /api/mobil/ilerlemeler/{id}/tamamla` yalnız okuma derslerinde kullanılır. Üç uç da JWT gerektirir. Gönderim API'sinin yanıt biçimi korunur.
- **5.5:** Teknik Mülakat Algoritmaları 00 dersinin dolu satır sayacı ve virgüllü alanlar alıştırmaları, geçici kabul veritabanında standart girdi/çıktı örneklerine uyarlandı. Docker'da çalıştırma, hatalı ve doğru test, gönderim ayrıntısı, iki hesap arasında veri ayrımı ve eşzamanlı doğru görevlerle tamamlanma doğrulandı. Bu koşu tam kaynak koleksiyonunun dört katmanlı pytest kabulünü kapsamaz.
- PostgreSQL 17 üzerinde 4. hafta şemasından örnek gönderimle yükseltme ve eski tamamlanma tarihinin korunması doğrulandı. Tarayıcıda okuma dersi tamamlandı ve panoda 1/2 görev ile bağımsız bir tamamlanan ders görüntülendi. Gerçek Jupyter testi de dahil .NET paketi ve gezinme betiği geçti.

6. haftanın başlangıç bağımlılığı notebook kayıt ve API sözleşmesidir. İlerleme kaydı mevcut dersin kimliğine bağlıdır. Görev eklemek kazanılmış tamamlanmayı geri almaz.
