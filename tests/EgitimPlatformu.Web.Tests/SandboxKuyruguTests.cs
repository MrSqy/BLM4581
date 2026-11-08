using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

// 4.4: Ortak sandbox kuyruğu, üst süre sınırı ve kod çalıştırma uçlarının 503 yanıtları.
public sealed class SandboxKuyruguTests
{
    private static readonly string[] Arguman = ["bash", "run-python-sandbox.sh"];

    [Fact]
    public async Task AyniAndaEnFazlaSinirKadarCalisirFazlasiSiradaBekler()
    {
        var ic = new KapiliIslem();
        var kuyruk = new KuyrukluIslemCalistirici(ic, new SandboxKuyruguAyarlari(EsZamanliCalisma: 2, BekleyenSiniri: 2));

        var istekler = Enumerable.Range(0, 4).Select(_ => kuyruk.CalistirAsync("setsid", Arguman)).ToArray();
        await BekleAsync(() => ic.Calisan == 2);

        Assert.Equal(2, kuyruk.Calisan);
        Assert.Equal(2, kuyruk.Bekleyen);

        ic.Kapi.SetResult();
        var sonuclar = await Task.WhenAll(istekler);

        Assert.All(sonuclar, s => Assert.True(s.Basarili));
        Assert.Equal(4, ic.Cagri);
        Assert.Equal(2, ic.EnFazlaEsZamanli);
        Assert.Equal(0, kuyruk.Calisan);
        Assert.Equal(0, kuyruk.Bekleyen);
    }

    [Fact]
    public async Task KuyrukDoluysaIstekBeklemedenReddedilir()
    {
        var ic = new KapiliIslem();
        var kuyruk = new KuyrukluIslemCalistirici(ic, new SandboxKuyruguAyarlari(EsZamanliCalisma: 1, BekleyenSiniri: 1));
        var calisan = kuyruk.CalistirAsync("setsid", Arguman);
        var bekleyen = kuyruk.CalistirAsync("setsid", Arguman);
        await BekleAsync(() => kuyruk.Bekleyen == 1);

        var hata = await Assert.ThrowsAsync<SandboxMesgulException>(() => kuyruk.CalistirAsync("setsid", Arguman));

        Assert.Contains("dolu", hata.Message);
        Assert.Equal(1, ic.Cagri);
        ic.Kapi.SetResult();
        await Task.WhenAll(calisan, bekleyen);
        Assert.Equal(2, ic.Cagri);
    }

    [Fact]
    public async Task SiraBeklemeSuresiDolarsaMesgulHatasiVerirVeSayaclarSifirlanir()
    {
        var ic = new KapiliIslem();
        var kuyruk = new KuyrukluIslemCalistirici(ic,
            new SandboxKuyruguAyarlari(EsZamanliCalisma: 1, BekleyenSiniri: 1, BeklemeSaniye: 1));
        var calisan = kuyruk.CalistirAsync("setsid", Arguman);
        await BekleAsync(() => ic.Calisan == 1);

        var hata = await Assert.ThrowsAsync<SandboxMesgulException>(() => kuyruk.CalistirAsync("setsid", Arguman));

        Assert.Contains("süre doldu", hata.Message);
        Assert.Equal(0, kuyruk.Bekleyen);
        ic.Kapi.SetResult();
        await calisan;
        Assert.Equal(0, kuyruk.Calisan);
    }

    [Fact]
    public async Task DockerTakilirsaUstSinirIslemiDurdururVeAltyapiHatasiDoner()
    {
        var ic = new KapiliIslem(); // Kapı hiç açılmaz: yanıt vermeyen Docker.
        var kuyruk = new KuyrukluIslemCalistirici(ic,
            new SandboxKuyruguAyarlari(EsZamanliCalisma: 1, IslemUstSiniriSaniye: 1));

        var sonuc = await kuyruk.CalistirAsync("setsid", Arguman);

        Assert.True(sonuc.AltyapiHatasi);
        Assert.Contains("1 saniye içinde yanıt vermedi", sonuc.StandartHata);
        Assert.True(ic.IptalGoruldu);
        Assert.Equal(0, kuyruk.Calisan);
    }

    [Fact]
    public async Task IstemciVazgecerseIptalGecerVeYuvaBosalir()
    {
        var ic = new KapiliIslem();
        var kuyruk = new KuyrukluIslemCalistirici(ic, new SandboxKuyruguAyarlari(EsZamanliCalisma: 1));
        using var iptal = new CancellationTokenSource();
        var istek = kuyruk.CalistirAsync("setsid", Arguman, iptal.Token);
        await BekleAsync(() => ic.Calisan == 1);

        await iptal.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => istek);
        Assert.Equal(0, kuyruk.Calisan);
        Assert.Equal(0, kuyruk.Bekleyen);
    }

    [Fact]
    public async Task IcIslemHataVerirseHataGecerVeYuvaBosalir()
    {
        var kuyruk = new KuyrukluIslemCalistirici(new HataliIslem(), new SandboxKuyruguAyarlari(EsZamanliCalisma: 1));

        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => kuyruk.CalistirAsync("setsid", Arguman));
        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => kuyruk.CalistirAsync("setsid", Arguman));

        Assert.Equal(0, kuyruk.Calisan);
    }

    [Fact]
    public void AyarlarYapilandirmadanOkunurGecersizDegerReddedilir()
    {
        var bos = new ConfigurationBuilder().Build();
        Assert.Equal(new SandboxKuyruguAyarlari(2, 8, 15, 30), SandboxKuyruguAyarlari.Oku(bos));

        var ozel = Yapilandirma(("Sandbox:EsZamanliCalisma", "4"), ("Sandbox:BeklemeSaniye", "3"));
        Assert.Equal(new SandboxKuyruguAyarlari(4, 8, 3, 30), SandboxKuyruguAyarlari.Oku(ozel));

        Assert.Throws<InvalidOperationException>(() =>
            SandboxKuyruguAyarlari.Oku(Yapilandirma(("Sandbox:EsZamanliCalisma", "0"))));
    }

    [Fact]
    public void UygulamaSandboxaKuyruktanGider()
    {
        using var app = new IdentityApplication();

        Assert.IsType<KuyrukluIslemCalistirici>(app.Services.GetRequiredService<IHariciIslemCalistirici>());
    }

    [Fact]
    public async Task KuyrukDoluysa503RetryAfterVeKuyrukDoluDurumuDoner()
    {
        var runner = new HataVerenGorevServisi(new SandboxMesgulException("Sandbox kuyruğu dolu."));
        using var app = new IdentityApplication(gorevRunner: runner);
        using var c = await app.IstemciAsync();

        var yanit = await c.PostAsJsonAsync("/api/mobil/egitimler/1/gorevler/1/test", new { kod = "print('x')" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(SandboxHataYaniti.YenidenDeneSaniye), yanit.Headers.RetryAfter?.Delta);
        var json = await yanit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("kuyruk_dolu", json.GetProperty("durum").GetString());
        Assert.Contains("yoğun", json.GetProperty("mesaj").GetString());
    }

    [Fact]
    public async Task SandboxYoksa503AltyapiYokDurumuDonerRetryAfterYok()
    {
        var runner = new HataVerenGorevServisi(new InvalidOperationException("Sandbox altyapısı yok."));
        using var app = new IdentityApplication(gorevRunner: runner);
        using var c = await app.IstemciAsync();

        var yanit = await c.PostAsJsonAsync("/api/mobil/egitimler/1/gorevler/1/calistir", new { kod = "print('x')" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
        Assert.Null(yanit.Headers.RetryAfter);
        var json = await yanit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("altyapi_yok", json.GetProperty("durum").GetString());
    }

    private static IConfiguration Yapilandirma(params (string Anahtar, string Deger)[] degerler) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(degerler.Select(d => new KeyValuePair<string, string?>(d.Anahtar, d.Deger)))
            .Build();

    private static async Task BekleAsync(Func<bool> kosul)
    {
        using var sinir = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!kosul()) await Task.Delay(10, sinir.Token);
    }

    // Kapı açılana kadar (ya da iptal gelene kadar) "çalışan" sahte sandbox işlemi.
    private sealed class KapiliIslem : IHariciIslemCalistirici
    {
        private int calisan;
        private int enFazla;
        private int cagri;

        public TaskCompletionSource Kapi { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calisan => Volatile.Read(ref calisan);
        public int EnFazlaEsZamanli => Volatile.Read(ref enFazla);
        public int Cagri => Volatile.Read(ref cagri);
        public bool IptalGoruldu { get; private set; }

        public async Task<KodCalistirmaSonucu> CalistirAsync(string dosyaAdi, IReadOnlyList<string> argumanlar,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref cagri);
            var simdi = Interlocked.Increment(ref calisan);
            int onceki;
            while (simdi > (onceki = Volatile.Read(ref enFazla)) &&
                   Interlocked.CompareExchange(ref enFazla, simdi, onceki) != onceki) { }
            try
            {
                await Kapi.Task.WaitAsync(cancellationToken);
                return new KodCalistirmaSonucu(0, "tamam", "");
            }
            catch (OperationCanceledException)
            {
                IptalGoruldu = true;
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref calisan);
            }
        }
    }

    private sealed class HataliIslem : IHariciIslemCalistirici
    {
        public Task<KodCalistirmaSonucu> CalistirAsync(string dosyaAdi, IReadOnlyList<string> argumanlar,
            CancellationToken cancellationToken = default) =>
            Task.FromException<KodCalistirmaSonucu>(new System.ComponentModel.Win32Exception("setsid bulunamadı"));
    }

    private sealed class HataVerenGorevServisi(Exception hata) : IGorevKodServisi
    {
        public Task<GorevKodSonucu> CalistirAsync(Gorev gorev, string kod, bool test, CancellationToken ct) =>
            Task.FromException<GorevKodSonucu>(hata);
    }
}
