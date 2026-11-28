using System.Security.Claims;
using System.Text.Json;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Pages.Notebooklar;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(2 * 1024 * 1024)]
public sealed class DetayModel(UygulamaDbContext db, MarkdownServisi markdown) : PageModel
{
    private string Kullanici => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<Notebook> Yazilabilir => db.Notebooklar.Duzenlenebilir(Kullanici, User.IsInRole(Roller.Yonetici));
    public Notebook Kayit { get; private set; } = null!;
    public IReadOnlyList<NotebookHucresi> Hucreler { get; private set; } = [];
    public bool Duzenlenebilir { get; private set; }
    [TempData] public string? Bildirim { get; set; }

    public string MarkdownHtml(string kaynak) => markdown.Html(kaynak);

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var kayit = await db.Notebooklar.AsNoTracking().Okunabilir(Kullanici).SingleOrDefaultAsync(n => n.Id == id, ct);
        if (kayit is null) return NotFound();
        Kayit = kayit;
        Duzenlenebilir = await Yazilabilir.AnyAsync(n => n.Id == id, ct);
        Hucreler = NotebookBelgesi.Oku(kayit.IcerikJson).OkuHucreler();
        return Page();
    }

    public async Task<IActionResult> OnPostHucreAsync(int id, int surum, int sira, string islem, CancellationToken ct)
    {
        var sorgu = Yazilabilir.Where(n => n.Id == id);
        var kayit = await sorgu.AsNoTracking().SingleOrDefaultAsync(ct);
        if (kayit is null) return NotFound();
        if (kayit.Surum != surum) return Cakisma(id);
        var belge = NotebookBelgesi.Oku(kayit.IcerikJson);
        var kaynak = islem == "kaydet" ? Request.Form["kaynak"].ToString() : null;
        if (!ModelState.IsValid || !belge.Degistir(islem, sira, kaynak)) return BadRequest();
        var icerik = belge.Yaz();
        if (System.Text.Encoding.UTF8.GetByteCount(icerik) > 2 * 1024 * 1024)
        {
            Bildirim = "Notebook boyut sınırını aşıyor.";
            return RedirectToPage(new { id });
        }
        var zaman = DateTime.UtcNow;
        var degisen = await sorgu.Where(n => n.Surum == surum).ExecuteUpdateAsync(u => u
            .SetProperty(n => n.IcerikJson, icerik).SetProperty(n => n.Surum, n => n.Surum + 1)
            .SetProperty(n => n.GuncellendiUtc, zaman), ct);
        if (degisen == 0) return Cakisma(id);
        return RedirectToPage(new { id });
    }

    private IActionResult Cakisma(int id)
    {
        Bildirim = "Notebook değişmiş. Güncel sürüm açıldı.";
        return RedirectToPage(new { id });
    }
}
