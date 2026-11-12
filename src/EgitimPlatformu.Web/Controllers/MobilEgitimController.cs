using System.ComponentModel.DataAnnotations;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Controllers;

public sealed record MobilKodIstegi([Required, StringLength(32000)] string Kod);

[ApiController, Route("api/mobil"), EnableRateLimiting("mobil")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MobilEgitimController(UygulamaDbContext db, IMobilKodServisi runner, DersErisimi erisim, GonderimServisi gonderimler) : ControllerBase
{
    private async Task<bool> OturumVar() => (await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)).Succeeded;
    [HttpGet("kategoriler")]
    public async Task<IActionResult> Kategoriler() => Ok(await db.Kategoriler.AsNoTracking().OrderBy(k => k.Sira)
        .Select(k => new { k.Id, k.Ad, k.Kisaltma, k.Aciklama, k.Sira }).ToListAsync());

    [HttpGet("egitimler"), IstegeBagliBearer]
    public async Task<IActionResult> Egitimler(int? kategoriId)
    {
        var member = await OturumVar();
        var dersler = await db.Egitimler.AsNoTracking().OrderBy(e => e.Sira).ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.KategoriId, e.Kod, e.Baslik, e.Sira, gorevSayisi = e.Gorevler.Count(g => g.Aktif) }).ToListAsync();
        var ilkler = dersler.GroupBy(e => e.KategoriId).Select(g => g.First().Id).ToHashSet();
        // Liste hiçbir dersin tam içeriğini, başlangıç/test kodunu taşımaz.
        return Ok(dersler.Where(e => kategoriId == null || e.KategoriId == kategoriId).Select(e => new {
            e.Id, e.KategoriId, e.Kod, e.Baslik, e.Sira, e.gorevSayisi,
            ozet = $"{e.Baslik} — {e.gorevSayisi} alıştırma içeren ders.",
            misafireAcik = ilkler.Contains(e.Id), erisilebilir = member || ilkler.Contains(e.Id) }));
    }

    [HttpGet("egitimler/{id:int}"), IstegeBagliBearer]
    public async Task<IActionResult> Egitim(int id)
    {
        var ders = await db.Egitimler.AsNoTracking().Include(e => e.Gorevler).SingleOrDefaultAsync(e => e.Id == id);
        if (ders == null) return NotFound(new { mesaj = "Ders bulunamadı." });
        var first = await erisim.IlkDers(ders);
        if (!first && !await OturumVar()) return Unauthorized(new { mesaj = "Bu derse devam etmek için giriş yapın." });
        return Ok(new { ders.Id, ders.KategoriId, ders.Kod, ders.Baslik, ders.Sira, ders.IcerikMarkdown,
            ders.BaslangicKodu, misafireAcik = first, erisilebilir = true,
            gorevler = ders.Gorevler.Where(g => g.Aktif).OrderBy(g => g.No).Select(GorevBilgisi.Olustur) });
    }

    [HttpPost("egitimler/{id:int}/calistir"), IstegeBagliBearer]
    public Task<IActionResult> Calistir(int id, MobilKodIstegi input, CancellationToken ct) => Kod(id, input, false, ct);

    [HttpPost("egitimler/{id:int}/test"), IstegeBagliBearer]
    public Task<IActionResult> Test(int id, MobilKodIstegi input, CancellationToken ct) => Kod(id, input, true, ct);

    private async Task<IActionResult> Kod(int id, MobilKodIstegi input, bool test, CancellationToken ct)
    {
        var ders = await db.Egitimler.AsNoTracking().Include(e => e.Gorevler).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (ders == null) return NotFound(new { mesaj = "Ders bulunamadı." });
        if (!await erisim.Izinli(HttpContext, ders, ct)) return Unauthorized(new { mesaj = "Bu derste kod çalıştırmak için giriş yapın." });
        if (ders.Gorevler.Any(g => g.Aktif && g.Tur != "eski-pytest"))
            return Conflict(new { mesaj = "Bu ders görev bazlıdır. /egitimler/{id}/gorevler/{no}/calistir veya /test kullanın." });
        if (test && string.IsNullOrWhiteSpace(ders.TestKodu))
            return Conflict(new { mesaj = "Bu derse henüz otomatik test eklenmemiş." });
        if (string.IsNullOrWhiteSpace(input.Kod)) return BadRequest(new { mesaj = "Çalıştırılacak kodu yazın." });
        var baslangic = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var sonuc = await runner.CalistirAsync(input.Kod, ders.TestKodu, test,
                ders.Gorevler.Where(g => g.Aktif && g.No.HasValue).Select(g => g.No!.Value).ToArray(), ct);
            var sure = System.Diagnostics.Stopwatch.GetElapsedTime(baslangic);
            if (!test) return Ok(sonuc);
            var kullaniciId = await DersErisimi.BearerKullaniciId(HttpContext);
            var gonderimId = await gonderimler.KaydetAsync(kullaniciId, ders, null, input.Kod,
                new(sonuc.Basarili, sonuc.Durum, sonuc.Cikti, sonuc.Hata), sure, ct);
            return Ok(sonuc with { GonderimId = gonderimId });
        }
        catch (DbUpdateException) { return StatusCode(503, new { mesaj = GonderimGecmisiHatasi.KayitMesaji, durum = "gonderim_kayit_hatasi" }); }
        catch (InvalidOperationException ex)
        {
            return SandboxHataYaniti.Olustur(ex);
        }
    }
}
