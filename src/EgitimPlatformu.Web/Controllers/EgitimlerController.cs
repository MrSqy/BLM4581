using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EgitimPlatformu.Web.Controllers;

[ApiController, Route("api/egitimler")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EgitimlerController(UygulamaDbContext db, DersErisimi erisim) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EgitimSayfasi>> Liste([FromQuery] EgitimSorgusu sorgu, CancellationToken ct)
    {
        var query = db.Egitimler.AsNoTracking();
        if (sorgu.KategoriId.HasValue) query = query.Where(e => e.KategoriId == sorgu.KategoriId);
        if (!string.IsNullOrWhiteSpace(sorgu.Q))
        {
            var arama = sorgu.Q.Trim().ToLowerInvariant();
            query = query.Where(e => e.Kod.ToLower().Contains(arama) || e.Baslik.ToLower().Contains(arama));
        }
        var toplam = await query.CountAsync(ct);
        var dersler = await query.OrderBy(e => e.Sira).ThenBy(e => e.Id)
            .Skip((sorgu.Sayfa - 1) * sorgu.SayfaBoyutu).Take(sorgu.SayfaBoyutu)
            .Select(e => new EgitimOzet(e.Id, e.KategoriId, e.Kod, e.Baslik, e.Sira)).ToListAsync(ct);
        return new EgitimSayfasi(toplam, sorgu.Sayfa, sorgu.SayfaBoyutu, dersler);
    }
    [HttpGet("{id:int}")]
    [ProducesResponseType<EgitimDetay>(200), ProducesResponseType(404)]
    public async Task<ActionResult<EgitimDetay>> Detay(int id, CancellationToken ct)
    {
        var ders = await db.Egitimler.AsNoTracking().Include(e => e.Gorevler).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (ders is null) return NotFound();
        if (!await erisim.Izinli(HttpContext, ders, ct)) return Unauthorized();
        return DetayDto(ders);
    }
    [HttpPost, Authorize(Policy = "ApiYonetici"), RequestSizeLimit(1024 * 1024)]
    [ProducesResponseType<EgitimDetay>(201), ProducesResponseType(400), ProducesResponseType(409)]
    public async Task<IActionResult> Ekle(EgitimYazma veri, CancellationToken ct)
    {
        var hata = await Dogrula(veri, null, ct);
        if (hata is not null) return hata;
        var ders = new Egitim(); Uygula(ders, veri); db.Egitimler.Add(ders);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Cakisma(); }
        return CreatedAtAction(nameof(Detay), new { id = ders.Id }, DetayDto(ders));
    }
    [HttpPut("{id:int}"), Authorize(Policy = "ApiYonetici"), RequestSizeLimit(1024 * 1024)]
    [ProducesResponseType(204), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<IActionResult> Guncelle(int id, EgitimYazma veri, CancellationToken ct)
    {
        var ders = await db.Egitimler.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (ders is null) return NotFound();
        var hata = await Dogrula(veri, id, ct);
        if (hata is not null) return hata;
        Uygula(ders, veri);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Cakisma(); }
        return NoContent();
    }
    [HttpDelete("{id:int}"), Authorize(Policy = "ApiYonetici")]
    [ProducesResponseType(204), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<IActionResult> Sil(int id, CancellationToken ct)
    {
        var ders = await db.Egitimler.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (ders is null) return NotFound();
        db.Egitimler.Remove(ders);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Cakisma(); }
        return NoContent();
    }
    private async Task<IActionResult?> Dogrula(EgitimYazma veri, int? id, CancellationToken ct)
    {
        if (!await db.Kategoriler.AnyAsync(k => k.Id == veri.KategoriId, ct))
        {
            ModelState.AddModelError(nameof(veri.KategoriId), "Kategori bulunamadı.");
            return ValidationProblem(ModelState);
        }
        if (await db.Egitimler.AnyAsync(e => e.Kod == veri.Kod && e.Id != id, ct)) return Cakisma();
        return null;
    }
    private ObjectResult Cakisma() => Problem(statusCode: 409, title: "Ders kaydedilemedi. Ders kodu veya ilişkili kayıtlar çakışıyor.");
    private static void Uygula(Egitim ders, EgitimYazma veri)
    {
        ders.KategoriId = veri.KategoriId; ders.Kod = veri.Kod; ders.Baslik = veri.Baslik;
        ders.Sira = veri.Sira; ders.IcerikMarkdown = veri.IcerikMarkdown;
        ders.BaslangicKodu = veri.BaslangicKodu; ders.TestKodu = veri.TestKodu;
    }
    private static EgitimDetay DetayDto(Egitim e) => new(e.Id, e.KategoriId, e.Kod, e.Baslik, e.Sira,
        e.IcerikMarkdown, e.BaslangicKodu, e.Gorevler.OrderBy(g => g.No).ThenBy(g => g.Id)
            .Select(g => new GorevDetay(g.Id, g.No, g.Baslik, g.Aciklama)).ToList());
}
