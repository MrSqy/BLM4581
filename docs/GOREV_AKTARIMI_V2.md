# Görev bazlı içerik ve yükleme sözleşmesi v2

## Kaynak ve kapsam

PY-01 güncel kaynakta **İlk Python programı**: Selam, Üçgen, Bilgi kartı, Bozuk programı düzelt, Süre hesabı (5 kod); Çıktı tahmini, Hata mesajını oku (2 açıklama). `calisma.py` ve `test_gorev_N` yoktur. Başlangıç programlarının boş/bozuk olması bilinçlidir. Açıklama görevleri otomatik değerlendirilmez.

Flutter deposundaki `tools/egitim_icerik` değiştirilmeden çalıştırıldı; çıktı yalnız `/tmp/blm-python-source.json` konumuna yazıldı. 20 ders/140 görev/100 çıktı kontrolü bulundu. Örnek PY-01 paketi bu çıktıdan alan eşlemesiyle hazırlandı; çözümler alınmadı. Dosya SHA-256 kanıtları `ornekler/py01-kaynak-kaniti.json` içindedir.

Bu v2 çalıştırma sözleşmesi tek bağımsız Python programı + stdin/stdout kontrolüdür. Üreticinin `kaynakDosyalari`, harici veri dosyaları ve çok-modüllü pytest paketlerini otomatik kurmaz. `veri/` dosyası veya ek modül isteyen dersler bu sözleşmeyle eksiksiz çalışır sayılmamalı; yalnız bu gereksinimi olmayan görevleri yükleyin. PY-01 bu sınıra uygundur. Başlangıç dosyası kaynak adı olarak saklanır, öğrencinin kodu onun yerine çalıştırılır.

## Nisa'nın üreteceği kesin yükleme paketi

API: `POST /api/icerik/aktar`. Yönetici **web cookie** + `GET /api/icerik/antiforgery` token'ının `X-CSRF-TOKEN` başlığı gerekir; Bearer yönetici politikası bu mevcut uç için kullanılmaz. Toplam UTF-8 istek en fazla 5 MiB. `scripts/icerik-aktar.py` mevcut giriş/CSRF akışını destekler. Nisa'nın JSON üreticisi bu çalışmada değiştirilmedi.

Üreticinin kökündeki `semaSurumu:1`, backend `surum:1` değildir! Üretici çıktısı düz ders listesi; yükleme sözleşmesi kategorilere iç içe gruplanır. Aynı JSON'u olduğu gibi yüklemeyin. Tam, gerçek örnek: [py01-v2.json](ornekler/py01-v2.json).

```json
{
  "surum": 2,
  "kategoriler": [{
    "kisaltma": "PY", "ad": "Python dili", "aciklama": "Python", "sira": 1,
    "egitimler": [{
      "kod": "PY-01", "baslik": "İlk Python programı", "sira": 1,
      "icerikMarkdown": "# Görevler", "baslangicKodu": "", "testKodu": "",
      "gorevler": [{
        "no": 1, "baslik": "Selam", "aciklama": "Ekrana selam yaz.",
        "tur": "kod", "baslangicDosyasi": "gorevler/selam.py", "baslangicKodu": "# TODO\n",
        "kontrol": {"tur": "standart-cikti", "beklenenCikti": "Merhaba, dünya!\n",
          "girdi": "", "beklenenCikisKodu": 0, "kullanilmamali": []}
      }, {
        "no": 6, "baslik": "Çıktı tahmini", "aciklama": "Tahminini açıkla.",
        "tur": "aciklama", "baslangicDosyasi": null, "baslangicKodu": "", "kontrol": null
      }]
    }]
  }]
}
```

Yukarıdaki iki görevli kısa JSON yalnız biçim örneğidir; gerçek PY-01'e yüklemeyin! V2 ders görev listesi tam listedir; yüklemek için 7 görevli `ornekler/py01-v2.json` kullanın.

Alan eşlemesi:

| Üretici | Backend yüklemesi |
|---|---|
| kategoriler[].kisaltma | Kategori doğal anahtarı; dersleri kategoriId üzerinden grupla |
| egitimler[].kod | Ders doğal anahtarı; id gönderme |
| görev.no | Ders içindeki doğal anahtar; üretilen geçici id/egitimId gönderme |
| baslangicDosyasi var | tur=kod; görev baslangicKodu ve kontrol alanlarını aynen taşı |
| baslangicDosyasi null | tur=aciklama; kod="", dosya=null, kontrol=null |
| kontrol.girdi/beklenenCikti | Satır sonu ve boşluklar aynen korunur; trim yapma |
| kontrol.beklenenCikisKodu | Kaynakta belirtilmemişse üreticinin varsayılanı 0 |
| kontrol.kullanilmamali | Kaynağın dizisi; boşsa [] |
| ders.baslangicKodu/testKodu | PY-01 için ""; ayrı programları birleştirme |
| uyarilar/testler/markdownDosyalari/ortakDosyalar/kaynakDosyalari | Yükleme alanları değildir; uyarıları yayın öncesi değerlendir |

V1'in kategori/ders/görev zorunlu alanları ve sınırları devam eder (bkz. ICERIK_AKTARIMI.md). V2 her görevde `tur` ve `baslangicKodu` ister. Kod görevinde dosya yalnız `gorevler/[A-Za-z0-9_-]+.py`; mutlak yol, `..`, alt dizin ve symlink yoktur. Dosya adları ders içinde tekildir. Kontrol türü yalnız `standart-cikti`; `beklenenCikti` (boş olabilir, 64000 karakter), `girdi` (boş olabilir, 32000), `beklenenCikisKodu` (0–255), `kullanilmamali` (en fazla 30, her biri 1–100 karakter ve satır sonu içermez) alanlarının tamamı zorunludur. Null/eksik beklenen çıktıya kontrol uydurmayın; kod görevini yükleme öncesi düzeltin. Açıklama görevinde dosya/kontrol null, başlangıç kodu boş olmalı.

Aktarım tek transaction'dır. Kategori kısaltması/ders kodu/görev numarası eşleşmesinde mevcut ID'ler korunur. V1 eski pytest derslerine eklemeli davranmaya devam eder; v2'ye geçmiş dersi v1 ile değiştirmek 400'dür. V2 **yalnız paketteki derslerin** görev listelerini tam kabul eder: pakette bulunmayan eski görevler `Aktif=false` olur. Satır/metin/ID silinmez. Yeniden gönderilen görev aynı ID ile aktif olur. Pakette olmayan ders ve kategoriler değiştirilmez. Kullanıcı/rol/oturum kayıtları aktarım kapsamı dışındadır. HTTP doğrulama hatası 400, DB çakışması 409; paket geri alınır.

## Mevcut veritabanında açık geçiş

Normal uygulama açılışı migration veya içerik aktarımı yapmaz. Bu çalışmada kullanıcının DB'sine dokunulmadı. Şema migration'ı yalnız `Gorevler` tablosuna Aktif (varsayılan true), Tur (eski-pytest), BaslangicDosyasi, BaslangicKodu, KontrolJson sütunlarını ekler. Üretilen seed UpdateData/DeleteData işlemleri migration'dan çıkarılmıştır. Böylece kullanıcıların değiştirdiği eski içerik otomatik ezilmez.

1. Mevcut yedekleme yönteminle PostgreSQL yedeği al, açık uygulamayı durdur.
2. Depo kökünde derle ve yalnız şemayı güncelle:

```bash
dotnet build EgitimPlatformu.slnx --no-restore
~/.dotnet/tools/dotnet-ef database update --project src/EgitimPlatformu.Web
```

3. Önce planı oku; bu adım içerik yazmaz:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --no-restore --no-launch-profile --project src/EgitimPlatformu.Web -- --icerik-planla "$PWD/docs/ornekler/py01-v2.json"
```

Eski PY-01 için 7 görev yazılacağı ve görev 8'in pasifleşeceği görünmeli. Paketteki 1–7 metin/kod/kontrolünün yenileneceğini onaylayınca açıkça uygula:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --no-restore --no-launch-profile --project src/EgitimPlatformu.Web -- --icerik-aktar "$PWD/docs/ornekler/py01-v2.json"
```

Bu CLI komutları yerel DB yönetim yetkisiyle çalışır; HTTP kimlik doğrulaması yapmaz, port açmadan kapanır. Komutlara mutlak paket yolu verin; `dotnet run` çalışma dizinini proje dizinine alır. DB bağlantısı mevcut user-secrets/ortam yapılandırmasından alınır. Tekrar çalıştırmak kayıt çoğaltmaz. `OrnekVeri` EnsureCreated/model örneklerinde güncel PY-01'i kullanır; migration zinciriyle yeni kurulan DB için de aynı v2 aktarım adımı gerekir.

4. JWT anahtarıyla uygulamayı yeniden başlat; GET `/api/mobil/egitimler/1` (gerçek ID'yi listeden al) yanıtında 7 aktif görev, kod 1–5, açıklama 6–7 gör. Görev 8'in çalıştırılması 404 olmalı. Arşiv satırı DB'de kalır. Şema Down işlemi yeni metadata sütunlarını kaldırır; geri alma için bunu otomatik çalıştırmayın, yedekten/ayrı planla ilerleyin.

## Mobil API ve çalıştırma

JWT ve kategori içindeki ilk ders kuralı değişmedi. Sonraki dersler geçerli Bearer ister. İstek gövdesi yalnız öğrenci kodudur; beklenen sonuç/girdi/kural/dosya istemciden alınmaz.

- `GET /api/mobil/egitimler/{id}`: aktif görevleri `id,egitimId,no,baslik,aciklama,tur,baslangicDosyasi,baslangicKodu,kontrol` ile döndürür. Genel `/api/egitimler/{id}` de aynı görev DTO'sunu kullanır. `kontrol` kod görevinde yukarıdaki nesne, açıklamada null. Kurallar görünür eğitim ölçütleridir; sunucu istekte gönderilen kopyalarını kullanmaz.
- `POST /api/mobil/egitimler/{id}/gorevler/{no}/calistir`: `{"kod":"print('Merhaba, dünya!')"}`. Kaydedilmiş stdin ile programı çalıştırır; beklenen çıktı üzerinden geçme/kalma vermez.
- Aynı yolda `/test`: programı çalıştırır; stdout'u **harfi harfine**, çıkış kodunu ve yasak kuralları sunucuda karşılaştırır. Açıklama/eski-pytest görevi 409, pasif/olmayan görev 404, korunan ders 401, altyapı/meşgul 503, limit 429, doğrulama 400.

Başarılı test yanıtı:

```json
{"no":1,"basarili":true,"durum":"basarili","cikti":"Merhaba, dünya!\n","hata":"","cikisKodu":0,"ciktiEslesiyor":true,"cikisKoduEslesiyor":true,"kuralIhlalleri":[]}
```

Çalıştırma yanıtında iki eşleşme alanı null, kuralIhlalleri [] olur; exit=0 ise tamamlandi, aksi hata. Testte yanlış sonuç 200 + basarili=false/durum=basarisiz döner; negatif olmayan beklenen çıkış kodu 0 olmak zorunda değildir. Zaman aşımı: basarili=false, durum=zaman_asimi, cikisKodu/eşleşmeler null. Çıktı taşması: durum=cikti_siniri, başarı false. stdout/stderr en fazla 24000 byte raporlanır. Satır sonları normalize edilmez. Yasaklar yorum/dizgiler dışındaki metinde kelime sınırıyla aranır; bu pedagojik kaynak kuralıdır, güvenlik filtresi değildir.

Çalıştırma mevcut Docker profilinde: ağ yok, salt okunur kök, uid=10001, 128 MiB RAM, 0.5 CPU, 64 süreç, 16 MiB tmpfs. Kod ayrı Python sürecinde, yazılabilir geçici çalışma dizininde çalışır. Beklenen çıktı/çıkış kodu sandbox'a gönderilmez; stdout JSON protokolünden ayrıdır. İç süre 5 sn, dış 8 sn. Sonuçlar eğitim geri bildirimidir; kötü niyetli koda karşı güvenilir sınav/not sistemi değildir.

Eski `/api/mobil/egitimler/{id}/calistir|test` yalnız eski-pytest derslerinde korunur. Güncel görevli derste 409 döner; istemciyi görev uçlarına taşıyın. Liste görev sayıları ve Razor ders detayları pasif görevleri içermez.

## Doğrulama

- .NET entegrasyonu: SQLite + gerçek Identity/JWT + HTTP; runner taklitleri ayrıca sunucu kontrolü ve hata yollarını ölçer.
- `python3 scripts/accept-task-sandbox.py`: önceden mevcut gerçek Docker imajıyla 11 senaryo; paket/imaj indirmez.
- `python3 scripts/accept-task-migration.py`: yalnız yeni/geçici postgres:16.15 konteynerinde eski migration → şema migration → salt okunur plan → iki v2 aktarımı. Eski görev 8'in metni/ID'si ve fixture kullanıcısı korunur; 7 aktif/8 toplam doğrulanır. Testin kendi konteyneri kaldırılır, kullanıcı DB'si kullanılmaz.
- `dotnet-ef migrations has-pending-model-changes`: model/snapshot eşitliği kontrolü. Telefon/Flutter kabulü bu deponun kapsamı dışında.

Son kabul sonucu: **120 .NET testi**, **11 gerçek Docker senaryosu**, **izole gerçek PostgreSQL geçiş kabulü** geçti. Temiz derleme sonrası EF model/snapshot eşitliği doğrulandı. Kullanıcının mevcut DB’sine migration/aktarım yapılmadı. Flutter deposu ve üretici kodu değiştirilmedi.
