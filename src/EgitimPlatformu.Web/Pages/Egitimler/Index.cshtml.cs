using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Pages.Egitimler;

public class IndexModel(UygulamaDbContext db, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kategori seçin.")]
    public int? KategoriId { get; set; }

    [BindProperty(SupportsGet = true)]
    [StringLength(100, ErrorMessage = "Arama en fazla 100 karakter olabilir.")]
    public string? Q { get; set; }

    public List<Kategori> Kategoriler { get; private set; } = [];
    public List<EgitimKartModel> Egitimler { get; private set; } = [];
    public Kategori? SeciliKategori { get; private set; }
    public string? Hata { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            Hata = "Kategori veya arama bilgisi geçersiz. Filtreleri temizleyerek yeniden deneyin.";
            return Page();
        }
        try
        {
            Kategoriler = await db.Kategoriler.AsNoTracking().OrderBy(k => k.Sira).ThenBy(k => k.Id).ToListAsync(cancellationToken);
            if (KategoriId.HasValue)
            {
                SeciliKategori = Kategoriler.FirstOrDefault(k => k.Id == KategoriId);
                if (SeciliKategori is null)
                {
                    Response.StatusCode = StatusCodes.Status404NotFound;
                    Hata = "Bu kategori bulunamadı. Mevcut kategorilerden birini seçebilirsiniz.";
                    return Page();
                }
            }
            var query = db.Egitimler.AsNoTracking().AsQueryable();
            if (KategoriId.HasValue) query = query.Where(e => e.KategoriId == KategoriId);
            Q = Q?.Trim();
            if (!string.IsNullOrEmpty(Q))
            {
                // Contains parametreleştirilir; kullanıcı girdisi SQL veya ILIKE kalıbı değildir.
                var arama = Q.ToLower();
                query = query.Where(e => e.Baslik.ToLower().Contains(arama) || e.Kod.ToLower().Contains(arama)
                    || (e.Kategori != null && e.Kategori.Ad.ToLower().Contains(arama)));
            }
            Egitimler = await query.OrderBy(e => e.Kategori == null ? 0 : e.Kategori.Sira)
                .ThenBy(e => e.Sira).ThenBy(e => e.Id).Select(EgitimKartModel.Secim).ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbException || ex is InvalidOperationException { InnerException: DbException })
        {
            logger.LogError(ex, "Eğitim kataloğu okunamadı.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            Hata = "Eğitimlere şu anda ulaşılamıyor. Lütfen daha sonra yeniden deneyin.";
        }
        return Page();
    }
}
