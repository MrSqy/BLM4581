using EgitimPlatformu.Web.Services;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public sealed class KodCalistirmaServisiTests
{
    [Fact]
    public async Task KodVeTestDosyalariniSandboxaVeripCiktilariDondurur()
    {
        var sandbox = new KaydedenSandboxIslemcisi(new KodCalistirmaSonucu(0, "2 passed", "uyari"));
        var servis = new PythonKodCalistirmaServisi(sandbox);

        var sonuc = await servis.CalistirAsync("def topla(a, b): return a + b", "from calisma import topla\n\ndef test_topla(): assert topla(2, 3) == 5");

        Assert.Equal("def topla(a, b): return a + b", sandbox.Kod);
        Assert.Contains("test_topla", sandbox.TestKodu);
        Assert.Equal(5, sandbox.Istek!.SureSiniriSaniye);
        Assert.False(Directory.Exists(sandbox.Istek.CalismaDizini));
        Assert.True(sonuc.Basarili);
        Assert.False(sonuc.ZamanAsimi);
        Assert.False(sonuc.AltyapiHatasi);
        Assert.Equal("2 passed", sonuc.StandartCikti);
        Assert.Equal("uyari", sonuc.StandartHata);
    }

    [Theory]
    [InlineData("", "def test_ornek(): pass", "kod")]
    [InlineData("print('merhaba')", "  ", "testKodu")]
    public async Task BosKodVeyaTestSandboxBaslatmadanReddedilir(string kod, string testKodu, string parametre)
    {
        var sandbox = new KaydedenSandboxIslemcisi(new KodCalistirmaSonucu(0, "", ""));
        var servis = new PythonKodCalistirmaServisi(sandbox);

        var hata = await Assert.ThrowsAsync<ArgumentException>(() => servis.CalistirAsync(kod, testKodu));

        Assert.Equal(parametre, hata.ParamName);
        Assert.Null(sandbox.Istek);
    }

    [Fact]
    public async Task SandboxHataVerseBileGeciciDosyalarSilinir()
    {
        var sandbox = new KaydedenSandboxIslemcisi(new InvalidOperationException("Docker kullanılamıyor."));
        var servis = new PythonKodCalistirmaServisi(sandbox);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.CalistirAsync("print('x')", "def test_x(): pass"));

        Assert.NotNull(sandbox.Istek);
        Assert.False(Directory.Exists(sandbox.Istek!.CalismaDizini));
    }

    [Theory]
    [InlineData(0, true, false, false)]
    [InlineData(1, false, false, false)]
    [InlineData(124, false, true, false)]
    [InlineData(125, false, false, true)]
    public void CikisKoduDurumBayraklarinaDonusur(int cikisKodu, bool basarili, bool zamanAsimi, bool altyapiHatasi)
    {
        var sonuc = new KodCalistirmaSonucu(cikisKodu, "", "");

        Assert.Equal(basarili, sonuc.Basarili);
        Assert.Equal(zamanAsimi, sonuc.ZamanAsimi);
        Assert.Equal(altyapiHatasi, sonuc.AltyapiHatasi);
    }

    [Fact]
    public async Task BashCalistiriciGuvenliHaftaBirProfiliniKullanir()
    {
        var hariciIslem = new KaydedenHariciIslemCalistirici();
        var calistirici = new BashSandboxIslemCalistirici("/proje", hariciIslem);

        var sonuc = await calistirici.CalistirAsync(new SandboxCalistirmaIstegi("/tmp/calisma", 7));

        Assert.Equal("setsid", hariciIslem.DosyaAdi);
        Assert.Equal([
            "bash",
            "/proje/scripts/run-python-sandbox.sh",
            "--timeout", "7",
            "--volume", "/tmp/calisma:/calisma:ro",
            "--workdir", "/calisma",
            "--", "python", "-m", "pytest", "-q"
        ], hariciIslem.Argumanlar);
        Assert.Equal(0, sonuc.CikisKodu);
    }

    [Fact]
    public async Task HariciIslemCiktiVeHataAkislariniDondurur()
    {
        var calistirici = new HariciIslemCalistirici();

        var sonuc = await calistirici.CalistirAsync("bash",
            ["-c", "printf cikti; printf hata >&2; exit 7"]);

        Assert.Equal(7, sonuc.CikisKodu);
        Assert.Equal("cikti", sonuc.StandartCikti);
        Assert.Equal("hata", sonuc.StandartHata);
    }

    [Fact]
    public async Task HariciIslemCiktiSiniriniAsincaSureciDurdurur()
    {
        var calistirici = new HariciIslemCalistirici(ciktiSiniri: 128);

        var sonuc = await calistirici.CalistirAsync("setsid",
            ["bash", "-c", "head -c 1024 /dev/zero | tr '\\0' x"]);

        Assert.True(sonuc.AltyapiHatasi);
        Assert.True(sonuc.StandartCikti.Length <= 128);
        Assert.Contains("çıktı sınırını aştı", sonuc.StandartHata);
    }

    [Fact]
    public async Task HariciIslemIptalEdilinceSurecGrubunuDurdurur()
    {
        var calistirici = new HariciIslemCalistirici();
        using var iptal = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            calistirici.CalistirAsync("setsid", ["bash", "-c", "sleep 30"], iptal.Token));
    }

    private sealed class KaydedenSandboxIslemcisi : ISandboxIslemCalistirici
    {
        private readonly KodCalistirmaSonucu? sonuc;
        private readonly Exception? hata;

        public KaydedenSandboxIslemcisi(KodCalistirmaSonucu sonuc) => this.sonuc = sonuc;
        public KaydedenSandboxIslemcisi(Exception hata) => this.hata = hata;

        public SandboxCalistirmaIstegi? Istek { get; private set; }
        public string Kod { get; private set; } = "";
        public string TestKodu { get; private set; } = "";

        public async Task<KodCalistirmaSonucu> CalistirAsync(SandboxCalistirmaIstegi istek, CancellationToken cancellationToken = default)
        {
            Istek = istek;
            Kod = await File.ReadAllTextAsync(Path.Combine(istek.CalismaDizini, "calisma.py"), cancellationToken);
            TestKodu = await File.ReadAllTextAsync(Path.Combine(istek.CalismaDizini, "test_alistirmalar.py"), cancellationToken);
            if (hata is not null) throw hata;
            return sonuc!;
        }
    }

    private sealed class KaydedenHariciIslemCalistirici : IHariciIslemCalistirici
    {
        public string DosyaAdi { get; private set; } = "";
        public IReadOnlyList<string> Argumanlar { get; private set; } = [];

        public Task<KodCalistirmaSonucu> CalistirAsync(string dosyaAdi, IReadOnlyList<string> argumanlar,
            CancellationToken cancellationToken = default)
        {
            DosyaAdi = dosyaAdi;
            Argumanlar = argumanlar;
            return Task.FromResult(new KodCalistirmaSonucu(0, "", ""));
        }
    }

}
