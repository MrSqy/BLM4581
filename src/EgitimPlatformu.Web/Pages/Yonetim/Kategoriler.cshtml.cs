using System.ComponentModel.DataAnnotations;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EgitimPlatformu.Web.Pages.Yonetim;

public class KategorilerModel(UygulamaDbContext db) : YonetimPageModel(db)
{
    [BindProperty] public KategoriFormu Input { get; set; } = new();
    public List<Kategori> Kayitlar { get; private set; } = [];
    public override string SilmeNotu => "Eğitim içeren kategoriler silinemez.";
    private async Task Yukle(CancellationToken ct) => Kayitlar = await Db.Kategoriler.AsNoTracking().OrderBy(k => k.Sira).ThenBy(k => k.Id).ToListAsync(ct);
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await Yukle(ct);
        if (DuzenleId.HasValue)
        {
            var k = Kayitlar.Find(k => k.Id == DuzenleId); if (k is null) return NotFound();
            Input = new() { Ad = k.Ad, Kisaltma = k.Kisaltma, Aciklama = k.Aciklama, Sira = k.Sira };
        }
        if (SilId.HasValue) { var k = Kayitlar.Find(k => k.Id == SilId); if (k is null) return NotFound(); SilinecekAd = k.Ad; }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await Yukle(ct);
        if (!ModelState.IsValid) return Page();
        if (await Db.Kategoriler.AnyAsync(k => k.Kisaltma == Input.Kisaltma && k.Id != DuzenleId, ct)) ModelState.AddModelError("Input.Kisaltma", "Bu kısaltma zaten kullanılıyor.");
        if (!ModelState.IsValid) return Page();
        var entity = DuzenleId.HasValue ? await Db.Kategoriler.FindAsync([DuzenleId.Value], ct) : new Kategori();
        if (entity is null) return NotFound();
        entity.Ad = Input.Ad; entity.Kisaltma = Input.Kisaltma; entity.Aciklama = Input.Aciklama ?? ""; entity.Sira = Input.Sira;
        if (!DuzenleId.HasValue) Db.Kategoriler.Add(entity);
        if (!await Kaydet(ct)) return Page();
        TempData["Bildirim"] = "Kategori kaydedildi."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostSilAsync(int id, CancellationToken ct)
    {
        // Silme formu düzenleme Input alanlarını göndermez.
        ModelState.Clear(); await Yukle(ct);
        var k = await Db.Kategoriler.FindAsync([id], ct); if (k is null) return NotFound();
        SilId = id; SilinecekAd = k.Ad;
        if (!SilmeOnayi()) return Page();
        if (await Db.Egitimler.AnyAsync(e => e.KategoriId == id, ct)) { ModelState.AddModelError("", "Bu kategori eğitim içeriyor; önce eğitimlerin kategorisini değiştirin veya eğitimleri silin."); return Page(); }
        Db.Kategoriler.Remove(k); if (!await Kaydet(ct)) return Page();
        TempData["Bildirim"] = "Kategori silindi."; return RedirectToPage();
    }
    public sealed class KategoriFormu
    {
        [Required(ErrorMessage = "Kategori adı zorunludur."), StringLength(100, ErrorMessage = "En fazla 100 karakter kullanın."), Display(Name = "Kategori adı")] public string Ad { get; set; } = "";
        [Required(ErrorMessage = "Kısaltma zorunludur."), RegularExpression("[A-Z][A-Z0-9-]{0,9}", ErrorMessage = "Kısaltma büyük harfle başlamalı; en fazla 10 büyük harf, rakam veya tire içermeli."), Display(Name = "Kısaltma")] public string Kisaltma { get; set; } = "";
        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir."), Display(Name = "Açıklama")] public string? Aciklama { get; set; } = "";
        [Range(0, int.MaxValue, ErrorMessage = "Sıra negatif olamaz."), Display(Name = "Sıra")] public int Sira { get; set; }
    }
}
