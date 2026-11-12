using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public sealed class GonderimEntegrasyonTests
{
    private sealed class Runner : IGorevKodServisi, IMobilKodServisi
    {
        public Task<GorevKodSonucu> CalistirAsync(Gorev gorev, string kod, bool test, CancellationToken ct)
        {
            if (kod == "altyapi") throw new InvalidOperationException("Test altyapı hatası");
            var basarili = !test || kod == "print('doğru')";
            var durum = !test ? "tamamlandi" : basarili ? "basarili" : "basarisiz";
            return Task.FromResult(new GorevKodSonucu(gorev.No, basarili, durum,
                basarili ? "doğru\n" : "  yanlış\n", basarili ? "" : "hata\n", 0,
                test ? basarili : null, test ? true : null, []));
        }

        public int DersCalistirmaSayisi { get; private set; }
        public Task<MobilKodSonucu> CalistirAsync(string kod, string testKodu, bool test, int[] gorevler, CancellationToken ct)
        {
            DersCalistirmaSayisi++;
            var testYok = test && kod == "test-yok";
            return Task.FromResult(new MobilKodSonucu(!testYok, testYok ? "test_yok" : "tamamlandi", "çıktı\n", "", []));
        }
    }

    private static async Task JwtAl(HttpClient c, string eposta)
    {
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.KayitAsync(c, eposta)).StatusCode);
        var login = await c.PostAsJsonAsync("/api/hesap/giris", new { eposta, sifre = "Guvenli123!" });
        login.EnsureSuccessStatusCode();
        var yanit = await login.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new("Bearer", yanit.GetProperty("accessToken").GetString());
    }

    [Theory]
    [InlineData("print('yanlış')", false, "basarisiz", "  yanlış\n", "hata\n")]
    [InlineData("print('doğru')", true, "basarili", "doğru\n", "")]
    public async Task MobilTestSonucuKendiHesabinaKaydedilirVeBaskaHesabaGizlenir(
        string kod, bool basarili, string durum, string cikti, string hata)
    {
        using var app = new IdentityApplication(gorevRunner: new Runner());
        using var a = await app.IstemciAsync();
        using var b = await app.IstemciAsync();
        await JwtAl(a, "bir@example.test");
        await JwtAl(b, "iki@example.test");

        var test = await a.PostAsJsonAsync("/api/mobil/egitimler/1/gorevler/1/test",
            new { kod, kullaniciId = "sahte", basarili = !basarili });
        test.EnsureSuccessStatusCode();
        var yanit = (await test.Content.ReadFromJsonAsync<GorevKodSonucu>())!;
        Assert.NotNull(yanit.GonderimId);
        Assert.Equal(basarili, yanit.Basarili);

        var url = $"/api/gonderimler/{yanit.GonderimId}";
        var detay = (await a.GetFromJsonAsync<GonderimDetayYaniti>(url))!;
        Assert.Equal(kod, detay.Kod);
        Assert.Equal(cikti, detay.Cikti);
        Assert.Equal(hata, detay.Hata);
        Assert.Equal(basarili, detay.Gonderim.Basarili);
        Assert.Equal(durum, detay.Gonderim.Durum);
        Assert.Equal(1, detay.Gonderim.GorevNo);
        Assert.Equal(0, detay.Gonderim.CikisKodu);
        Assert.Equal(TimeSpan.Zero, detay.Gonderim.Tarih.Offset);
        Assert.True(detay.Gonderim.SureMs >= 0);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(url)).StatusCode);
        Assert.Empty((await b.GetFromJsonAsync<GonderimListeYaniti>("/api/gonderimler"))!.Kayitlar);
        a.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task MisafirCalistirVeReddedilenIsteklerGecmiseYazilmaz()
    {
        using var app = new IdentityApplication(gorevRunner: new Runner());
        using var c = await app.IstemciAsync();
        var guest = await c.PostAsJsonAsync("/api/mobil/egitimler/1/gorevler/1/test", new { kod = "pass" });
        guest.EnsureSuccessStatusCode();
        Assert.DoesNotContain("gonderimId", await guest.Content.ReadAsStringAsync());
        await JwtAl(c, "ogrenci@example.test");
        var run = await c.PostAsJsonAsync("/api/mobil/egitimler/1/gorevler/1/calistir", new { kod = "pass" });
        run.EnsureSuccessStatusCode();
        Assert.DoesNotContain("gonderimId", await run.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(
            "/api/mobil/egitimler/1/gorevler/6/test", new { kod = "pass" })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.PostAsJsonAsync(
            "/api/mobil/egitimler/1/gorevler/1/test", new { kod = "altyapi" })).StatusCode);
        Assert.Equal(0, (await c.GetFromJsonAsync<GonderimListeYaniti>("/api/gonderimler"))!.Toplam);
    }

    [Fact]
    public async Task EskiMobilDersTestiYoksaCalistirmadanReddedilirVeGecmiseYazilmaz()
    {
        var runner = new Runner();
        using var app = new IdentityApplication(runner: runner);
        using var c = await app.IstemciAsync();
        await JwtAl(c, "ogrenci@example.test");
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        var ders = await db.Egitimler.SingleAsync(e => e.Id == 2);
        ders.TestKodu = "";
        await db.SaveChangesAsync();

        const string yol = "/api/mobil/egitimler/2";
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(yol + "/test", new { kod = "pass" })).StatusCode);
        Assert.Equal(0, runner.DersCalistirmaSayisi);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync(yol + "/calistir", new { kod = "pass" })).StatusCode);
        Assert.Equal(1, runner.DersCalistirmaSayisi);

        ders.TestKodu = "# pytest işlevi bulunmayan dosya";
        await db.SaveChangesAsync();
        var cevap = await c.PostAsJsonAsync(yol + "/test", new { kod = "test-yok" });
        cevap.EnsureSuccessStatusCode();
        var sonuc = (await cevap.Content.ReadFromJsonAsync<MobilKodSonucu>())!;
        Assert.Equal("test_yok", sonuc.Durum);
        Assert.Null(sonuc.GonderimId);
        Assert.Equal(2, runner.DersCalistirmaSayisi);
        Assert.Equal(0, (await c.GetFromJsonAsync<GonderimListeYaniti>("/api/gonderimler"))!.Toplam);
    }

    [Fact]
    public async Task EksikGonderimTablosundaWebVeApiAyri503MesajlariVerir()
    {
        using var app = new IdentityApplication(gorevRunner: new Runner());
        using var c = await app.IstemciAsync();
        await JwtAl(c, "ogrenci@example.test");
        await IdentityApplication.GirisAsync(c);
        using (var scope = app.Services.CreateScope())
        {
            // Yalnız bu testin bellek içi SQLite veritabanında eski şemayı taklit et.
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            await db.Database.ExecuteSqlRawAsync("DROP TABLE Gonderimler");
        }
        foreach (var path in new[] { "/api/gonderimler", "/api/gonderimler/1" })
        {
            using var yanit = await c.GetAsync(path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
            var hata = await yanit.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("gonderim_gecmisi_kullanilamiyor", hata.GetProperty("durum").GetString());
        }

        using var test = await c.PostAsJsonAsync("/api/mobil/egitimler/1/gorevler/1/test", new { kod = "print('doğru')" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, test.StatusCode);
        var kayitHatasi = await test.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("gonderim_kayit_hatasi", kayitHatasi.GetProperty("durum").GetString());
        Assert.False(kayitHatasi.TryGetProperty("gonderimId", out _));

        using var liste = await c.GetAsync("/Gonderimler");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, liste.StatusCode);
        Assert.Contains("Gönderim geçmişine şu anda ulaşılamıyor.", WebUtility.HtmlDecode(await liste.Content.ReadAsStringAsync()));
        using var detay = await c.GetAsync("/Gonderimler/1");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, detay.StatusCode);
        Assert.Contains("Gönderim ayrıntısına şu anda ulaşılamıyor.", WebUtility.HtmlDecode(await detay.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData(1, "&no=1")]
    [InlineData(2, "")]
    public async Task RazorTestiKimlikliGecmiseYansir(int egitimId, string gorevQuery)
    {
        var runner = new Runner();
        using var app = new IdentityApplication(runner: runner, gorevRunner: runner);
        using var c = await app.IstemciAsync();
        await IdentityApplication.KayitAsync(c);
        await IdentityApplication.GirisAsync(c);
        var sayfa = $"/Egitimler/Detay/{egitimId}";
        var html = await c.GetStringAsync(sayfa);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        using var req = new HttpRequestMessage(HttpMethod.Post, sayfa + "?handler=Test" + gorevQuery)
        {
            Content = JsonContent.Create(new { kod = "<script>ornek</script>" })
        };
        req.Headers.Add("X-CSRF-TOKEN", WebUtility.HtmlDecode(token));
        using var test = await c.SendAsync(req);
        test.EnsureSuccessStatusCode();
        var id = (await test.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("gonderimId").GetInt32();
        Assert.Contains($"Gönderim #{id}", WebUtility.HtmlDecode(await c.GetStringAsync("/Gonderimler")));
        var detay = await c.GetStringAsync($"/Gonderimler/{id}");
        Assert.Contains("&lt;script&gt;ornek&lt;/script&gt;", detay);
        Assert.DoesNotContain("<script>ornek</script>", detay);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/gonderimler")).StatusCode);
    }

    [Fact]
    public async Task GorevUclariAktifGorevleriVeDersErisiminiKorur()
    {
        using var app = new IdentityApplication();
        using var c = await app.IstemciAsync();
        var gorevler = (await c.GetFromJsonAsync<GorevBilgisi[]>("/api/egitimler/1/gorevler"))!;
        Assert.Equal(7, gorevler.Length);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/egitimler/1/gorevler/8")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/egitimler/2/gorevler")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/egitimler/2/gorevler/1")).StatusCode);
        await JwtAl(c, "ogrenci@example.test");
        var gorev = (await c.GetFromJsonAsync<GorevBilgisi>("/api/egitimler/2/gorevler/1"))!;
        Assert.Equal(2, gorev.EgitimId);
        Assert.Equal(1, gorev.No);
    }

    [Fact]
    public async Task ListeSahibineGoreSayfalanirAyrintidaTarihVeKodKorunur()
    {
        using var app = new IdentityApplication();
        using var a = await app.IstemciAsync();
        using var b = await app.IstemciAsync();
        await JwtAl(a, "bir@example.test");
        await JwtAl(b, "iki@example.test");
        int sonId, oncekiId;
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            var sahibi = (await users.FindByEmailAsync("bir@example.test"))!.Id;
            var baskasi = (await users.FindByEmailAsync("iki@example.test"))!.Id;
            var tarih = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
            var onceki = new Gonderim { KullaniciId = sahibi, Ders = "Ders", TarihUtc = tarih, Kod = "önceki" };
            var son = new Gonderim { KullaniciId = sahibi, Ders = "Ders", TarihUtc = tarih, Kod = "son" };
            db.Gonderimler.AddRange(onceki, son,
                new Gonderim { KullaniciId = baskasi, Ders = "Başkasının dersi", TarihUtc = tarih.AddHours(1), Kod = "gizli" });
            await db.SaveChangesAsync();
            oncekiId = onceki.Id;
            sonId = son.Id;
        }
        const string liste = "/api/gonderimler?sayfaBoyutu=1";
        var ilk = (await a.GetFromJsonAsync<GonderimListeYaniti>(liste))!;
        Assert.Equal(2, ilk.Toplam);
        Assert.Equal(sonId, Assert.Single(ilk.Kayitlar).Id);
        var ikinci = (await a.GetFromJsonAsync<GonderimListeYaniti>(liste + "&sayfa=2"))!;
        Assert.Equal(oncekiId, Assert.Single(ikinci.Kayitlar).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync("/api/gonderimler?sayfaBoyutu=101")).StatusCode);
        Assert.Equal("son", (await a.GetFromJsonAsync<GonderimDetayYaniti>($"/api/gonderimler/{sonId}"))!.Kod);
    }
}
