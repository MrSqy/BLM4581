using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Pages.Yonetim;

[Authorize(Roles = Roller.Yonetici), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class YonetimPageModel(UygulamaDbContext db) : PageModel
{
    protected UygulamaDbContext Db { get; } = db;
    [BindProperty(SupportsGet = true)] public int? DuzenleId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SilId { get; set; }
    [BindProperty] public bool Onay { get; set; }
    public string? SilinecekAd { get; protected set; }
    public virtual string SilmeNotu => "Bu işlem geri alınamaz.";
    protected async Task<bool> Kaydet(CancellationToken ct)
    {
        try { await Db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "Kayıt başka bir işlemde değişmiş veya silinmiş. Sayfayı yenileyin."); }
        catch (DbUpdateException) { ModelState.AddModelError("", "İşlem kaydedilemedi. Benzersiz alanları ve ilişkili kayıtları kontrol edin."); }
        return false;
    }
    protected bool SilmeOnayi()
    {
        if (Onay) return true;
        ModelState.AddModelError("", "Silme işlemini onaylamalısınız."); return false;
    }
}
