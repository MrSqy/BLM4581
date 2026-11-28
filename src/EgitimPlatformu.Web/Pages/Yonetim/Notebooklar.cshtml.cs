using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Pages.Yonetim;

[RequestSizeLimit(3 * 1024 * 1024)]
public sealed class NotebooklarModel(UygulamaDbContext db) : YonetimPageModel(db)
{
    [BindProperty, Required, StringLength(150)] public string Baslik { get; set; } = "";
    [BindProperty, Range(1, int.MaxValue)] public int EgitimId { get; set; }
    [BindProperty] public IFormFile? Belge { get; set; }
    public IReadOnlyList<Notebook> Kayitlar { get; private set; } = [];
    public IReadOnlyList<Egitim> Egitimler { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await Yukle(ct);
        if (SilId.HasValue)
        {
            SilinecekAd = Kayitlar.FirstOrDefault(n => n.Id == SilId)?.Baslik;
            if (SilinecekAd is null) return NotFound();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await Yukle(ct);
        if (!Egitimler.Any(e => e.Id == EgitimId)) ModelState.AddModelError(nameof(EgitimId), "Ders bulunamadı.");
        if (!ModelState.IsValid) return Page();
        var icerik = NotebookBelgesi.Bos().Yaz();
        if (Belge is not null)
        {
            if (Belge.Length is < 1 or > 2 * 1024 * 1024)
                ModelState.AddModelError(nameof(Belge), "Notebook dosyası 1 bayt ile 2 MiB arasında olmalı.");
            else
            {
                try
                {
                    using var okuyucu = new StreamReader(Belge.OpenReadStream());
                    icerik = NotebookBelgesi.Oku(await okuyucu.ReadToEndAsync(ct)).Yaz();
                }
                catch (Exception ex) when (ex is JsonException or ArgumentException)
                {
                    ModelState.AddModelError(nameof(Belge), "Geçerli bir nbformat 4 dosyası seçin.");
                }
            }
        }
        if (System.Text.Encoding.UTF8.GetByteCount(icerik) > 2 * 1024 * 1024)
            ModelState.AddModelError(nameof(Belge), "Notebook boyut sınırını aşıyor.");
        if (!ModelState.IsValid) return Page();
        var notebook = new Notebook
        {
            KullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            EgitimId = EgitimId, Baslik = Baslik.Trim(), IcerikJson = icerik, GuncellendiUtc = DateTime.UtcNow
        };
        Db.Notebooklar.Add(notebook);
        if (!await Kaydet(ct)) return Page();
        return RedirectToPage("/Notebooklar/Detay", new { id = notebook.Id });
    }

    public async Task<IActionResult> OnPostSilAsync(int id, CancellationToken ct)
    {
        ModelState.Clear();
        var kayit = await Db.Notebooklar.SingleOrDefaultAsync(n => n.Id == id && n.EgitimId != null, ct);
        if (kayit is null) return NotFound();
        SilId = id;
        SilinecekAd = kayit.Baslik;
        if (!SilmeOnayi()) { await Yukle(ct); return Page(); }
        Db.Notebooklar.Remove(kayit);
        if (!await Kaydet(ct)) { await Yukle(ct); return Page(); }
        return RedirectToPage();
    }

    private async Task Yukle(CancellationToken ct)
    {
        Kayitlar = await Db.Notebooklar.AsNoTracking().Where(n => n.EgitimId != null)
            .OrderByDescending(n => n.GuncellendiUtc).ThenByDescending(n => n.Id).ToListAsync(ct);
        Egitimler = await Db.Egitimler.AsNoTracking().OrderBy(e => e.Kod).ToListAsync(ct);
    }
}
