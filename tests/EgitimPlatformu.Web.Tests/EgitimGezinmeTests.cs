using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public class EgitimGezinmeTests
{
    private static string Menu(string html) => Regex.Match(html, "<aside.*?</aside>", RegexOptions.Singleline).Value;
    private static string Pager(string html) => Regex.Match(html, "<nav class=\"lesson-pagination.*?</nav>", RegexOptions.Singleline).Value;

    [Theory]
    [InlineData("misafir")][InlineData("ogrenci")][InlineData("yonetici")]
    public async Task KategoriSirayaGoreAcilirMenuVeSinirlarKategoriyeOzeldir(string rol)
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        if (rol != "misafir")
        {
            await IdentityApplication.KayitAsync(c);
            if (rol == "yonetici")
            {
                using var s = app.Services.CreateScope(); var users = s.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                await users.AddToRoleAsync((await users.FindByEmailAsync("ogrenci@example.test"))!, Roller.Yonetici);
            }
            await IdentityApplication.GirisAsync(c);
        }
        int yeniId;
        using (var s = app.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            (await db.Egitimler.FindAsync(3))!.Sira = -1;
            (await db.Egitimler.FindAsync(1))!.Sira = 10;
            (await db.Egitimler.FindAsync(2))!.Sira = 10; // Eşit sırada kimlik bağlayıcısı.
            var diger = new Egitim { KategoriId = 2, Kod = "BASKA", Baslik = "Başka kategori dersi", Sira = -100 };
            db.Egitimler.Add(diger); await db.SaveChangesAsync(); yeniId = diger.Id;
        }
        var redirect = await c.GetAsync("/Egitimler?KategoriId=1");
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Equal("/Egitimler/Detay/3", redirect.Headers.Location!.OriginalString);
        var first = await c.GetStringAsync("/Egitimler/Detay/3");
        var menu = Menu(first);
        Assert.DoesNotContain("Başka kategori dersi", WebUtility.HtmlDecode(menu));
        var links = Regex.Matches(menu, "href=\"/Egitimler/Detay/(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).ToArray();
        Assert.Equal(new[] { 3, 1, 2 }, links);
        Assert.Single(Regex.Matches(menu, "aria-current=\"page\""));
        Assert.Contains("href=\"/Egitimler/Detay/3\"", Regex.Match(menu, "<a[^>]*aria-current=\"page\"[^>]*>").Value);
        foreach (var anchor in new[] { "ders-icerigi", "kod-editoru", "gorevler" })
        {
            Assert.Single(Regex.Matches(menu, $"href=\"#{anchor}\"")); Assert.Contains($"id=\"{anchor}\"", first);
        }
        var decoded = WebUtility.HtmlDecode(menu);
        Assert.DoesNotContain("Açık ders", decoded);
        Assert.Contains("PY-01: İlk Python programı</a>", decoded);
        var title = decoded.IndexOf("id=\"curriculum-title\"", StringComparison.Ordinal);
        var subtitle = decoded.IndexOf("Eğitim içeriği", StringComparison.Ordinal);
        var shortcuts = decoded.IndexOf("class=\"curriculum-anchors\"", StringComparison.Ordinal);
        var list = decoded.IndexOf("class=\"curriculum-list\"", StringComparison.Ordinal);
        Assert.True(title < subtitle && subtitle < shortcuts && shortcuts < list);
        Assert.DoesNotContain("curriculum-anchors", decoded[list..]);
        Assert.DoesNotContain("rel=\"prev\"", Pager(first)); Assert.Contains("rel=\"next\"", Pager(first));
        var middle = Pager(await c.GetStringAsync("/Egitimler/Detay/1"));
        Assert.Contains("href=\"/Egitimler/Detay/3\"", middle); Assert.Contains("href=\"/Egitimler/Detay/2\"", middle);
        var last = Pager(await c.GetStringAsync("/Egitimler/Detay/2"));
        Assert.Contains("rel=\"prev\"", last); Assert.DoesNotContain("rel=\"next\"", last);
        var single = await c.GetStringAsync($"/Egitimler/Detay/{yeniId}");
        Assert.DoesNotContain("rel=\"prev\"", Pager(single)); Assert.DoesNotContain("rel=\"next\"", Pager(single));
        Assert.DoesNotContain("/Egitimler/Detay/1\"", Menu(single));
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Gonderimler.ToListAsync());
    }

    [Fact]
    public async Task KategorilerVeridenGelirBosVeGecersizKategoriEleAlinir()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        int kategoriId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            var yeni = new Kategori { Ad = "Yeni eğitim alanı", Kisaltma = "YENI", Aciklama = "Veritabanından gelen açıklama", Sira = -1 };
            db.Kategoriler.Add(yeni); await db.SaveChangesAsync(); kategoriId = yeni.Id;
        }
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/Egitimler"));
        Assert.Contains("Yeni eğitim alanı", html); Assert.Contains("Veritabanından gelen açıklama", html);
        Assert.DoesNotContain("class=\"course-card course-python", html);
        var empty = await c.GetAsync($"/Egitimler?KategoriId={kategoriId}");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Contains("henüz erişilebilir ders yok", WebUtility.HtmlDecode(await empty.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Egitimler?KategoriId=99999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/Egitimler?KategoriId=-1")).StatusCode);
    }

    private sealed class Runner : IGorevKodServisi
    {
        public int Cagri;
        public Task<GorevKodSonucu> CalistirAsync(Gorev g, string kod, bool test, CancellationToken ct)
        { Cagri++; return Task.FromResult(new GorevKodSonucu(g.No, true, "tamamlandi", "", "", 0, null, null, [])); }
    }

    [Fact]
    public async Task MetinlerMisafireAcikKalirKodCalistirmaDogrudanUrlIleAsilamaz()
    {
        var runner = new Runner(); using var app = new IdentityApplication(gorevRunner: runner); using var c = await app.IstemciAsync();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            db.Gorevler.Add(new Gorev { EgitimId = 2, No = 99, Baslik = "Test görevi", Tur = "kod", Aktif = true,
                BaslangicDosyasi = "gorevler/test.py", KontrolJson = "{}" });
            await db.SaveChangesAsync();
        }
        var html = await c.GetStringAsync("/Egitimler/Detay/2");
        using var config = JsonDocument.Parse(Regex.Match(html, "<script id=\"code-editor-config\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value);
        Assert.False(config.RootElement.GetProperty("izinli").GetBoolean());
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        async Task<HttpResponseMessage> Gonder()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/Egitimler/Detay/2?handler=Calistir&no=99") { Content = JsonContent.Create(new { kod = "pass" }) };
            request.Headers.Add("X-CSRF-TOKEN", token); return await c.SendAsync(request);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Gonder()).StatusCode); Assert.Equal(0, runner.Cagri);
        await IdentityApplication.KayitAsync(c); await IdentityApplication.GirisAsync(c);
        html = await c.GetStringAsync("/Egitimler/Detay/2");
        token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.Equal(HttpStatusCode.OK, (await Gonder()).StatusCode); Assert.Equal(1, runner.Cagri);
    }

    [Fact]
    public async Task UzunMenuButunDersleriGosterirVeBasliklariHtmlOlarakCalistirmaz()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync(); int id;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            var ders = new Egitim { KategoriId = 2, Kod = "DEN-SON", Baslik = "<script>deneme</script>", Sira = 100 };
            db.Egitimler.Add(ders);
            for (int n = 0; n < 40; n++) db.Egitimler.Add(new Egitim { KategoriId = 2, Kod = $"DEN-{n}", Baslik = $"Uzun liste {n}", Sira = n });
            await db.SaveChangesAsync(); id = ders.Id;
        }
        var html = await c.GetStringAsync($"/Egitimler/Detay/{id}");
        var menu = Menu(html);
        Assert.Equal(41, Regex.Matches(menu, "href=\"/Egitimler/Detay/").Count);
        Assert.Contains("&lt;script&gt;deneme&lt;/script&gt;", menu); Assert.DoesNotContain("<script>deneme</script>", menu);
        Assert.Contains("curriculum-body", menu); Assert.Contains("data-bs-toggle=\"collapse\"", menu);
        Assert.DoesNotContain("rel=\"next\"", Pager(html));
    }
}
