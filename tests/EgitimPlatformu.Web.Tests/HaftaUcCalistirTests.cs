using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

// 3.4: pytest çıktısının görev bazında ayrıştırılması ve /api/calistir ucu.
public sealed class HaftaUcCalistirTests
{
    private const string KarisikCikti = """
        ============================= test session starts ==============================
        collected 7 items

        test_alistirmalar.py::test_gorev_2[ 0 -0] PASSED                         [ 14%]
        test_alistirmalar.py::test_gorev_2_hata[elli] FAILED                     [ 28%]
        test_alistirmalar.py::test_gorev_3[0-kaldi] PASSED                       [ 42%]
        test_alistirmalar.py::test_gorev_4 SKIPPED (henüz değil)                 [ 57%]
        test_alistirmalar.py::test_gorev_5 ERROR                                 [ 71%]
        test_alistirmalar.py::test_gorev_12 PASSED                               [ 85%]
        test_alistirmalar.py::test_yardimci PASSED                               [100%]

        =========================== short test summary info ============================
        FAILED test_alistirmalar.py::test_gorev_3 - assert 1 == 2
        """;

    [Fact]
    public void GorevlerTestAdindakiNumarayaGoreToplanir()
    {
        var yanit = PytestGorevAyristirici.Ayristir(new KodCalistirmaSonucu(1, KarisikCikti, ""), [1, 2, 3, 4, 5, 12]);

        Assert.False(yanit.Basarili);
        Assert.Equal("basarisiz", yanit.Durum);
        Assert.Equal([
            new GorevDurumu(1, "test_yok"),
            new GorevDurumu(2, "basarisiz"),  // aynı görevin bir testi kalırsa görev kalır
            new GorevDurumu(3, "basarili"),   // özet satırı ("FAILED ...") sonucu değiştirmez
            new GorevDurumu(4, "atlanmis"),
            new GorevDurumu(5, "basarisiz"),
            new GorevDurumu(12, "basarili")   // test_gorev_1 ile karışmaz
        ], yanit.Gorevler);
    }

    [Theory]
    [InlineData(0, true, "tamamlandi")]
    [InlineData(1, false, "basarisiz")]
    [InlineData(2, false, "hata")]
    [InlineData(5, false, "test_yok")]
    [InlineData(124, false, "zaman_asimi")]
    public void CikisKoduGenelDurumaDonusur(int cikisKodu, bool basarili, string durum)
    {
        var yanit = PytestGorevAyristirici.Ayristir(new KodCalistirmaSonucu(cikisKodu, "", "hata metni"), [1]);

        Assert.Equal(basarili, yanit.Basarili);
        Assert.Equal(durum, yanit.Durum);
        Assert.Equal("hata metni", yanit.Hata);
        Assert.Equal([new GorevDurumu(1, "test_yok")], yanit.Gorevler);
    }

    [Fact]
    public async Task ServisDersTestleriIcinCozumFixtureiniYazar()
    {
        var sandbox = new ConftestOkuyanSandbox();
        await new PythonKodCalistirmaServisi(sandbox).CalistirAsync("x = 1", "def test_gorev_1(cozum): pass");

        Assert.Contains("def cozum():", sandbox.Conftest);
        Assert.Contains("importlib.import_module(\"calisma\")", sandbox.Conftest);
    }

    [Fact]
    public async Task CalistirUcuGorevSonuclariniDondururVeTestKodunuSunucudanAlir()
    {
        var servis = new SahteKodServisi(new KodCalistirmaSonucu(1, KarisikCikti, ""));
        using var app = Uygulama(servis);
        using var c = await IstemciAsync(app, girisYap: true);

        var yanit = await c.PostAsJsonAsync("/api/calistir", new { egitimId = 2, kod = "def not_oku(m): ..." });

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var json = await yanit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("basarisiz", json.GetProperty("durum").GetString());
        var gorevler = json.GetProperty("gorevler").EnumerateArray()
            .ToDictionary(g => g.GetProperty("no").GetInt32(), g => g.GetProperty("durum").GetString());
        Assert.Equal("basarisiz", gorevler[2]);
        Assert.Equal("basarili", gorevler[3]);
        Assert.Equal("test_yok", gorevler[1]);
        Assert.Equal("def not_oku(m): ...", servis.Kod);
        Assert.Contains("test_gorev_2", servis.TestKodu); // test kodu istemciden değil veritabanından gelir
    }

    [Fact]
    public async Task CalistirUcuErisimVeDersKurallariniUygular()
    {
        var servis = new SahteKodServisi(new KodCalistirmaSonucu(0, "", ""));
        using var app = Uygulama(servis);
        using var misafir = await IstemciAsync(app, girisYap: false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await misafir.PostAsJsonAsync("/api/calistir", new { egitimId = 2, kod = "pass" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await misafir.PostAsJsonAsync("/api/calistir", new { egitimId = 1, kod = "pass" })).StatusCode); // PY-01 görev bazlı
        Assert.Equal(HttpStatusCode.NotFound, (await misafir.PostAsJsonAsync("/api/calistir", new { egitimId = 999, kod = "pass" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await misafir.PostAsJsonAsync("/api/calistir", new { egitimId = 2, kod = "" })).StatusCode);
        Assert.Equal(0, servis.Cagri);
    }

    [Fact]
    public async Task SandboxYoksa503ZamanAsimindaSonucDoner()
    {
        var servis = new SahteKodServisi(new KodCalistirmaSonucu(125, "", "docker yok"));
        using var app = Uygulama(servis);
        using var c = await IstemciAsync(app, girisYap: true);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.PostAsJsonAsync("/api/calistir", new { egitimId = 2, kod = "pass" })).StatusCode);

        servis.Sonuc = new KodCalistirmaSonucu(124, "", "");
        var yanit = await c.PostAsJsonAsync("/api/calistir", new { egitimId = 2, kod = "while True: pass" });
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var json = await yanit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("basarili").GetBoolean());
        Assert.Equal("zaman_asimi", json.GetProperty("durum").GetString());

        servis.Hata = new InvalidOperationException("başlatılamadı");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.PostAsJsonAsync("/api/calistir", new { egitimId = 2, kod = "pass" })).StatusCode);
    }

    private static WebApplicationFactory<Program> Uygulama(IKodCalistirmaServisi servis) =>
        new IdentityApplication().WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IKodCalistirmaServisi>();
            s.AddSingleton(servis);
        }));

    private static async Task<HttpClient> IstemciAsync(WebApplicationFactory<Program> app, bool girisYap)
    {
        var c = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
        });
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Database.EnsureCreatedAsync();
            var roller = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var rol in new[] { Roller.Ogrenci, Roller.Yonetici })
                if (!await roller.RoleExistsAsync(rol)) await roller.CreateAsync(new IdentityRole(rol));
        }
        if (!girisYap) return c;
        var kayit = await c.PostAsJsonAsync("/api/mobil/hesap/kayit",
            new { adSoyad = "Çalıştır Test", eposta = "calistir@example.test", sifre = "Guvenli123!" });
        Assert.Equal(HttpStatusCode.OK, kayit.StatusCode);
        var token = (await kayit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        c.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return c;
    }

    private sealed class SahteKodServisi(KodCalistirmaSonucu sonuc) : IKodCalistirmaServisi
    {
        public KodCalistirmaSonucu Sonuc { get; set; } = sonuc;
        public Exception? Hata { get; set; }
        public int Cagri { get; private set; }
        public string Kod { get; private set; } = "";
        public string TestKodu { get; private set; } = "";

        public Task<KodCalistirmaSonucu> CalistirAsync(string kod, string testKodu, CancellationToken cancellationToken = default)
        {
            Cagri++;
            Kod = kod;
            TestKodu = testKodu;
            return Hata is null ? Task.FromResult(Sonuc) : Task.FromException<KodCalistirmaSonucu>(Hata);
        }
    }

    private sealed class ConftestOkuyanSandbox : ISandboxIslemCalistirici
    {
        public string Conftest { get; private set; } = "";

        public async Task<KodCalistirmaSonucu> CalistirAsync(SandboxCalistirmaIstegi istek, CancellationToken cancellationToken = default)
        {
            Conftest = await File.ReadAllTextAsync(Path.Combine(istek.CalismaDizini, "conftest.py"), cancellationToken);
            return new KodCalistirmaSonucu(0, "", "");
        }
    }
}
