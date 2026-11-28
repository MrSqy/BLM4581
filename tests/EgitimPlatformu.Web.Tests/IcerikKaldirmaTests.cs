using System.Net.Http.Json;
using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace EgitimPlatformu.Web.Tests;

public class IcerikKaldirmaTests
{
    private static IcerikPaketi Paket(string kisaltma, string ad, params string[] kodlar) => new() { Surum = 2, Kategoriler = [new() {
        Kisaltma = kisaltma, Ad = ad, Aciklama = "", Sira = 3,
        Egitimler = kodlar.Select((kod, i) => new EgitimAktarimi {
            Kod = kod, Baslik = kod, Sira = i + 1, IcerikMarkdown = "# " + kod, Gorevler = [
                new() { No = 1, Baslik = "Oku", Aciklama = "Metin", Tur = "aciklama", BaslangicKodu = "" },
                new() { No = 2, Baslik = "Açıkla", Aciklama = "Metin", Tur = "aciklama", BaslangicKodu = "" }] }).ToList() }] };

    private static async Task<string[]> MobilKategoriler(HttpClient c) =>
        (await c.GetFromJsonAsync<JsonElement>("/api/mobil/kategoriler")).EnumerateArray()
            .Select(k => k.GetProperty("kisaltma").GetString()!).ToArray();

    [Fact]
    public async Task DersleriKaldirirKategoriyiVeDigerDersleriKorur()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        var paket = Paket("TMA", "Teknik Mülakat Algoritmalar", "TMA-00", "TMA-01");
        var digerDers = await db.Egitimler.CountAsync(e => e.Kategori!.Kisaltma != "TMA");
        var digerGorev = await db.Gorevler.CountAsync(g => g.Egitim!.Kategori!.Kisaltma != "TMA");

        await scope.ServiceProvider.GetRequiredService<IcerikAktarimServisi>().AktarAsync(paket, default);
        Assert.Equal(2, await db.Egitimler.CountAsync(e => e.Kategori!.Kisaltma == "TMA"));

        var sonuc = await scope.ServiceProvider.GetRequiredService<IcerikKaldirmaServisi>().KaldirAsync(paket, kategoriDahil: false, default);
        Assert.Equal(new KaldirmaSonucu(0, 2, 4), sonuc);
        db.ChangeTracker.Clear();
        Assert.Equal(0, await db.Egitimler.CountAsync(e => e.Kategori!.Kisaltma == "TMA"));
        Assert.Equal(0, await db.Gorevler.CountAsync(g => g.Egitim!.Kod.StartsWith("TMA-")));
        Assert.Equal(digerDers, await db.Egitimler.CountAsync());
        Assert.Equal(digerGorev, await db.Gorevler.CountAsync());
        Assert.Contains("TMA", await MobilKategoriler(c));

        // Tekrar çalıştırmak hata vermez, hiçbir şey silmez.
        Assert.Equal(new KaldirmaSonucu(0, 0, 0),
            await scope.ServiceProvider.GetRequiredService<IcerikKaldirmaServisi>().KaldirAsync(paket, false, default));
    }

    [Fact]
    public async Task KategoriDahilYeniKategoriyiTamamenKaldirir()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        var paket = Paket("CS", "C# dili", "CS-01", "CS-02", "CS-03");
        await scope.ServiceProvider.GetRequiredService<IcerikAktarimServisi>().AktarAsync(paket, default);
        Assert.Contains("CS", await MobilKategoriler(c));

        var sonuc = await scope.ServiceProvider.GetRequiredService<IcerikKaldirmaServisi>().KaldirAsync(paket, kategoriDahil: true, default);
        Assert.Equal(new KaldirmaSonucu(1, 3, 6), sonuc);
        db.ChangeTracker.Clear();
        Assert.False(await db.Kategoriler.AnyAsync(k => k.Kisaltma == "CS"));
        Assert.False(await db.Egitimler.AnyAsync(e => e.Kod.StartsWith("CS-")));
        Assert.DoesNotContain("CS", await MobilKategoriler(c));
        Assert.Contains("PY", await MobilKategoriler(c));
    }

    [Fact]
    public async Task PaketDisiDersVarsaKategoriSilinmezHicbirSeyDegismez()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        var once = await db.Egitimler.CountAsync();
        var paket = Paket("PY", "Python dili", "PY-01");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IcerikKaldirmaServisi>().KaldirAsync(paket, kategoriDahil: true, default));
        db.ChangeTracker.Clear();
        Assert.Equal(once, await db.Egitimler.CountAsync());
        Assert.True(await db.Egitimler.AnyAsync(e => e.Kod == "PY-01"));
        Assert.True(await db.Kategoriler.AnyAsync(k => k.Kisaltma == "PY"));
    }

    [Fact]
    public async Task BaskaKategoridekiAyniKodluDersiSilmez()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        var paket = Paket("TMA", "Teknik Mülakat Algoritmalar", "PY-02");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IcerikKaldirmaServisi>().KaldirAsync(paket, false, default));
        db.ChangeTracker.Clear();
        Assert.True(await db.Egitimler.AnyAsync(e => e.Kod == "PY-02"));
    }
}

public class DemoPaketleriTests
{
    private static string Yol(string ad)
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "EgitimPlatformu.slnx"))) dizin = dizin.Parent;
        return Path.Combine(dizin!.FullName, "docs", "ornekler", ad);
    }

    private static async Task<int> DersSayisi(HttpClient c, string kisaltma)
    {
        var kategoriler = (await c.GetFromJsonAsync<JsonElement>("/api/mobil/kategoriler")).EnumerateArray().ToArray();
        var kategori = kategoriler.Where(k => k.GetProperty("kisaltma").GetString() == kisaltma).ToArray();
        if (kategori.Length == 0) return -1;
        var id = kategori[0].GetProperty("id").GetInt32();
        return (await c.GetFromJsonAsync<JsonElement>($"/api/mobil/egitimler?kategoriId={id}")).EnumerateArray().Count();
    }

    [Fact]
    public async Task TeknikMulakatEkleKaldirKategoriKalir()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        var paket = await IcerikYerelKomutu.Oku(Yol("tma-3-ders-v2.json"));
        using var scope = app.Services.CreateScope();
        Assert.Equal(0, await DersSayisi(c, "TMA"));
        await scope.ServiceProvider.GetRequiredService<IcerikAktarimServisi>().AktarAsync(paket, default);
        Assert.Equal(3, await DersSayisi(c, "TMA"));
        var detay = await c.GetFromJsonAsync<JsonElement>("/api/mobil/egitimler/" +
            (await c.GetFromJsonAsync<JsonElement>("/api/mobil/egitimler")).EnumerateArray()
                .First(e => e.GetProperty("kod").GetString() == "TMA-00").GetProperty("id").GetInt32());
        Assert.Equal(7, detay.GetProperty("gorevler").GetArrayLength());
        Assert.All(detay.GetProperty("gorevler").EnumerateArray(), g => Assert.Equal("aciklama", g.GetProperty("tur").GetString()));
        await scope.ServiceProvider.GetRequiredService<IcerikKaldirmaServisi>().KaldirAsync(paket, false, default);
        Assert.Equal(0, await DersSayisi(c, "TMA"));
        await scope.ServiceProvider.GetRequiredService<IcerikAktarimServisi>().AktarAsync(paket, default);
        Assert.Equal(3, await DersSayisi(c, "TMA"));
    }

    [Fact]
    public async Task CSharpKategoriyleEkleTamamenKaldir()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        var paket = await IcerikYerelKomutu.Oku(Yol("cs-3-ders-v2.json"));
        using var scope = app.Services.CreateScope();
        Assert.Equal(-1, await DersSayisi(c, "CS"));
        await scope.ServiceProvider.GetRequiredService<IcerikAktarimServisi>().AktarAsync(paket, default);
        Assert.Equal(3, await DersSayisi(c, "CS"));
        await scope.ServiceProvider.GetRequiredService<IcerikKaldirmaServisi>().KaldirAsync(paket, true, default);
        Assert.Equal(-1, await DersSayisi(c, "CS"));
        await scope.ServiceProvider.GetRequiredService<IcerikAktarimServisi>().AktarAsync(paket, default);
        Assert.Equal(3, await DersSayisi(c, "CS"));
    }
}
