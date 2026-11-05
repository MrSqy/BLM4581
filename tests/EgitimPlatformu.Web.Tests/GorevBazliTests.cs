using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace EgitimPlatformu.Web.Tests;

public class GorevBazliTests
{
    private sealed class Runner : IGorevKodServisi
    {
        public Gorev? Gorev; public bool Fail;
        public Task<GorevKodSonucu> CalistirAsync(Gorev gorev, string kod, bool test, CancellationToken ct)
        {
            Gorev = gorev;
            if (Fail) throw new InvalidOperationException();
            return Task.FromResult(new GorevKodSonucu(gorev.No, true, "basarili", "Merhaba, dünya!\n", "", 0, true, true, []));
        }
    }
    private static IcerikPaketi Paket() => new() { Surum = 2, Kategoriler = [new() {
        Kisaltma = "PY", Ad = "Python", Aciklama = "", Egitimler = [new() {
            Kod = "PY-01", Baslik = "Görevli ders", IcerikMarkdown = "# Görev", Gorevler = [new() {
                No = 1, Baslik = "Kod", Tur = "kod", BaslangicDosyasi = "gorevler/selam.py", BaslangicKodu = "# TODO",
                Kontrol = new() { Tur = "standart-cikti", BeklenenCikti = "Merhaba, dünya!\n", Girdi = "", BeklenenCikisKodu = 0, Kullanilmamali = [] }
            }, new() { No = 2, Baslik = "Açıkla", Tur = "aciklama", BaslangicKodu = "" }] }] }] };
    [Fact]
    public async Task KaynakOrnegiYediGorevVeMobilKontrolBilgisi()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        var lesson = await c.GetFromJsonAsync<JsonElement>("/api/mobil/egitimler/1");
        var tasks = lesson.GetProperty("gorevler").EnumerateArray().ToArray();
        Assert.Equal(7, tasks.Length); Assert.Equal(5, tasks.Count(g => g.GetProperty("tur").GetString() == "kod"));
        Assert.Equal("gorevler/selam.py", tasks[0].GetProperty("baslangicDosyasi").GetString());
        Assert.Equal("Merhaba, dünya!\n", tasks[0].GetProperty("kontrol").GetProperty("beklenenCikti").GetString());
        Assert.Equal(JsonValueKind.Null, tasks[5].GetProperty("kontrol").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/mobil/egitimler/1/test", new { kod = "pass" })).StatusCode);
    }
    [Theory]
    [InlineData("calistir")][InlineData("test")]
    public async Task GorevEndpointYetkiSiniriVeSunucuKurallari(string action)
    {
        var runner = new Runner(); using var app = new IdentityApplication(gorevRunner: runner); using var c = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/mobil/egitimler/1/gorevler/1/{action}", new { kod = "print('x')", kontrol = new { beklenenCikti = "CLIENT" }, baslangicDosyasi = "../../x" })).StatusCode);
        Assert.Equal("gorevler/selam.py", runner.Gorev!.BaslangicDosyasi);
        Assert.Equal("Merhaba, dünya!\n", JsonSerializer.Deserialize<GorevKontrolu>(runner.Gorev.KontrolJson!)!.BeklenenCikti);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync($"/api/mobil/egitimler/2/gorevler/1/{action}", new { kod = "pass" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/mobil/egitimler/1/gorevler/6/{action}", new { kod = "pass" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync($"/api/mobil/egitimler/1/gorevler/8/{action}", new { kod = "pass" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/mobil/egitimler/1/gorevler/1/{action}", new { kod = " " })).StatusCode);
        runner.Fail = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.PostAsJsonAsync($"/api/mobil/egitimler/1/gorevler/1/{action}", new { kod = "pass" })).StatusCode);
        await IdentityApplication.KayitAsync(c);
        var login = await c.PostAsJsonAsync("/api/hesap/giris", new { eposta = "ogrenci@example.test", sifre = "Guvenli123!" });
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenYaniti>())!.AccessToken);
        runner.Fail = false;
        // Protected lesson receives a current code task: same access policy as guest first lesson.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            var task = await db.Gorevler.FirstAsync(g => g.EgitimId == 2 && g.No == 1);
            task.Tur = runner.Gorev!.Tur; task.BaslangicDosyasi = runner.Gorev.BaslangicDosyasi; task.KontrolJson = runner.Gorev.KontrolJson;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/mobil/egitimler/2/gorevler/1/{action}", new { kod = "pass" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/mobil/hesap/cikis", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync($"/api/mobil/egitimler/2/gorevler/1/{action}", new { kod = "pass" })).StatusCode);
    }
    [Fact]
    public async Task V2AktarimEskiSekizinciyiSilmezTekrarAktarimKimlikKorur()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IcerikAktarimServisi>();
        var eighth = await db.Gorevler.SingleAsync(g => g.EgitimId == 1 && g.No == 8);
        eighth.Aktif = true; var oldText = eighth.Aciklama; var oldId = eighth.Id; await db.SaveChangesAsync();
        await service.AktarAsync(Paket(), default);
        var id = (await db.Gorevler.SingleAsync(g => g.EgitimId == 1 && g.No == 1)).Id;
        await service.AktarAsync(Paket(), default);
        Assert.Equal(id, (await db.Gorevler.SingleAsync(g => g.EgitimId == 1 && g.No == 1)).Id);
        Assert.False(eighth.Aktif); Assert.Equal(oldId, eighth.Id); Assert.Equal(oldText, eighth.Aciklama);
        Assert.Equal(24, await db.Gorevler.CountAsync());
        Assert.Equal(2, await db.Gorevler.CountAsync(g => g.EgitimId == 1 && g.Aktif));
        var detail = await c.GetFromJsonAsync<EgitimDetay>("/api/egitimler/1"); Assert.Equal(2, detail!.Gorevler.Count);
        var legacy = Paket(); legacy.Surum = 1;
        foreach(var g in legacy.Kategoriler[0].Egitimler[0].Gorevler) { g.Tur = null; g.Kontrol = null; g.BaslangicDosyasi = null; g.BaslangicKodu = null; }
        await Assert.ThrowsAsync<ArgumentException>(() => service.AktarAsync(legacy, default));
    }
    [Fact]
    public async Task GercekOrnekPaketHttpIleTekrarlanabilirVeEksikKontrolReddedilir()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["BaslangicYoneticisi:Eposta"] = "admin@example.test", ["BaslangicYoneticisi:Sifre"] = "Guvenli123!" }).Build();
        await IdentityBaslangic.HazirlaAsync(app.Services,config); await IdentityApplication.GirisAsync(c,"admin@example.test");
        var token=await c.GetFromJsonAsync<Dictionary<string,string>>("/api/icerik/antiforgery");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token!["token"]);
        using var stream=typeof(GuncelPythonOrnegi).Assembly.GetManifestResourceStream("PythonOrnegi")!;
        var node=await System.Text.Json.Nodes.JsonNode.ParseAsync(stream);
        for(var i=0;i<2;i++) Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync("/api/icerik/aktar",node)).StatusCode);
        node!["kategoriler"]![0]!["egitimler"]![0]!["gorevler"]![0]!["kontrol"]!.AsObject().Remove("beklenenCikti");
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/icerik/aktar",node)).StatusCode);
        using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        Assert.Equal(7,await db.Gorevler.CountAsync(g=>g.EgitimId==1 && g.Aktif));
        Assert.Equal(8,await db.Gorevler.CountAsync(g=>g.EgitimId==1));
    }
    [Theory]
    [InlineData("path")][InlineData("type")][InlineData("control")][InlineData("explanation")][InlineData("duplicate")]
    public async Task GecersizV2PaketHttp400VeVeriDegismez(string error)
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["BaslangicYoneticisi:Eposta"] = "admin@example.test", ["BaslangicYoneticisi:Sifre"] = "Guvenli123!" }).Build();
        await IdentityBaslangic.HazirlaAsync(app.Services, config); await IdentityApplication.GirisAsync(c,"admin@example.test");
        var token = await c.GetFromJsonAsync<Dictionary<string,string>>("/api/icerik/antiforgery"); c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token!["token"]);
        var p = Paket(); var g = p.Kategoriler[0].Egitimler[0].Gorevler[0];
        if(error == "path") g.BaslangicDosyasi = "gorevler/../../x.py";
        if(error == "type") g.Tur = "pytest";
        if(error == "control") g.Kontrol = null;
        if(error == "explanation") g.Tur = "aciklama";
        if(error == "duplicate") p.Kategoriler[0].Egitimler[0].Gorevler.Add(g);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/icerik/aktar",p)).StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Equal(7,await scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Gorevler.CountAsync(g=>g.EgitimId==1 && g.Aktif));
    }
}
