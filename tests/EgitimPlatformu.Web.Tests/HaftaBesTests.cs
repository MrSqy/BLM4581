using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public class HaftaBesTests
{
    private static async Task<string> Hesap(IdentityApplication app, HttpClient client, string email, bool yonetici = false)
    {
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.KayitAsync(client, email)).StatusCode);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        if (yonetici) Assert.True((await users.AddToRoleAsync(user, Roller.Yonetici)).Succeeded);
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.GirisAsync(client, email)).StatusCode);
        return user.Id;
    }

    private static Task<HttpResponseMessage> Rol(HttpClient client, string id, string rol)
        => IdentityApplication.FormAsync(client, "/Yonetim/Kullanicilar", new()
        { ["Input.KullaniciId"] = id, ["Input.Rol"] = rol });

    private static Gonderim Gonderim(string user, string ders, int n, bool basarili = false, int egitim = 1)
        => new() { KullaniciId = user, Ders = ders, EgitimId = egitim, Kod = "print('deneme')", Basarili = basarili,
            Durum = basarili ? "basarili" : "basarisiz", TarihUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(n) };

    [Fact]
    public async Task PanoAnonimeKapaliBosVeYalnizKendiSonBesKaydiGosterir()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/Pano")).StatusCode);
        var id = await Hesap(app, c, "bir@example.test");
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/Pano"));
        Assert.Contains("Henüz gönderimin yok", html);
        Assert.Contains("İlk dersini tamamladığında ilerlemen burada görünecek", html);
        using var other = await app.IstemciAsync(); var otherId = await Hesap(app, other, "iki@example.test");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            for (var i = 1; i <= 7; i++) db.Gonderimler.Add(Gonderim(id, "Kendi dersim " + i, i));
            db.Gonderimler.Add(Gonderim(otherId, "GİZLİ BAŞKA ÖĞRENCİ", 100));
            await db.SaveChangesAsync();
        }
        html = WebUtility.HtmlDecode(await c.GetStringAsync("/Pano"));
        Assert.Contains("Toplam 7", html); Assert.Contains("Kendi dersim 7", html); Assert.Contains("Kendi dersim 3", html);
        Assert.DoesNotContain("Kendi dersim 2", html); Assert.DoesNotContain("GİZLİ", html);
        Assert.True(html.IndexOf("Kendi dersim 7", StringComparison.Ordinal) < html.IndexOf("Kendi dersim 3", StringComparison.Ordinal));
        Assert.DoesNotContain("role=\"progressbar\"", html); // Gönderim sayısı ilerleme sayılmaz.
    }

    private sealed class YanlisGecmis : IGonderimGecmisi
    {
        public bool Kullanilabilir => true;
        public Task<GonderimSayfasi> ListeAsync(string id, int sayfa, int boyut, CancellationToken ct)
            => Task.FromResult(new GonderimSayfasi(1, [new(1, "baskasi", DateTimeOffset.UtcNow, 1, "Gizli", null, "basarili", "", "", "")]));
        public Task<GonderimGorunumu?> DetayAsync(string id, int no, CancellationToken ct) => Task.FromResult<GonderimGorunumu?>(null);
    }

    [Fact]
    public async Task PanoYanlisSahipDondurenAdaptorunVerisiniGizler()
    {
        using var basis = new IdentityApplication();
        using var app = basis.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IGonderimGecmisi, YanlisGecmis>()));
        using var c = app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Database.EnsureCreatedAsync();
        await IdentityApplication.KayitAsync(c); await IdentityApplication.GirisAsync(c);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Pano")).StatusCode);
    }

    [Theory]
    [InlineData("/Yonetim/Kullanicilar")]
    [InlineData("/Yonetim/Istatistikler")]
    public async Task YonetimV2AnonimVeOgrenciyeKapali(string path)
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync(path)).StatusCode);
        await Hesap(app, c, "ogrenci@example.test");
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string,string>
        { ["Input.KullaniciId"] = "sahte", ["Input.Rol"] = Roller.Yonetici }))).StatusCode);
    }

    [Fact]
    public async Task RolAtamaCsrfGecersizRolEksikKullaniciVeSonYoneticiKontrolu()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        var admin = await Hesap(app, c, "admin@example.test", true);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/Yonetim/Kullanicilar", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["Input.KullaniciId"] = admin, ["Input.Rol"] = Roller.Ogrenci }))).StatusCode);
        var response = await Rol(c, admin, "SuperAdmin");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Geçerli bir rol seçin", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await Rol(c, "olmayan", Roller.Ogrenci)).StatusCode);
        response = await Rol(c, admin, Roller.Ogrenci);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Son yöneticinin yetkisi kaldırılamaz", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await users.IsInRoleAsync((await users.FindByIdAsync(admin))!, Roller.Yonetici));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/Yonetim/Kullanicilar?Sayfa=0")).StatusCode);
    }

    [Fact]
    public async Task RolDegisimiWebOturumuVeJwtIptalEderYenidenGirisYeniRoluKullanir()
    {
        using var app = new IdentityApplication(); using var admin = await app.IstemciAsync();
        await Hesap(app, admin, "admin@example.test", true);
        using var hedef = await app.IstemciAsync(); var id = await Hesap(app, hedef, "hedef@example.test");
        var login = await hedef.PostAsJsonAsync("/api/hesap/giris", new { eposta = "hedef@example.test", sifre = "Guvenli123!" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        using var api = await app.IstemciAsync(); api.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/gonderimler")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Rol(admin, id, Roller.Yonetici)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await hedef.GetAsync("/Pano")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/gonderimler")).StatusCode);
        await IdentityApplication.GirisAsync(hedef, "hedef@example.test");
        Assert.Equal(HttpStatusCode.OK, (await hedef.GetAsync("/Yonetim/Kullanicilar")).StatusCode);
        login = await hedef.PostAsJsonAsync("/api/hesap/giris", new { eposta = "hedef@example.test", sifre = "Guvenli123!" });
        token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        api.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.Redirect, (await Rol(admin, id, Roller.Ogrenci)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await hedef.GetAsync("/Yonetim/Kullanicilar")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/gonderimler")).StatusCode);
        await IdentityApplication.GirisAsync(hedef, "hedef@example.test");
        Assert.Equal(HttpStatusCode.Redirect, (await hedef.GetAsync("/Yonetim/Kullanicilar")).StatusCode);
        using var scope = app.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.Equal(new[] { Roller.Ogrenci }, await users.GetRolesAsync((await users.FindByIdAsync(id))!));
    }

    [Fact]
    public async Task KendiniDusurenYoneticiBaskaYoneticiVarsaKaydedilirVeOturumuKapanir()
    {
        using var app = new IdentityApplication(); using var a = await app.IstemciAsync();
        var id = await Hesap(app, a, "bir@example.test", true);
        using var b = await app.IstemciAsync(); await Hesap(app, b, "iki@example.test", true);
        Assert.Equal(HttpStatusCode.Redirect, (await Rol(a, id, Roller.Ogrenci)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await a.GetAsync("/Yonetim")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/Yonetim")).StatusCode);
    }

    [Fact]
    public async Task KullaniciListesiYirmiKayittaSayfalanir()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync(); await Hesap(app, c, "admin@example.test", true);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            for (int i = 0; i < 21; i++) Assert.True((await users.CreateAsync(new IdentityUser { UserName = $"u{i:00}@example.test", Email = $"u{i:00}@example.test" })).Succeeded);
        }
        var first = await c.GetStringAsync("/Yonetim/Kullanicilar");
        Assert.Contains("Sonraki", first); Assert.DoesNotContain("u20@example.test", first);
        Assert.Contains("u20@example.test", await c.GetStringAsync("/Yonetim/Kullanicilar?Sayfa=2"));
    }

    [Fact]
    public async Task IstatistiklerBosDurumuVeTekrarDenemeleriDogruSayar()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync(); var id = await Hesap(app, c, "admin@example.test", true);
        Assert.Contains("Henüz kayıtlı gönderim yok", WebUtility.HtmlDecode(await c.GetStringAsync("/Yonetim/Istatistikler")));
        using var other = await app.IstemciAsync(); var otherId = await Hesap(app, other, "ogrenci@example.test");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            db.Gonderimler.AddRange(Gonderim(id, "Eski başlık", 1, true), Gonderim(id, "Güncel başlık", 2),
                Gonderim(otherId, "Güncel başlık", 3, true), Gonderim(otherId, "İkinci ders", 4, false, 2));
            await db.SaveChangesAsync();
        }
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/Yonetim/Istatistikler"));
        Assert.Contains("id=\"istatistik-toplam\">4", html); Assert.Contains("id=\"istatistik-basarili\">2", html);
        Assert.Contains("id=\"istatistik-basarisiz\">2", html); Assert.Contains("id=\"istatistik-katilimci\">2", html);
        Assert.Contains("%50", html); Assert.Contains("Güncel başlık", html); Assert.DoesNotContain("Eski başlık", html);
        Assert.Contains("<td>3</td><td>2</td><td>1</td>", html);
        Assert.DoesNotContain("print('deneme')", html);
    }

    [Fact]
    public async Task EksikGonderimTablosundaPanoVeIstatistikler503Doner()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync(); await Hesap(app, c, "admin@example.test", true);
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Database.ExecuteSqlRawAsync("DROP TABLE Gonderimler");
        foreach (var path in new[] { "/Pano", "/Yonetim/Istatistikler" })
        {
            var response = await c.GetAsync(path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("şu anda ulaşılamıyor", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }
    }

    [Fact]
    public async Task TeknikMulakatKategorisiMevcutAmaOrnekDersAktarimiYok()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        var kategori = await db.Kategoriler.SingleAsync(k => k.Kisaltma == "TMA");
        Assert.False(await db.Egitimler.AnyAsync(e => e.KategoriId == kategori.Id));
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/Egitimler?KategoriId={kategori.Id}")).StatusCode);
        // Bu kontrol gerçek TMA dersi/sandbox uçtan uca kabulü değildir.
    }
}
