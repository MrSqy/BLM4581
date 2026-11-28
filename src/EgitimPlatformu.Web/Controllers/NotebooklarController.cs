using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Controllers;

[ApiController, Route("api/notebooklar"), EnableRateLimiting("mobil")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(2 * 1024 * 1024)]
public sealed class NotebooklarController(UygulamaDbContext db) : ControllerBase
{
    private string KullaniciId => User.FindFirst("sub")!.Value;
    private bool Yonetici => User.IsInRole(Roller.Yonetici);

    [HttpGet]
    public async Task<ActionResult<List<NotebookOzeti>>> Liste(CancellationToken ct,
        [FromQuery, Range(1, int.MaxValue)] int? egitimId = null,
        [FromQuery, Range(1, 1000000)] int sayfa = 1,
        [FromQuery, Range(1, 100)] int sayfaBoyutu = 20,
        [FromQuery] bool kisisel = false)
    {
        var sorgu = Okunabilir();
        if (egitimId.HasValue) sorgu = sorgu.Where(n => n.EgitimId == egitimId);
        if (kisisel) sorgu = sorgu.Where(n => n.EgitimId == null);
        return await sorgu.OrderByDescending(n => n.GuncellendiUtc).ThenByDescending(n => n.Id)
            .Skip((sayfa - 1) * sayfaBoyutu).Take(sayfaBoyutu)
            .Select(n => new NotebookOzeti(n.Id, n.Baslik, n.EgitimId, n.Surum, n.GuncellendiUtc))
            .ToListAsync(ct);
    }

    [HttpGet("{id:int:min(1)}")]
    public async Task<ActionResult<NotebookDetayi>> Detay(int id, CancellationToken ct)
    {
        var notebook = await Okunabilir().SingleOrDefaultAsync(n => n.Id == id, ct);
        return notebook is null ? NotFound() : Donustur(notebook);
    }

    [HttpPost]
    public async Task<ActionResult<NotebookDetayi>> Olustur(NotebookOlusturIstegi istek, CancellationToken ct)
    {
        if (istek.EgitimId.HasValue && !Yonetici) return Forbid(JwtBearerDefaults.AuthenticationScheme);
        if (!Gecerli(istek.Icerik)) return GecersizBelge();
        if (istek.EgitimId is { } ders && !await db.Egitimler.AnyAsync(e => e.Id == ders, ct))
            return NotFound(new { mesaj = "Ders bulunamadı." });

        var notebook = new Notebook
        {
            KullaniciId = KullaniciId, EgitimId = istek.EgitimId, Baslik = istek.Baslik.Trim(),
            IcerikJson = istek.Icerik.GetRawText(), GuncellendiUtc = DateTime.UtcNow
        };
        db.Notebooklar.Add(notebook);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Detay), new { id = notebook.Id }, Donustur(notebook));
    }

    [HttpPut("{id:int:min(1)}")]
    public async Task<IActionResult> Guncelle(int id, NotebookGuncelleIstegi istek, CancellationToken ct)
    {
        var sorgu = Duzenlenebilir(id);
        if (!Gecerli(istek.Icerik)) return GecersizBelge();
        var baslik = istek.Baslik.Trim();
        var icerik = istek.Icerik.GetRawText();
        var zaman = DateTime.UtcNow;
        var degisen = await sorgu.Where(n => n.Surum == istek.Surum).ExecuteUpdateAsync(yaz => yaz
            .SetProperty(n => n.Baslik, baslik).SetProperty(n => n.IcerikJson, icerik)
            .SetProperty(n => n.Surum, n => n.Surum + 1).SetProperty(n => n.GuncellendiUtc, zaman), ct);
        if (degisen > 0) return NoContent();
        return await sorgu.AnyAsync(ct)
            ? Conflict(new { mesaj = "Notebook güncellendi. Yeniden açıp son sürümü yükleyin." })
            : NotFound();
    }

    [HttpDelete("{id:int:min(1)}")]
    public async Task<IActionResult> Sil(int id, CancellationToken ct) =>
        await Duzenlenebilir(id).ExecuteDeleteAsync(ct) > 0 ? NoContent() : NotFound();

    private IQueryable<Notebook> Okunabilir()
    {
        var kullanici = KullaniciId;
        return db.Notebooklar.AsNoTracking().Where(n => n.EgitimId.HasValue || n.KullaniciId == kullanici);
    }

    private IQueryable<Notebook> Duzenlenebilir(int id)
    {
        var kullanici = KullaniciId;
        return db.Notebooklar.Where(n => n.Id == id &&
            (n.EgitimId.HasValue ? Yonetici : n.KullaniciId == kullanici));
    }

    private static bool Gecerli(JsonElement belge) =>
        belge.ValueKind == JsonValueKind.Object &&
        belge.TryGetProperty("nbformat", out var bicim) && bicim.ValueKind == JsonValueKind.Number &&
        bicim.TryGetInt32(out var ana) && ana == 4 &&
        belge.TryGetProperty("nbformat_minor", out var altBicim) && altBicim.ValueKind == JsonValueKind.Number &&
        altBicim.TryGetInt32(out var alt) && alt >= 0 &&
        belge.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object &&
        belge.TryGetProperty("cells", out var hucreler) && hucreler.ValueKind == JsonValueKind.Array &&
        hucreler.EnumerateArray().All(hucre => hucre.ValueKind == JsonValueKind.Object);

    private BadRequestObjectResult GecersizBelge() =>
        BadRequest(new { mesaj = "Geçerli bir nbformat 4 notebook belgesi gönderin." });

    private static NotebookDetayi Donustur(Notebook n) =>
        new(n.Id, n.Baslik, n.EgitimId, n.Surum, n.GuncellendiUtc,
            JsonSerializer.Deserialize<JsonElement>(n.IcerikJson));
}
