using System.Data.Common;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Pages.Egitimler;

public class DetayModel(UygulamaDbContext db, MarkdownServisi markdown, ILogger<DetayModel> logger) : PageModel
{
    public Egitim? Egitim { get; private set; }
    public string IcerikHtml { get; private set; } = "";
    public List<GorevIcerigi> Gorevler { get; private set; } = [];
    public string? Hata { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || id is null or <= 0) return Bulunamadi();
        try
        {
            Egitim = await db.Egitimler.AsNoTracking().Include(e => e.Kategori).Include(e => e.Gorevler)
                .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (Egitim is null) return Bulunamadi();
            IcerikHtml = markdown.Html(Egitim.IcerikMarkdown);
            Gorevler = Egitim.Gorevler.OrderBy(g => g.No).ThenBy(g => g.Id)
                .Select(g => new GorevIcerigi(g.Id, g.No, g.Baslik, markdown.Html(g.Aciklama))).ToList();
        }
        catch (Exception ex) when (ex is DbException || ex is InvalidOperationException { InnerException: DbException })
        {
            logger.LogError(ex, "Ders okunamadı: {EgitimId}", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            Hata = "Ders içeriğine şu anda ulaşılamıyor. Lütfen daha sonra yeniden deneyin.";
        }
        return Page();
    }

    private IActionResult Bulunamadi()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        Hata = "Aradığın ders bulunamadı.";
        return Page();
    }

    public sealed record GorevIcerigi(int Id, int? No, string Baslik, string AciklamaHtml);
}
