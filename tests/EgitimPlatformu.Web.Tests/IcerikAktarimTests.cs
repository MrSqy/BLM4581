using System.Net;
using System.Net.Http.Json;
using System.Text;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public sealed class IcerikAktarimTests
{
    private static IcerikPaketi Paket() => new()
    {
        Surum = 1,
        Kategoriler = [new()
        {
            Kisaltma = "DEN", Ad = "Deneme", Egitimler = [new()
            {
                Kod = "DEN-01", Baslik = "Yeni ders", IcerikMarkdown = "# Deneme",
                BaslangicKodu = "pass", TestKodu = "assert True",
                Gorevler = [new() { No = 1, Baslik = "İlk görev" }]
            }]
        }]
    };

    private static async Task<HttpClient> YoneticiAsync(IdentityApplication app)
    {
        var client = await app.IstemciAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BaslangicYoneticisi:Eposta"] = "yonetici@example.test",
            ["BaslangicYoneticisi:Sifre"] = "Guvenli123!"
        }).Build();
        await IdentityBaslangic.HazirlaAsync(app.Services, config);
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.GirisAsync(client, "yonetici@example.test")).StatusCode);
        var token = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/icerik/antiforgery");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!["token"]);
        return client;
    }

    [Fact]
    public async Task AnonimOgrenciVeTokensizYoneticiReddedilir()
    {
        using var app = new IdentityApplication();
        using var client = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/icerik/aktar", Paket())).StatusCode);
        await IdentityApplication.KayitAsync(client);
        await IdentityApplication.GirisAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/icerik/aktar", Paket())).StatusCode);
        using var admin = await YoneticiAsync(app);
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/icerik/aktar", Paket())).StatusCode);
    }

    [Fact]
    public async Task TekrarAktarimKimlikleriKorurEksikleriSilmezVeGunceller()
    {
        using var app = new IdentityApplication();
        using var client = await YoneticiAsync(app);
        var paket = Paket();
        var response = await client.PostAsJsonAsync("/api/icerik/aktar", paket);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new AktarimSonucu(1, 1, 1), await response.Content.ReadFromJsonAsync<AktarimSonucu>());
        int dersId, gorevId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            var ders = await db.Egitimler.Include(e => e.Gorevler).SingleAsync(e => e.Kod == "DEN-01");
            dersId = ders.Id; gorevId = ders.Gorevler.Single().Id;
        }
        paket.Kategoriler[0].Egitimler[0].Baslik = "Güncellenen ders";
        paket.Kategoriler[0].Egitimler[0].Gorevler[0].Baslik = "Güncellenen görev";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/icerik/aktar", paket)).StatusCode);
        paket.Kategoriler[0].Egitimler[0].Gorevler.Clear();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/icerik/aktar", paket)).StatusCode);
        using var check = app.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        var saved = await context.Egitimler.Include(e => e.Gorevler).SingleAsync(e => e.Kod == "DEN-01");
        Assert.Equal(dersId, saved.Id);
        Assert.Equal("Güncellenen ders", saved.Baslik);
        Assert.Equal(gorevId, saved.Gorevler.Single().Id);
        Assert.Equal("Güncellenen görev", saved.Gorevler.Single().Baslik);
        Assert.Equal(4, await context.Egitimler.CountAsync());
        Assert.Equal(25, await context.Gorevler.CountAsync());
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"surum\":2,\"kategoriler\":[]}")]
    [InlineData("{\"surum\":1,\"kategoriler\":null}")]
    [InlineData("{\"surum\":1,\"kategoriler\":[null]}")]
    [InlineData("{\"surum\":1,\"kategoriler\":[{\"kisaltma\":\"DEN\",\"ad\":\"Deneme\",\"egitimler\":[null]}]}")]
    [InlineData("{\"surum\":1,\"kategoriler\":[{\"kisaltma\":\"DEN\",\"ad\":\"Deneme\",\"egitimler\":[{\"kod\":\"DEN-01\",\"baslik\":\"Ders\",\"gorevler\":[null]}]}]}")]
    public async Task BozukJsonSurumVeNullKayitlariDegistirmez(string json)
    {
        using var app = new IdentityApplication();
        using var client = await YoneticiAsync(app);
        var response = await client.PostAsync("/api/icerik/aktar", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        Assert.Equal(2, await db.Kategoriler.CountAsync());
        Assert.Equal(3, await db.Egitimler.CountAsync());
    }

    [Theory]
    [InlineData("kategori")]
    [InlineData("ders")]
    [InlineData("gorev")]
    [InlineData("uzun")]
    [InlineData("numara")]
    public async Task TekrarVeAlanHatalariPaketinTamaminiReddeder(string hata)
    {
        using var app = new IdentityApplication();
        using var client = await YoneticiAsync(app);
        var paket = Paket();
        var kategori = paket.Kategoriler[0];
        var ders = kategori.Egitimler[0];
        switch (hata)
        {
            case "kategori": paket.Kategoriler.Add(kategori); break;
            case "ders": kategori.Egitimler.Add(ders); break;
            case "gorev": ders.Gorevler.Add(ders.Gorevler[0]); break;
            case "uzun": ders.Baslik = new string('a', 151); break;
            case "numara": ders.Gorevler[0].No = 101; break;
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/icerik/aktar", paket)).StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Egitimler.CountAsync());
    }

    [Fact]
    public async Task VeritabaniHatasiOncekiDegisiklikleriDeGeriAlir()
    {
        using var app = new IdentityApplication();
        using var client = await YoneticiAsync(app);
        string eskiBaslik;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            eskiBaslik = (await db.Egitimler.SingleAsync(e => e.Kod == "PY-01")).Baslik;
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reddet BEFORE UPDATE ON Egitimler WHEN NEW.Kod = 'PY-02' BEGIN SELECT RAISE(ABORT, 'test'); END;");
        }
        var paket = Paket();
        paket.Kategoriler[0].Egitimler = [new() { Kod = "PY-01", Baslik = "Değişmemeli" }, new() { Kod = "PY-02", Baslik = "Hatalı" }];
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/icerik/aktar", paket)).StatusCode);
        using var check = app.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        Assert.Equal(eskiBaslik, (await context.Egitimler.SingleAsync(e => e.Kod == "PY-01")).Baslik);
        Assert.False(await context.Kategoriler.AnyAsync(k => k.Kisaltma == "DEN"));
    }
}
