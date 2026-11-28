using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Controllers;

[ApiController, Route("api/mobil/yonetim/egitimler/{id:int:min(1)}")]
[Authorize(Policy = "ApiYonetici")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MobilYonetimController(UygulamaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Detay(int id, CancellationToken ct)
    {
        var e = await db.Egitimler.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        return e is null ? NotFound() : Ok(new
        {
            e.Id, e.KategoriId, e.Kod, e.Baslik, e.Sira, e.IcerikMarkdown, e.BaslangicKodu, e.TestKodu
        });
    }

    [HttpGet("gorevler")]
    public async Task<IActionResult> Gorevler(int id, CancellationToken ct)
    {
        if (!await db.Egitimler.AnyAsync(e => e.Id == id, ct)) return NotFound();
        var gorevler = await db.Gorevler.AsNoTracking().Where(g => g.EgitimId == id).OrderBy(g => g.No).ToListAsync(ct);
        return Ok(gorevler.Select(g => new { g.Id, g.EgitimId, g.No, g.Baslik, g.Aciklama, g.Tur, g.Aktif,
            g.BaslangicDosyasi, g.BaslangicKodu, kontrol = GorevBilgisi.Olustur(g).Kontrol }));
    }

    [HttpPost("gorevler"), RequestSizeLimit(1024 * 1024)]
    public Task<IActionResult> Ekle(int id, GorevFormu veri, CancellationToken ct) =>
        Kaydet(id, null, veri, ct);

    [HttpPut("gorevler/{gorevId:int:min(1)}"), RequestSizeLimit(1024 * 1024)]
    public Task<IActionResult> Guncelle(int id, int gorevId, GorevFormu veri, CancellationToken ct) =>
        Kaydet(id, gorevId, veri, ct);

    [HttpDelete("gorevler/{gorevId:int:min(1)}")]
    public async Task<IActionResult> Sil(int id, int gorevId, CancellationToken ct)
    {
        var gorev = await db.Gorevler.SingleOrDefaultAsync(g => g.EgitimId == id && g.Id == gorevId, ct);
        if (gorev is null) return NotFound();
        // Gönderim ve ilerleme bağlantıları görevin kimliğini kullanır.
        gorev.Aktif = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> Kaydet(int id, int? gorevId, GorevFormu veri, CancellationToken ct)
    {
        if (veri.EgitimId != id)
        {
            ModelState.AddModelError(nameof(veri.EgitimId), "Görev ve yol aynı eğitime ait olmalı.");
            return ValidationProblem(ModelState);
        }
        if (!await db.Egitimler.AnyAsync(e => e.Id == id, ct)) return NotFound();
        var gorevler = await db.Gorevler.Where(g => g.EgitimId == id).ToListAsync(ct);
        var gorev = gorevId is null ? new Gorev() : gorevler.Find(g => g.Id == gorevId);
        if (gorev is null) return NotFound();
        foreach (var hata in veri.Cakismalar(gorevler, gorevId))
            ModelState.AddModelError(hata.MemberNames.Single(), hata.ErrorMessage!);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        veri.Uygula(gorev);
        if (gorevId is null) db.Gorevler.Add(gorev);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Problem(statusCode: 409, title: "Görev başka bir değişiklikle çakıştı."); }
        return gorevId is null ? StatusCode(201, new { gorev.Id }) : NoContent();
    }
}
