using System.ComponentModel.DataAnnotations;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Controllers;

public sealed record KodCalistirIstegi(
    [Range(1, int.MaxValue)] int EgitimId,
    [Required, StringLength(32000)] string Kod);

// 3.4: Öğrenci kodunu dersin pytest testleriyle sandbox'ta çalıştırır, sonucu görev görev döndürür.
// Yalnız eski-pytest dersleri içindir; görev bazlı derslerde /api/mobil/.../gorevler/{no} uçları kullanılır.
[ApiController, Route("api/calistir"), EnableRateLimiting("mobil")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), RequestSizeLimit(256 * 1024)]
public sealed class KodCalistirController(UygulamaDbContext db, DersErisimi erisim, IKodCalistirmaServisi servis)
    : ControllerBase
{
    [HttpPost, IstegeBagliBearer]
    [ProducesResponseType<KodCalistirYaniti>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Calistir(KodCalistirIstegi istek, CancellationToken ct)
    {
        var ders = await db.Egitimler.AsNoTracking().Include(e => e.Gorevler)
            .SingleOrDefaultAsync(e => e.Id == istek.EgitimId, ct);
        if (ders is null) return NotFound(new { mesaj = "Ders bulunamadı." });
        if (!await erisim.Izinli(HttpContext, ders, ct))
            return Unauthorized(new { mesaj = "Bu derste kod çalıştırmak için giriş yapın." });

        var gorevler = ders.Gorevler.Where(g => g.Aktif).ToArray();
        if (gorevler.Any(g => g.Tur != "eski-pytest"))
            return Conflict(new { mesaj = "Bu ders görev bazlıdır. /api/mobil/egitimler/{id}/gorevler/{no}/test ucunu kullanın." });
        if (string.IsNullOrWhiteSpace(ders.TestKodu))
            return Conflict(new { mesaj = "Bu derse henüz otomatik test eklenmemiş." });
        if (string.IsNullOrWhiteSpace(istek.Kod)) return BadRequest(new { mesaj = "Çalıştırılacak kodu yazın." });

        try
        {
            var sonuc = await servis.CalistirAsync(istek.Kod, ders.TestKodu, ct);
            if (sonuc.AltyapiHatasi) return SandboxHataYaniti.AltyapiYok();
            return Ok(PytestGorevAyristirici.Ayristir(sonuc,
                gorevler.Where(g => g.No.HasValue).Select(g => g.No!.Value)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return SandboxHataYaniti.Olustur(ex);
        }
    }
}
