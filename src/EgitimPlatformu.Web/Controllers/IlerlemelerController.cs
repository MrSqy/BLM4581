using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Controllers;

[ApiController, Route("api/mobil/ilerlemeler"), EnableRateLimiting("mobil")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IlerlemelerController(IlerlemeServisi ilerleme, ILogger<IlerlemelerController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DersIlerlemesi>>> Liste(CancellationToken ct)
    {
        var kullaniciId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(kullaniciId)) return Unauthorized();
        try { return await ilerleme.ListeAsync(kullaniciId, ct); }
        catch (Exception ex) when (GonderimGecmisiHatasi.VeritabaniHatasiMi(ex))
        {
            return Kullanilamiyor(ex);
        }
    }

    [HttpGet("{id:int:min(1)}")]
    public async Task<ActionResult<DersIlerlemesi>> Detay(int id, CancellationToken ct)
    {
        var kullaniciId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(kullaniciId)) return Unauthorized();
        try
        {
            var ders = (await ilerleme.ListeAsync(kullaniciId, ct, id)).SingleOrDefault();
            return ders is null ? NotFound() : ders;
        }
        catch (Exception ex) when (GonderimGecmisiHatasi.VeritabaniHatasiMi(ex))
        {
            return Kullanilamiyor(ex);
        }
    }

    [HttpPost("{id:int:min(1)}/tamamla")]
    public async Task<IActionResult> Tamamla(int id, CancellationToken ct)
    {
        var kullaniciId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(kullaniciId)) return Unauthorized();
        try
        {
            return (await ilerleme.ElleTamamlaAsync(kullaniciId, id, ct)) switch
            {
                TamamlamaSonucu.Bulunamadi => NotFound(),
                TamamlamaSonucu.TestGerekli => Conflict(new { mesaj = "Bu dersi tamamlamak için otomatik testleri geç." }),
                _ => NoContent()
            };
        }
        catch (Exception ex) when (ex is DbUpdateException || GonderimGecmisiHatasi.VeritabaniHatasiMi(ex))
        {
            return Kullanilamiyor(ex);
        }
    }

    private ObjectResult Kullanilamiyor(Exception ex)
    {
        logger.LogWarning(ex, "Ders ilerlemesine erişilemedi.");
        return StatusCode(503, new { mesaj = "Ders ilerlemesine şu anda ulaşılamıyor.", durum = "ilerleme_kullanilamiyor" });
    }
}
