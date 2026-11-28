using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EgitimPlatformu.Web.Pages.Yonetim;

public class GorevlerModel(UygulamaDbContext db) : YonetimPageModel(db)
{
    [BindProperty] public GorevFormu Input { get; set; } = new();
    public List<Gorev> Kayitlar { get; private set; } = [];
    public List<Egitim> Egitimler { get; private set; } = [];
    private async Task Yukle(CancellationToken ct)
    {
        Kayitlar = await Db.Gorevler.AsNoTracking().Include(g => g.Egitim).OrderBy(g => g.EgitimId).ThenBy(g => g.No).ToListAsync(ct);
        Egitimler = await Db.Egitimler.AsNoTracking().OrderBy(e => e.Kod).ToListAsync(ct);
    }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await Yukle(ct);
        if (DuzenleId.HasValue)
        {
            var g = Kayitlar.Find(g => g.Id == DuzenleId); if (g is null) return NotFound();
            GorevKontrolu? k = null;
            try { if (g.KontrolJson != null) k = JsonSerializer.Deserialize<GorevKontrolu>(g.KontrolJson); }
            catch (JsonException) { ModelState.AddModelError("", "Mevcut kontrol verisi okunamadı. Kaydetmeden önce ölçütleri kontrol edin."); }
            Input = new() { EgitimId = g.EgitimId, No = g.No, Baslik = g.Baslik, Aciklama = g.Aciklama, Tur = g.Tur, Aktif = g.Aktif, BaslangicDosyasi = g.BaslangicDosyasi, BaslangicKodu = g.BaslangicKodu, BeklenenCikti = k?.BeklenenCikti, Girdi = k?.Girdi, BeklenenCikisKodu = k?.BeklenenCikisKodu ?? 0, Yasaklar = k == null ? "" : string.Join("\n", k.Kullanilmamali) };
        }
        if (SilId.HasValue) { var g = Kayitlar.Find(g => g.Id == SilId); if (g is null) return NotFound(); SilinecekAd = g.Baslik; }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await Yukle(ct); if (!ModelState.IsValid) return Page();
        if (!Egitimler.Any(e => e.Id == Input.EgitimId)) ModelState.AddModelError("Input.EgitimId", "Eğitim bulunamadı.");
        foreach (var hata in Input.Cakismalar(Kayitlar, DuzenleId))
            ModelState.AddModelError("Input." + hata.MemberNames.Single(), hata.ErrorMessage!);
        if (!ModelState.IsValid) return Page();
        var entity = DuzenleId.HasValue ? await Db.Gorevler.FindAsync([DuzenleId.Value], ct) : new Gorev();
        if (entity is null) return NotFound();
        Input.Uygula(entity);
        if (!DuzenleId.HasValue) Db.Gorevler.Add(entity);
        if (!await Kaydet(ct)) return Page();
        TempData["Bildirim"] = "Görev kaydedildi."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostSilAsync(int id, CancellationToken ct)
    {
        ModelState.Clear(); await Yukle(ct); var g = await Db.Gorevler.FindAsync([id], ct); if (g is null) return NotFound();
        SilId = id; SilinecekAd = g.Baslik; if (!SilmeOnayi()) return Page();
        Db.Gorevler.Remove(g); if (!await Kaydet(ct)) return Page();
        TempData["Bildirim"] = "Görev silindi."; return RedirectToPage();
    }
}
