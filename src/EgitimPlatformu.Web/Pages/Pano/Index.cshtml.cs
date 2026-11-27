using System.Security.Claims;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EgitimPlatformu.Web.Pages.Pano;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(IGonderimGecmisi gecmis, ILogger<IndexModel> logger) : PageModel
{
    public bool GecmisHazir { get; private set; }
    public GonderimSayfasi SonGonderimler { get; private set; } = new(0, []);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(kullaniciId)) return Challenge();
        if (!gecmis.Kullanilabilir) { Response.StatusCode = 503; return Page(); }
        try
        {
            var sonuc = await gecmis.ListeAsync(kullaniciId, 1, 5, ct);
            if (sonuc.Kayitlar.Any(g => g.KullaniciId != kullaniciId)) return NotFound();
            SonGonderimler = sonuc;
            GecmisHazir = true;
        }
        catch (Exception ex) when (GonderimGecmisiHatasi.VeritabaniHatasiMi(ex))
        {
            logger.LogWarning(ex, "Öğrenci panosunun son gönderimleri okunamadı.");
            Response.StatusCode = 503;
        }
        return Page();
    }
}
