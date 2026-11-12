using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace EgitimPlatformu.Web.Controllers;

[ApiController, Route("api/mobil/egitimler/{id:int}/gorevler/{no:int}"), EnableRateLimiting("mobil")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), RequestSizeLimit(256 * 1024)]
public sealed class MobilGorevController(UygulamaDbContext db, DersErisimi erisim, IGorevKodServisi runner, GonderimServisi gonderimler) : ControllerBase
{
    [HttpPost("calistir"), IstegeBagliBearer]
    public Task<IActionResult> Calistir(int id, int no, MobilKodIstegi input, CancellationToken ct) => Kod(id, no, input, false, ct);
    [HttpPost("test"), IstegeBagliBearer]
    public Task<IActionResult> Test(int id, int no, MobilKodIstegi input, CancellationToken ct) => Kod(id, no, input, true, ct);
    private async Task<IActionResult> Kod(int id, int no, MobilKodIstegi input, bool test, CancellationToken ct)
    {
        var ders = await db.Egitimler.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (ders is null) return NotFound(new { mesaj = "Ders bulunamadı." });
        if (!await erisim.Izinli(HttpContext, ders, ct)) return Unauthorized(new { mesaj = "Bu ders için giriş yapın." });
        var gorev = await db.Gorevler.AsNoTracking().SingleOrDefaultAsync(g => g.EgitimId == id && g.No == no && g.Aktif, ct);
        if (gorev is null) return NotFound(new { mesaj = "Aktif görev bulunamadı." });
        if (gorev.Tur != "kod" || gorev.KontrolJson is null)
            return Conflict(new { mesaj = "Bu görevde otomatik program kontrolü yok." });
        if (string.IsNullOrWhiteSpace(input.Kod)) return BadRequest(new { mesaj = "Çalıştırılacak kodu yazın." });
        var baslangic = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var sonuc = await runner.CalistirAsync(gorev, input.Kod, test, ct);
            var sure = System.Diagnostics.Stopwatch.GetElapsedTime(baslangic);
            if (!test) return Ok(sonuc);
            var kullaniciId = await DersErisimi.BearerKullaniciId(HttpContext);
            var gonderimId = await gonderimler.KaydetAsync(kullaniciId, ders, gorev, input.Kod,
                new(sonuc.Basarili, sonuc.Durum, sonuc.Cikti, sonuc.Hata, sonuc.CikisKodu), sure, ct);
            return Ok(sonuc with { GonderimId = gonderimId });
        }
        catch (DbUpdateException) { return StatusCode(503, new { mesaj = GonderimGecmisiHatasi.KayitMesaji, durum = "gonderim_kayit_hatasi" }); }
        catch (InvalidOperationException) { return StatusCode(503, new { mesaj = "Sandbox kullanılamıyor veya meşgul." }); }
    }
}
