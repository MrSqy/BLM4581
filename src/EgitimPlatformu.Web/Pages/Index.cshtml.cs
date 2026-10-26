using System.Data.Common;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Pages;

public class IndexModel(UygulamaDbContext db, ILogger<IndexModel> logger) : PageModel
{
    public List<EgitimKartModel> Egitimler { get; private set; } = [];
    public bool KatalogKullanilamiyor { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Egitimler = await db.Egitimler.AsNoTracking()
                .OrderBy(e => e.Kategori == null ? 0 : e.Kategori.Sira)
                .ThenBy(e => e.Sira).ThenBy(e => e.Id)
                .Select(EgitimKartModel.Secim).Take(4).ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbException || ex is InvalidOperationException { InnerException: DbException })
        {
            logger.LogError(ex, "Ana sayfanın eğitimleri okunamadı.");
            KatalogKullanilamiyor = true;
        }
    }
}
