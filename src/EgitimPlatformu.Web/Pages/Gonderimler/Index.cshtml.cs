using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EgitimPlatformu.Web.Pages.Gonderimler;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(IGonderimGecmisi gecmis) : PageModel
{
    [BindProperty(SupportsGet = true), Range(1, 1000000)] public int Sayfa { get; set; } = 1;
    public const int Boyut = 20;
    public bool Hazir => gecmis.Kullanilabilir;
    public GonderimSayfasi Sonuc { get; private set; } = new(0, []);
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Challenge();
        if (!ModelState.IsValid) return BadRequest();
        if (!Hazir) { Response.StatusCode = 503; return Page(); }
        Sonuc = await gecmis.ListeAsync(userId, Sayfa, Boyut, ct);
        // Adaptör yanlışlıkla başka kullanıcı döndürürse hiçbir kaydı göstermeyiz.
        if (Sonuc.Kayitlar.Any(g => g.KullaniciId != userId)) return NotFound();
        return Page();
    }
}
