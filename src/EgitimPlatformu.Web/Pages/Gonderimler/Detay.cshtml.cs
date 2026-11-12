using System.Security.Claims;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace EgitimPlatformu.Web.Pages.Gonderimler;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DetayModel(IGonderimGecmisi gecmis, ILogger<DetayModel> logger) : PageModel
{
    private bool okumaHatasi;
    public bool Hazir => gecmis.Kullanilabilir && !okumaHatasi;
    public GonderimGorunumu? Kayit { get; private set; }
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Challenge();
        if (!ModelState.IsValid || id <= 0) return NotFound();
        if (!Hazir) { Response.StatusCode = 503; return Page(); }
        try { Kayit = await gecmis.DetayAsync(userId, id, ct); }
        catch (Exception ex) when (GonderimGecmisiHatasi.VeritabaniHatasiMi(ex))
        {
            logger.LogWarning(ex, GonderimGecmisiHatasi.OkumaMesaji);
            okumaHatasi = true;
            Response.StatusCode = 503;
            return Page();
        }
        if (Kayit is null || Kayit.KullaniciId != userId || Kayit.Id != id) return NotFound();
        return Page();
    }
}
