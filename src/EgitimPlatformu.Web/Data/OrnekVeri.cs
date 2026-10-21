using EgitimPlatformu.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Data;

// Migration ile veritabanına yazılan örnek veri.
// Kaynak: Mühendislik Eğitimleri deposu, yazilim-gelistirme/programlama-dilleri/python/dersler/PY-01..PY-03.
// Asıl içerik aktarımı içerik aracıyla yapılacak; bu veri geliştirme ve deneme içindir.
public static class OrnekVeri
{
    public static void Ekle(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Kategori>().HasData(
            new Kategori
            {
                Id = 1, Ad = "Python dili", Kisaltma = "PY", Sira = 1,
                Aciklama = "Temelden dilin ve çalışma zamanının derinliklerine; ders başına sekiz görev."
            },
            new Kategori
            {
                Id = 2, Ad = "Teknik Mülakat Algoritmalar", Kisaltma = "TMA", Sira = 2,
                Aciklama = "Karmaşıklık analizinden dinamik programlamaya teknik mülakat algoritmaları."
            }
        );

        modelBuilder.Entity<Egitim>().HasData(
            new Egitim
            {
                Id = 1, KategoriId = 1, Kod = "PY-01", Baslik = "Çalıştırma modeli", Sira = 1,
                IcerikMarkdown = @"# PY-01 — Bağımsız görevler

Saat ve dakika sabitlerinden toplam saniye hesapla. `(1, 2)` için 3720, `(0, 0)` için 0 beklenmeli; hesap ile çıktıyı ayrı satırlarda tut.

Çözümünü ayrı bir çalışma dosyasında oluştur. Normal durum, sınır/hata durumu ve gerekçeni teslim et. Kendi denemenden sonra [gerekçeli çözüm notlarıyla](cozumler/CEVAPLAR.md) karşılaştır.
## Kademeli görevler

Üstteki özgün görev korunmuştur. Aşağıdaki sekiz görev onu küçük adımlara ayırır ve genişletir. Önce kendi `calisma.py` dosyanı tamamla. `ornek.py` dersin çalışan örneği; `cozumler/referans.py` ayrı çözümdür. Testlerin geçtiğini görmek açıklama ve mini görev değerlendirmesinin yerini tutmaz.

Ders klasöründe:

```bash
PYTEST_DISABLE_PLUGIN_AUTOLOAD=1 python3 -m pytest -q test_alistirmalar.py
```

Başlangıç şablonunda kod görevleri bilerek başarısız olur. Bir görevi çalışırken `-k gorev_3` gibi bir filtre ekleyebilirsin. Başka çalışma dosyası için `--calisma /tam/yol/cozumum.py` kullan. Kendi denemenden sonra referansı aynı testlerde çalıştırmak için `--referans` ekle.

### Görev 1 — Temel — Kaynak ve yorumlayıcı

`python3 --version` ve `python3 -c ""import sys; print(sys.executable)""` çalıştır; iki çıktının ne söylediğini yaz.

**Kabul ölçütü:** Sürüm ile yürütülebilir dosya yolu ayrı kaydedilir; sanal ortamın neyi ayırdığı bir cümleyle açıklanır.

**Kontrol:** Cevap anahtarıyla elle karşılaştır; ölçütlerin her birini işaretle.

### Görev 2 — Temel — Sonucu öngör

`dakika=3; saniye=dakika*60; dakika=5` sonrasında iki değişkenin değerini çalıştırmadan yaz.

**Kabul ölçütü:** dakika=5 ve saniye=180 denir; atamanın önceki hesaplamayı yeniden yürütmediği açıklanır.

**Kontrol:** Cevap anahtarıyla elle karşılaştır; ölçütlerin her birini işaretle.

### Görev 3 — Temel — Dakikayı dönüştür

`dakika_saniye(dakika)` işlevini yaz. Girdi bu görevde negatif olmayan tamsayıdır.

**Kabul ölçütü:** 0→0, 3→180, 7→420, 1440→86400; fonksiyon sonucu döndürür.

**Kontrol:** `-k gorev_3`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 4 — Orta — İki birimi birleştir

Özgün görevi `toplam_saniye(saat, dakika)` işlevine taşı. Negatif olmayan tamsayılar kullan; dakika 60 üzerinde de toplam süreye katılır.

**Kabul ölçütü:** (1,2)→3720, (0,0)→0, (0,90)→5400 ve (2,0)→7200.

**Kontrol:** `-k gorev_4`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 5 — Orta — Hesap ve sunum

`sure_satiri(saat,dakika)` tam olarak `Toplam: 3720 saniye` biçiminde metin döndürsün; hesabı önceki işleve devret.

**Kabul ölçütü:** (1,2) için tam metin; (0,0) için `Toplam: 0 saniye`; stdout boş kalır.

**Kontrol:** `-k gorev_5`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 6 — Orta · hata ayıklama — Yanlış dönüşüm katsayısı

Hatalı `duzelt(saat,dakika): return (saat+dakika)*60` hesabını düzelt; (1,0) girdisinin hatayı neden yakaladığını yaz.

**Kabul ölçütü:** duzelt(1,0)=3600, duzelt(1,2)=3720, duzelt(0,5)=300.

**Kontrol:** `-k gorev_6`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 7 — Zor — Güvenli süre girişi

`guvenli_toplam(saat,dakika)` ekle. Yalnız bool olmayan, negatif olmayan iki int kabul et; uygunsuz değerlerde ValueError yükselt.

**Kabul ölçütü:** Sıfır, büyük tamsayı ve normal süreler doğru; -1, 1.5, ""2"" ve True girdileri reddedilir.

**Kontrol:** `-k gorev_7`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 8 — Zor · açık uçlu mini görev — Küçük süre raporu

`rapor(saat,dakika)` ile `{""saniye"": 3720, ""metin"": ""Toplam: 3720 saniye""}` biçimini üret. Geçersiz girdiler görev 7 gibi reddedilsin. Bunu bir terminal arayüzünde nasıl sunacağını kendin tasarla; arayüz tasarımını iki örnekle yaz.

**Kabul ölçütü:** İki alan tutarlı; (0,0) doğru; negatif giriş ValueError; hesap/sunum ayrımını açıklayan iki cümle ve normal/hata arayüz örneği var.

**Kontrol:** `-k gorev_8`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

[Gerekçeli çözümler](cozumler/CEVAPLAR.md). Mini görevde tasarımını, bir normal ve bir hata/sınır örneğini yaz; test raporuyla birlikte sakla.
",
                BaslangicKodu = @"""""""Her işlevi ALISTIRMALAR.md sözleşmesine göre tamamla.""""""
def dakika_saniye(dakika): raise NotImplementedError
def toplam_saniye(saat, dakika): raise NotImplementedError
def sure_satiri(saat, dakika): raise NotImplementedError
def duzelt(saat, dakika): raise NotImplementedError
def guvenli_toplam(saat, dakika): raise NotImplementedError
def rapor(saat, dakika): raise NotImplementedError
",
                TestKodu = @"import pytest

@pytest.mark.parametrize(""dakika,beklenen"", [(0,0),(3,180),(7,420),(1440,86400)])
def test_gorev_3(cozum,dakika,beklenen):
    assert cozum.dakika_saniye(dakika)==beklenen

@pytest.mark.parametrize(""s,d,beklenen"",[(1,2,3720),(0,0,0),(0,90,5400),(2,0,7200)])
def test_gorev_4(cozum,s,d,beklenen):
    assert cozum.toplam_saniye(s,d)==beklenen

@pytest.mark.parametrize(""s,d,beklenen"",[(1,2,""Toplam: 3720 saniye""),(0,0,""Toplam: 0 saniye"")])
def test_gorev_5(cozum,capsys,s,d,beklenen):
    assert cozum.sure_satiri(s,d)==beklenen
    assert capsys.readouterr().out==""""

@pytest.mark.parametrize(""s,d,beklenen"",[(1,0,3600),(1,2,3720),(0,5,300)])
def test_gorev_6(cozum,s,d,beklenen):
    assert cozum.duzelt(s,d)==beklenen

@pytest.mark.parametrize(""s,d"",[(-1,0),(0,-1),(True,0),(0,False),(1.5,2),(""2"",0)])
def test_gorev_7_hata(cozum,s,d):
    with pytest.raises(ValueError): cozum.guvenli_toplam(s,d)

def test_gorev_7_sinir(cozum):
    assert cozum.guvenli_toplam(0,0)==0
    assert cozum.guvenli_toplam(1000000,2)==3600000120

def test_gorev_8(cozum):
    assert cozum.rapor(1,2)=={""saniye"":3720,""metin"":""Toplam: 3720 saniye""}
    assert cozum.rapor(0,0)=={""saniye"":0,""metin"":""Toplam: 0 saniye""}
    with pytest.raises(ValueError): cozum.rapor(-1,2)
"
            },
            new Egitim
            {
                Id = 2, KategoriId = 1, Kod = "PY-02", Baslik = "Değerler ve akış", Sira = 2,
                IcerikMarkdown = @"# PY-02 — Bağımsız görevler

0..100 arasında tam sayı not alan bir fonksiyon yaz. 0, 100, -1, 101 ve ""elli"" girdilerini dene; geçersiz girdiyi 0’a çevirmemeli.

Çözümünü ayrı bir çalışma dosyasında oluştur. Normal durum, sınır/hata durumu ve gerekçeni teslim et. Kendi denemenden sonra [gerekçeli çözüm notlarıyla](cozumler/CEVAPLAR.md) karşılaştır.
## Kademeli görevler

Üstteki özgün görev korunmuştur. Aşağıdaki sekiz görev onu küçük adımlara ayırır ve genişletir. Önce kendi `calisma.py` dosyanı tamamla. `ornek.py` dersin çalışan örneği; `cozumler/referans.py` ayrı çözümdür. Testlerin geçtiğini görmek açıklama ve mini görev değerlendirmesinin yerini tutmaz.

Ders klasöründe:

```bash
PYTEST_DISABLE_PLUGIN_AUTOLOAD=1 python3 -m pytest -q test_alistirmalar.py
```

Başlangıç şablonunda kod görevleri bilerek başarısız olur. Bir görevi çalışırken `-k gorev_3` gibi bir filtre ekleyebilirsin. Başka çalışma dosyası için `--calisma /tam/yol/cozumum.py` kullan. Kendi denemenden sonra referansı aynı testlerde çalıştırmak için `--referans` ekle.

### Görev 1 — Temel — Metin mi sayı mı?

`""3""*2`, `int(""3"")*2`, `bool(""0"")` ifadelerinin türünü ve sonucunu tahmin et.

**Kabul ölçütü:** Sırasıyla ""33""/str, 6/int ve True/bool; boş olmayan metin açıklanır.

**Kontrol:** Cevap anahtarıyla elle karşılaştır; ölçütlerin her birini işaretle.

### Görev 2 — Temel — Notu oku

`not_oku(metin)` yaz: str girdiyi strip/int ile dönüştür; 0..100 dışını ValueError ile reddet.

**Kabul ölçütü:** "" 0 ""→0, ""100""→100, ""75""→75; """", ""elli"", ""1.5"", ""-1"", ""101"" reddedilir.

**Kontrol:** `-k gorev_2`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 3 — Temel — Geçti mi?

`durum(notu)` geçerli tamsayı not için 50 ve üzerindeyse ""gecti"", değilse ""kaldi"" döndürsün.

**Kabul ölçütü:** 0/49 kaldi; 50/100 gecti.

**Kontrol:** `-k gorev_3`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 4 — Orta — Geçerli notları ayır

`ayir(metinler)` sıralı str listesinden `(notlar, hatali_indeksler)` döndürsün; indeksler 0 tabanlı.

**Kabul ölçütü:** [""10"",""x"",""101"",""0""]→([10,0],[1,2]); boş liste→([],[]); giriş değişmez.

**Kontrol:** `-k gorev_4`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 5 — Orta — Döngüyü bitir

`menu(metinler)` ilk strip sonrası `q` girdisine kadar geçerli notları toplayıp döndürsün; bozuk notları atla. q büyük/küçük harf duyarlıdır.

**Kabul ölçütü:** [""2"",""x"",""3"","" q "",""100""]→5; ilk q→0; q yoksa sonuna kadar işler.

**Kontrol:** `-k gorev_5`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 6 — Orta · hata ayıklama — Dahil sınırlar

`sinir_duzelt(metin)` içindeki varsayılan `0 < n < 100` kusurunu düzelt; not_oku ile aynı sözleşmeye getir.

**Kabul ölçütü:** ""0"" ve ""100"" kabul; ""-1"" ve ""101"" ret. Hangi iki örneğin yanlış sürümü yakaladığını yaz.

**Kontrol:** `-k gorev_6`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 7 — Zor — Sayaçların tutarlılığı

`ozet(metinler)` toplam geçerli sayısı, geçen ve kalan sayısı ile hatalı indeksleri sözlükte döndürsün: gecerli, gecen, kalan, hatalar.

**Kabul ölçütü:** [""0"",""50"",""100"",""x""]→{gecerli:3,gecen:2,kalan:1,hatalar:[3]}; geçerli=geçen+kalan.

**Kontrol:** `-k gorev_7`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 8 — Zor · açık uçlu mini görev — Not takip çekirdeği

`not_defteri(metinler)` ozet alanlarına `en_yuksek` eklesin; geçerli veri yoksa None olsun. Veriyi terminalden, dosyadan veya elle listeden alma seçeneklerinden birini seçip hata sunumunu tasarla.

**Kabul ölçütü:** Boş ve tümü bozuk veri en_yuksek=None; [""40"",""90"",""x""] için 90 ve hatalar=[2]. Tasarımda geçersiz veri ile sıfır not ayrılır.

**Kontrol:** `-k gorev_8`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

[Gerekçeli çözümler](cozumler/CEVAPLAR.md). Mini görevde tasarımını, bir normal ve bir hata/sınır örneğini yaz; test raporuyla birlikte sakla.
",
                BaslangicKodu = @"def not_oku(metin): raise NotImplementedError
def durum(notu): raise NotImplementedError
def ayir(metinler): raise NotImplementedError
def menu(metinler): raise NotImplementedError
def sinir_duzelt(metin): raise NotImplementedError
def ozet(metinler): raise NotImplementedError
def not_defteri(metinler): raise NotImplementedError
",
                TestKodu = @"import pytest

@pytest.mark.parametrize(""g,b"",[("" 0 "",0),(""100"",100),(""75"",75)])
def test_gorev_2(cozum,g,b): assert cozum.not_oku(g)==b
@pytest.mark.parametrize(""g"",["""",""elli"",""1.5"",""-1"",""101""])
def test_gorev_2_hata(cozum,g):
    with pytest.raises(ValueError): cozum.not_oku(g)
@pytest.mark.parametrize(""g,b"",[(0,""kaldi""),(49,""kaldi""),(50,""gecti""),(100,""gecti"")])
def test_gorev_3(cozum,g,b): assert cozum.durum(g)==b

def test_gorev_4(cozum):
    data=[""10"",""x"",""101"",""0""]
    assert cozum.ayir(data)==([10,0],[1,2])
    assert data==[""10"",""x"",""101"",""0""]
    assert cozum.ayir([])==([],[])
    assert cozum.ayir([""x"",""y"",""40""])==([40],[0,1])
@pytest.mark.parametrize(""g,b"",[([""2"",""x"",""3"","" q "",""100""],5),([""q"",""100""],0),([""2"",""3""],5),([],0)])
def test_gorev_5(cozum,g,b): assert cozum.menu(g)==b

def test_gorev_6(cozum):
    assert cozum.sinir_duzelt(""0"")==0
    assert cozum.sinir_duzelt(""100"")==100
    for g in [""-1"",""101""]:
        with pytest.raises(ValueError): cozum.sinir_duzelt(g)

def test_gorev_7(cozum):
    assert cozum.ozet([""0"",""50"",""100"",""x""])==dict(gecerli=3,gecen=2,kalan=1,hatalar=[3])
    assert cozum.ozet([])==dict(gecerli=0,gecen=0,kalan=0,hatalar=[])

def test_gorev_8(cozum):
    assert cozum.not_defteri([""40"",""90"",""x""])==dict(gecerli=2,gecen=1,kalan=1,hatalar=[2],en_yuksek=90)
    assert cozum.not_defteri([])['en_yuksek'] is None
    assert cozum.not_defteri([""x""])['en_yuksek'] is None
    assert cozum.not_defteri([""0""])['en_yuksek']==0
"
            },
            new Egitim
            {
                Id = 3, KategoriId = 1, Kod = "PY-03", Baslik = "Koleksiyonlar", Sira = 3,
                IcerikMarkdown = @"# PY-03 — Bağımsız görevler

İçinde etiket listesi bulunan iki kullanıcı kaydı oluştur. Birinin etiketi değişince diğeri değişmesin. Sonra kelime frekansını büyük/küçük harf duyarsız say.

Çözümünü ayrı bir çalışma dosyasında oluştur. Normal durum, sınır/hata durumu ve gerekçeni teslim et. Kendi denemenden sonra [gerekçeli çözüm notlarıyla](cozumler/CEVAPLAR.md) karşılaştır.
## Kademeli görevler

Üstteki özgün görev korunmuştur. Aşağıdaki sekiz görev onu küçük adımlara ayırır ve genişletir. Önce kendi `calisma.py` dosyanı tamamla. `ornek.py` dersin çalışan örneği; `cozumler/referans.py` ayrı çözümdür. Testlerin geçtiğini görmek açıklama ve mini görev değerlendirmesinin yerini tutmaz.

Ders klasöründe:

```bash
PYTEST_DISABLE_PLUGIN_AUTOLOAD=1 python3 -m pytest -q test_alistirmalar.py
```

Başlangıç şablonunda kod görevleri bilerek başarısız olur. Bir görevi çalışırken `-k gorev_3` gibi bir filtre ekleyebilirsin. Başka çalışma dosyası için `--calisma /tam/yol/cozumum.py` kullan. Kendi denemenden sonra referansı aynı testlerde çalıştırmak için `--referans` ekle.

### Görev 1 — Temel — Nesne oklarını çiz

`a=[[1]]; b=a; c=a.copy(); c[0].append(2)` sonrası a,b,c değerlerini ve `a is b`, `a is c`, `a[0] is c[0]` sonuçlarını yaz.

**Kabul ölçütü:** Üç değer de `[[1,2]]`; kimlikler True,False,True; dış ve iç nesne ayrılır.

**Kontrol:** Cevap anahtarıyla elle karşılaştır; ölçütlerin her birini işaretle.

### Görev 2 — Temel — Sıralı tekilleştirme

`tekil(kelimeler)` metin listesini ilk görülme sırasını koruyarak tekilleştirsin. Büyük/küçük harf burada ayrı kabul edilir.

**Kabul ölçütü:** [""b"",""a"",""b"",""B""]→[""b"",""a"",""B""]; boş liste→[]; girdi korunur.

**Kontrol:** `-k gorev_2`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 3 — Temel — Frekans

`frekans(kelimeler)` casefold uygulanmış kelimelerin sayılarını dict olarak döndürsün; kelimeleri ayrıca strip etme.

**Kabul ölçütü:** [""A"",""a"",""B"",""Straße"",""STRASSE""]→{""a"":2,""b"":1,""strasse"":2}; boş veri→{}.

**Kontrol:** `-k gorev_3`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 4 — Orta — Bağımsız kullanıcılar

`kullanicilar(adlar)` ile her ad için {ad:...,etiketler:[]} kaydı üret; her kaydın listesi ayrı olsun.

**Kabul ölçütü:** İki kayıttan birine etiket eklemek diğerini etkilemez; ad sırası ve tekrarlar korunur.

**Kontrol:** `-k gorev_4`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 5 — Orta — Etiketli kopya

`etiket_ekle(kayit,etiket)` yalnız ad ve etiketler alanı olan kaydın yeni sürümünü döndürsün. Etiket metindir; özgün etiket listesi değişmesin.

**Kabul ölçütü:** {ad:""Ada"",etiketler:[""py""]} ve ""sql""→yeni [""py"",""sql""]; özgün [""py""].

**Kontrol:** `-k gorev_5`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 6 — Orta · hata ayıklama — Paylaşılan satırlar

Hatalı `tablo(satir,sutun): return [[0]*sutun]*satir` kodunu düzelt. Negatif boyutlarda ValueError ver.

**Kabul ölçütü:** tablo(2,2) satırları bağımsız; (0,3)→[]; (2,0)→[[],[]]; negatif ret.

**Kontrol:** `-k gorev_6`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 7 — Zor — Sayımları birleştir

`birlestir(a,b)` metin anahtarlı, negatif olmayan tamsayı sayımları yeni dict içinde toplasın; kaynak sözlükler değişmesin.

**Kabul ölçütü:** {x:2,y:1}+{x:3,z:4}→{x:5,y:1,z:4}; eksik anahtar 0; boşlar desteklenir.

**Kontrol:** `-k gorev_7`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

### Görev 8 — Zor · açık uçlu mini görev — Küçük metin raporu

`metin_raporu(kelimeler)` {toplam,tekil,frekans} döndürsün; tekil sayısı casefold sonrası hesaplanır. Raporu alfabetik ya da sıklık sırasıyla göstermek için bir sunum tercihi yapıp gerekçelendir.

**Kabul ölçütü:** [""A"",""a"",""B""]→{toplam:3,tekil:2,frekans:{a:2,b:1}}; boş veri için sıfırlar/{}. Sunum sırası sayım sonucunu değiştirmez.

**Kontrol:** `-k gorev_8`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

[Gerekçeli çözümler](cozumler/CEVAPLAR.md). Mini görevde tasarımını, bir normal ve bir hata/sınır örneğini yaz; test raporuyla birlikte sakla.
",
                BaslangicKodu = @"def tekil(kelimeler): raise NotImplementedError
def frekans(kelimeler): raise NotImplementedError
def kullanicilar(adlar): raise NotImplementedError
def etiket_ekle(kayit,etiket): raise NotImplementedError
def tablo(satir,sutun): raise NotImplementedError
def birlestir(a,b): raise NotImplementedError
def metin_raporu(kelimeler): raise NotImplementedError
",
                TestKodu = @"import pytest

def test_gorev_2(cozum):
    data=[""b"",""a"",""b"",""B""]
    assert cozum.tekil(data)==[""b"",""a"",""B""]
    assert data==[""b"",""a"",""b"",""B""]
    assert cozum.tekil([])==[]

def test_gorev_3(cozum):
    assert cozum.frekans([""A"",""a"",""B"",""Straße"",""STRASSE""])=={""a"":2,""b"":1,""strasse"":2}
    assert cozum.frekans([])=={}
    assert cozum.frekans(["" a "",""a""])=={"" a "":1,""a"":1}

def test_gorev_4(cozum):
    data=cozum.kullanicilar([""Ada"",""Ada""])
    assert data==[dict(ad=""Ada"",etiketler=[]),dict(ad=""Ada"",etiketler=[])]
    data[0]['etiketler'].append('py')
    assert data[1]['etiketler']==[]
    assert cozum.kullanicilar([])==[]

def test_gorev_5(cozum):
    old=dict(ad=""Ada"",etiketler=[""py""])
    new=cozum.etiket_ekle(old,""sql"")
    assert new==dict(ad=""Ada"",etiketler=[""py"",""sql""])
    assert old==dict(ad=""Ada"",etiketler=[""py""])
    new['etiketler'].append('go')
    assert old['etiketler']==['py']

def test_gorev_6(cozum):
    t=cozum.tablo(2,2); t[0][0]=7
    assert t==[[7,0],[0,0]]
    assert cozum.tablo(0,3)==[]
    t=cozum.tablo(2,0); t[0].append(7)
    assert t==[[7],[]]
    for s,c in [(-1,2),(2,-1)]:
        with pytest.raises(ValueError): cozum.tablo(s,c)

def test_gorev_7(cozum):
    a={'x':2,'y':1}; b={'x':3,'z':4}
    assert cozum.birlestir(a,b)=={'x':5,'y':1,'z':4}
    assert a=={'x':2,'y':1} and b=={'x':3,'z':4}
    assert cozum.birlestir({},{})=={}

def test_gorev_8(cozum):
    assert cozum.metin_raporu(['A','a','B'])==dict(toplam=3,tekil=2,frekans={'a':2,'b':1})
    assert cozum.metin_raporu([])==dict(toplam=0,tekil=0,frekans={})
"
            }
        );

        modelBuilder.Entity<Gorev>().HasData(
            new Gorev { Id = 1, EgitimId = 1, No = 1, Baslik = @"Temel — Kaynak ve yorumlayıcı", Aciklama = @"`python3 --version` ve `python3 -c ""import sys; print(sys.executable)""` çalıştır; iki çıktının ne söylediğini yaz.

**Kabul ölçütü:** Sürüm ile yürütülebilir dosya yolu ayrı kaydedilir; sanal ortamın neyi ayırdığı bir cümleyle açıklanır.

**Kontrol:** Cevap anahtarıyla elle karşılaştır; ölçütlerin her birini işaretle." },
            new Gorev { Id = 2, EgitimId = 1, No = 2, Baslik = @"Temel — Sonucu öngör", Aciklama = @"`dakika=3; saniye=dakika*60; dakika=5` sonrasında iki değişkenin değerini çalıştırmadan yaz.

**Kabul ölçütü:** dakika=5 ve saniye=180 denir; atamanın önceki hesaplamayı yeniden yürütmediği açıklanır.

**Kontrol:** Cevap anahtarıyla elle karşılaştır; ölçütlerin her birini işaretle." },
            new Gorev { Id = 3, EgitimId = 1, No = 3, Baslik = @"Temel — Dakikayı dönüştür", Aciklama = @"`dakika_saniye(dakika)` işlevini yaz. Girdi bu görevde negatif olmayan tamsayıdır.

**Kabul ölçütü:** 0→0, 3→180, 7→420, 1440→86400; fonksiyon sonucu döndürür.

**Kontrol:** `-k gorev_3`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 4, EgitimId = 1, No = 4, Baslik = @"Orta — İki birimi birleştir", Aciklama = @"Özgün görevi `toplam_saniye(saat, dakika)` işlevine taşı. Negatif olmayan tamsayılar kullan; dakika 60 üzerinde de toplam süreye katılır.

**Kabul ölçütü:** (1,2)→3720, (0,0)→0, (0,90)→5400 ve (2,0)→7200.

**Kontrol:** `-k gorev_4`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 5, EgitimId = 1, No = 5, Baslik = @"Orta — Hesap ve sunum", Aciklama = @"`sure_satiri(saat,dakika)` tam olarak `Toplam: 3720 saniye` biçiminde metin döndürsün; hesabı önceki işleve devret.

**Kabul ölçütü:** (1,2) için tam metin; (0,0) için `Toplam: 0 saniye`; stdout boş kalır.

**Kontrol:** `-k gorev_5`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 6, EgitimId = 1, No = 6, Baslik = @"Orta · hata ayıklama — Yanlış dönüşüm katsayısı", Aciklama = @"Hatalı `duzelt(saat,dakika): return (saat+dakika)*60` hesabını düzelt; (1,0) girdisinin hatayı neden yakaladığını yaz.

**Kabul ölçütü:** duzelt(1,0)=3600, duzelt(1,2)=3720, duzelt(0,5)=300.

**Kontrol:** `-k gorev_6`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 7, EgitimId = 1, No = 7, Baslik = @"Zor — Güvenli süre girişi", Aciklama = @"`guvenli_toplam(saat,dakika)` ekle. Yalnız bool olmayan, negatif olmayan iki int kabul et; uygunsuz değerlerde ValueError yükselt.

**Kabul ölçütü:** Sıfır, büyük tamsayı ve normal süreler doğru; -1, 1.5, ""2"" ve True girdileri reddedilir.

**Kontrol:** `-k gorev_7`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 8, EgitimId = 1, No = 8, Baslik = @"Zor · açık uçlu mini görev — Küçük süre raporu", Aciklama = @"`rapor(saat,dakika)` ile `{""saniye"": 3720, ""metin"": ""Toplam: 3720 saniye""}` biçimini üret. Geçersiz girdiler görev 7 gibi reddedilsin. Bunu bir terminal arayüzünde nasıl sunacağını kendin tasarla; arayüz tasarımını iki örnekle yaz.

**Kabul ölçütü:** İki alan tutarlı; (0,0) doğru; negatif giriş ValueError; hesap/sunum ayrımını açıklayan iki cümle ve normal/hata arayüz örneği var.

**Kontrol:** `-k gorev_8`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

[Gerekçeli çözümler](cozumler/CEVAPLAR.md). Mini görevde tasarımını, bir normal ve bir hata/sınır örneğini yaz; test raporuyla birlikte sakla." },
            new Gorev { Id = 9, EgitimId = 2, No = 1, Baslik = @"Temel — Metin mi sayı mı?", Aciklama = @"`""3""*2`, `int(""3"")*2`, `bool(""0"")` ifadelerinin türünü ve sonucunu tahmin et.

**Kabul ölçütü:** Sırasıyla ""33""/str, 6/int ve True/bool; boş olmayan metin açıklanır.

**Kontrol:** Cevap anahtarıyla elle karşılaştır; ölçütlerin her birini işaretle." },
            new Gorev { Id = 10, EgitimId = 2, No = 2, Baslik = @"Temel — Notu oku", Aciklama = @"`not_oku(metin)` yaz: str girdiyi strip/int ile dönüştür; 0..100 dışını ValueError ile reddet.

**Kabul ölçütü:** "" 0 ""→0, ""100""→100, ""75""→75; """", ""elli"", ""1.5"", ""-1"", ""101"" reddedilir.

**Kontrol:** `-k gorev_2`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 11, EgitimId = 2, No = 3, Baslik = @"Temel — Geçti mi?", Aciklama = @"`durum(notu)` geçerli tamsayı not için 50 ve üzerindeyse ""gecti"", değilse ""kaldi"" döndürsün.

**Kabul ölçütü:** 0/49 kaldi; 50/100 gecti.

**Kontrol:** `-k gorev_3`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 12, EgitimId = 2, No = 4, Baslik = @"Orta — Geçerli notları ayır", Aciklama = @"`ayir(metinler)` sıralı str listesinden `(notlar, hatali_indeksler)` döndürsün; indeksler 0 tabanlı.

**Kabul ölçütü:** [""10"",""x"",""101"",""0""]→([10,0],[1,2]); boş liste→([],[]); giriş değişmez.

**Kontrol:** `-k gorev_4`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 13, EgitimId = 2, No = 5, Baslik = @"Orta — Döngüyü bitir", Aciklama = @"`menu(metinler)` ilk strip sonrası `q` girdisine kadar geçerli notları toplayıp döndürsün; bozuk notları atla. q büyük/küçük harf duyarlıdır.

**Kabul ölçütü:** [""2"",""x"",""3"","" q "",""100""]→5; ilk q→0; q yoksa sonuna kadar işler.

**Kontrol:** `-k gorev_5`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 14, EgitimId = 2, No = 6, Baslik = @"Orta · hata ayıklama — Dahil sınırlar", Aciklama = @"`sinir_duzelt(metin)` içindeki varsayılan `0 < n < 100` kusurunu düzelt; not_oku ile aynı sözleşmeye getir.

**Kabul ölçütü:** ""0"" ve ""100"" kabul; ""-1"" ve ""101"" ret. Hangi iki örneğin yanlış sürümü yakaladığını yaz.

**Kontrol:** `-k gorev_6`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 15, EgitimId = 2, No = 7, Baslik = @"Zor — Sayaçların tutarlılığı", Aciklama = @"`ozet(metinler)` toplam geçerli sayısı, geçen ve kalan sayısı ile hatalı indeksleri sözlükte döndürsün: gecerli, gecen, kalan, hatalar.

**Kabul ölçütü:** [""0"",""50"",""100"",""x""]→{gecerli:3,gecen:2,kalan:1,hatalar:[3]}; geçerli=geçen+kalan.

**Kontrol:** `-k gorev_7`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 16, EgitimId = 2, No = 8, Baslik = @"Zor · açık uçlu mini görev — Not takip çekirdeği", Aciklama = @"`not_defteri(metinler)` ozet alanlarına `en_yuksek` eklesin; geçerli veri yoksa None olsun. Veriyi terminalden, dosyadan veya elle listeden alma seçeneklerinden birini seçip hata sunumunu tasarla.

**Kabul ölçütü:** Boş ve tümü bozuk veri en_yuksek=None; [""40"",""90"",""x""] için 90 ve hatalar=[2]. Tasarımda geçersiz veri ile sıfır not ayrılır.

**Kontrol:** `-k gorev_8`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

[Gerekçeli çözümler](cozumler/CEVAPLAR.md). Mini görevde tasarımını, bir normal ve bir hata/sınır örneğini yaz; test raporuyla birlikte sakla." },
            new Gorev { Id = 17, EgitimId = 3, No = 1, Baslik = @"Temel — Nesne oklarını çiz", Aciklama = @"`a=[[1]]; b=a; c=a.copy(); c[0].append(2)` sonrası a,b,c değerlerini ve `a is b`, `a is c`, `a[0] is c[0]` sonuçlarını yaz.

**Kabul ölçütü:** Üç değer de `[[1,2]]`; kimlikler True,False,True; dış ve iç nesne ayrılır.

**Kontrol:** Cevap anahtarıyla elle karşılaştır; ölçütlerin her birini işaretle." },
            new Gorev { Id = 18, EgitimId = 3, No = 2, Baslik = @"Temel — Sıralı tekilleştirme", Aciklama = @"`tekil(kelimeler)` metin listesini ilk görülme sırasını koruyarak tekilleştirsin. Büyük/küçük harf burada ayrı kabul edilir.

**Kabul ölçütü:** [""b"",""a"",""b"",""B""]→[""b"",""a"",""B""]; boş liste→[]; girdi korunur.

**Kontrol:** `-k gorev_2`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 19, EgitimId = 3, No = 3, Baslik = @"Temel — Frekans", Aciklama = @"`frekans(kelimeler)` casefold uygulanmış kelimelerin sayılarını dict olarak döndürsün; kelimeleri ayrıca strip etme.

**Kabul ölçütü:** [""A"",""a"",""B"",""Straße"",""STRASSE""]→{""a"":2,""b"":1,""strasse"":2}; boş veri→{}.

**Kontrol:** `-k gorev_3`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 20, EgitimId = 3, No = 4, Baslik = @"Orta — Bağımsız kullanıcılar", Aciklama = @"`kullanicilar(adlar)` ile her ad için {ad:...,etiketler:[]} kaydı üret; her kaydın listesi ayrı olsun.

**Kabul ölçütü:** İki kayıttan birine etiket eklemek diğerini etkilemez; ad sırası ve tekrarlar korunur.

**Kontrol:** `-k gorev_4`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 21, EgitimId = 3, No = 5, Baslik = @"Orta — Etiketli kopya", Aciklama = @"`etiket_ekle(kayit,etiket)` yalnız ad ve etiketler alanı olan kaydın yeni sürümünü döndürsün. Etiket metindir; özgün etiket listesi değişmesin.

**Kabul ölçütü:** {ad:""Ada"",etiketler:[""py""]} ve ""sql""→yeni [""py"",""sql""]; özgün [""py""].

**Kontrol:** `-k gorev_5`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 22, EgitimId = 3, No = 6, Baslik = @"Orta · hata ayıklama — Paylaşılan satırlar", Aciklama = @"Hatalı `tablo(satir,sutun): return [[0]*sutun]*satir` kodunu düzelt. Negatif boyutlarda ValueError ver.

**Kabul ölçütü:** tablo(2,2) satırları bağımsız; (0,3)→[]; (2,0)→[[],[]]; negatif ret.

**Kontrol:** `-k gorev_6`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 23, EgitimId = 3, No = 7, Baslik = @"Zor — Sayımları birleştir", Aciklama = @"`birlestir(a,b)` metin anahtarlı, negatif olmayan tamsayı sayımları yeni dict içinde toplasın; kaynak sözlükler değişmesin.

**Kabul ölçütü:** {x:2,y:1}+{x:3,z:4}→{x:5,y:1,z:4}; eksik anahtar 0; boşlar desteklenir.

**Kontrol:** `-k gorev_7`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla." },
            new Gorev { Id = 24, EgitimId = 3, No = 8, Baslik = @"Zor · açık uçlu mini görev — Küçük metin raporu", Aciklama = @"`metin_raporu(kelimeler)` {toplam,tekil,frekans} döndürsün; tekil sayısı casefold sonrası hesaplanır. Raporu alfabetik ya da sıklık sırasıyla göstermek için bir sunum tercihi yapıp gerekçelendir.

**Kabul ölçütü:** [""A"",""a"",""B""]→{toplam:3,tekil:2,frekans:{a:2,b:1}}; boş veri için sıfırlar/{}. Sunum sırası sayım sonucunu değiştirmez.

**Kontrol:** `-k gorev_8`; ayrıca aşağıdaki gerekçeyi kendi cümlelerinle açıkla.

[Gerekçeli çözümler](cozumler/CEVAPLAR.md). Mini görevde tasarımını, bir normal ve bir hata/sınır örneğini yaz; test raporuyla birlikte sakla." }
        );
    }
}
