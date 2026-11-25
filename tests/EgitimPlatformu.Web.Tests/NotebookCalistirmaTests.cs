using Xunit;
using EgitimPlatformu.Web.Services;

namespace EgitimPlatformu.Web.Tests;

public sealed class NotebookCalistirmaTests
{
    [Theory]
    [InlineData(124, "", "zaman_asimi")]
    [InlineData(125, "", "altyapi_yok")]
    [InlineData(0, "geçersiz", "altyapi_yok")]
    [InlineData(0, "{\"durum\":\"tamamlandi\",\"hucreler\":[{\"durum\":\"ok\",\"ciktilar\":[]}]}", "tamamlandi")]
    public async Task SandboxYanitiVeGeciciDosya(int kod, string stdout, string durum)
    {
        var islem = new SahteIslem(new(kod, stdout, ""));
        var servis = new NotebookCalistirmaServisi(islem);
        var sonuc = await servis.CalistirAsync(["print(42)"]);
        Assert.Equal(durum, sonuc.Durum);
        Assert.NotNull(islem.Dosya);
        Assert.False(File.Exists(islem.Dosya));
    }

    [NotebookDockerFact]
    public async Task GercekDockerAyniCekirdekHataVeZamanAsimi()
    {
        var servis = new NotebookCalistirmaServisi(
            new KuyrukluIslemCalistirici(new HariciIslemCalistirici(), new()));
        var sonuc = await servis.CalistirAsync(["x = 40", "print(x + 2)", "raise ValueError('deneme')"]);
        Assert.Equal("tamamlandi", sonuc.Durum);
        Assert.Equal("ok", sonuc.Hucreler[0].Durum);
        Assert.Equal("42", Assert.Single(sonuc.Hucreler[1].Ciktilar).GetProperty("text").GetString()!.Trim());
        Assert.Equal("error", sonuc.Hucreler[2].Durum);
        Assert.Equal("ValueError", Assert.Single(sonuc.Hucreler[2].Ciktilar).GetProperty("ename").GetString());

        var zamanAsimi = await servis.CalistirAsync(["import time; time.sleep(60)"]);
        Assert.Equal("zaman_asimi", zamanAsimi.Durum);
    }

    private sealed class NotebookDockerFactAttribute : FactAttribute
    {
        public NotebookDockerFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("NOTEBOOK_DOCKER_TEST") != "1")
                Skip = "Gerçek Docker testi için NOTEBOOK_DOCKER_TEST=1 tanımlayın.";
        }
    }

    private sealed class SahteIslem(KodCalistirmaSonucu sonuc) : IHariciIslemCalistirici
    {
        public string? Dosya { get; private set; }

        public Task<KodCalistirmaSonucu> CalistirAsync(string dosyaAdi, IReadOnlyList<string> argumanlar,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("setsid", dosyaAdi);
            Assert.Contains("--jupyter", argumanlar);
            Dosya = argumanlar.Single(a => a.EndsWith(":/istek.json:ro")).Split(':')[0];
            Assert.Contains("print(42)", File.ReadAllText(Dosya));
            return Task.FromResult(sonuc);
        }
    }
}
