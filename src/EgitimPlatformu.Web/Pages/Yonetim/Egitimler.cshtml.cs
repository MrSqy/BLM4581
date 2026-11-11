using System.ComponentModel.DataAnnotations;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EgitimPlatformu.Web.Pages.Yonetim;

public class EgitimlerModel(UygulamaDbContext db) : YonetimPageModel(db)
{
    [BindProperty] public EgitimFormu Input { get; set; } = new();
    public List<Egitim> Kayitlar { get; private set; } = [];
    public List<Kategori> Kategoriler { get; private set; } = [];
    public override string SilmeNotu => "Eğitimle birlikte bağlı görevleri de silinir. Bu işlem geri alınamaz.";
    private async Task Yukle(CancellationToken ct)
    {
        Kayitlar = await Db.Egitimler.AsNoTracking().Include(e => e.Kategori).OrderBy(e => e.Sira).ThenBy(e => e.Id).ToListAsync(ct);
        Kategoriler = await Db.Kategoriler.AsNoTracking().OrderBy(k => k.Sira).ThenBy(k => k.Id).ToListAsync(ct);
    }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await Yukle(ct);
        if (DuzenleId.HasValue)
        {
            var e = Kayitlar.Find(e => e.Id == DuzenleId); if (e is null) return NotFound();
            Input = new() { KategoriId = e.KategoriId, Kod = e.Kod, Baslik = e.Baslik, Sira = e.Sira, IcerikMarkdown = e.IcerikMarkdown, BaslangicKodu = e.BaslangicKodu, TestKodu = e.TestKodu };
        }
        if (SilId.HasValue) { var e = Kayitlar.Find(e => e.Id == SilId); if (e is null) return NotFound(); SilinecekAd = e.Baslik; }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await Yukle(ct); if (!ModelState.IsValid) return Page();
        if (!Kategoriler.Any(k => k.Id == Input.KategoriId)) ModelState.AddModelError("Input.KategoriId", "Kategori bulunamadı.");
        if (await Db.Egitimler.AnyAsync(e => e.Kod == Input.Kod && e.Id != DuzenleId, ct)) ModelState.AddModelError("Input.Kod", "Bu ders kodu zaten kullanılıyor.");
        if (!ModelState.IsValid) return Page();
        var entity = DuzenleId.HasValue ? await Db.Egitimler.FindAsync([DuzenleId.Value], ct) : new Egitim();
        if (entity is null) return NotFound();
        entity.KategoriId = Input.KategoriId; entity.Kod = Input.Kod; entity.Baslik = Input.Baslik; entity.Sira = Input.Sira;
        entity.IcerikMarkdown = Input.IcerikMarkdown ?? ""; entity.BaslangicKodu = Input.BaslangicKodu ?? ""; entity.TestKodu = Input.TestKodu ?? "";
        if (!DuzenleId.HasValue) Db.Egitimler.Add(entity);
        if (!await Kaydet(ct)) return Page();
        TempData["Bildirim"] = "Eğitim kaydedildi."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostSilAsync(int id, CancellationToken ct)
    {
        ModelState.Clear(); await Yukle(ct);
        var e = await Db.Egitimler.FindAsync([id], ct); if (e is null) return NotFound();
        SilId = id; SilinecekAd = e.Baslik; if (!SilmeOnayi()) return Page();
        Db.Egitimler.Remove(e); if (!await Kaydet(ct)) return Page();
        TempData["Bildirim"] = "Eğitim ve bağlı görevleri silindi."; return RedirectToPage();
    }
    public sealed class EgitimFormu
    {
        [Required(ErrorMessage = "Kategori seçin."), Range(1, int.MaxValue, ErrorMessage = "Geçerli kategori seçin."), Display(Name = "Kategori")] public int? KategoriId { get; set; }
        [Required(ErrorMessage = "Ders kodu zorunludur."), RegularExpression("[A-Z][A-Z0-9-]{0,19}", ErrorMessage = "Kod büyük harfle başlamalı; en fazla 20 büyük harf, rakam veya tire içermeli."), Display(Name = "Ders kodu")] public string Kod { get; set; } = "";
        [Required(ErrorMessage = "Başlık zorunludur."), StringLength(150, ErrorMessage = "Başlık en fazla 150 karakter olabilir."), Display(Name = "Başlık")] public string Baslik { get; set; } = "";
        [Range(0, int.MaxValue, ErrorMessage = "Sıra negatif olamaz."), Display(Name = "Sıra")] public int Sira { get; set; }
        [StringLength(200000, ErrorMessage = "İçerik en fazla 200000 karakter olabilir."), Display(Name = "Ders içeriği (Markdown)")] public string? IcerikMarkdown { get; set; } = "";
        [StringLength(100000, ErrorMessage = "Kod en fazla 100000 karakter olabilir."), Display(Name = "Ders başlangıç kodu")] public string? BaslangicKodu { get; set; } = "";
        [StringLength(100000, ErrorMessage = "Test kodu en fazla 100000 karakter olabilir."), Display(Name = "Ders test kodu (eski pytest dersleri)")] public string? TestKodu { get; set; } = "";
    }
}
