using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Controllers;

[ApiController, Route("api/egitimler/{id:int:min(1)}/gorevler")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GorevlerController(UygulamaDbContext db, DersErisimi erisim) : ControllerBase
{
    [HttpGet, IstegeBagliBearer]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IReadOnlyList<GorevBilgisi>>> Liste(int id, CancellationToken ct)
    {
        var ders = await db.Egitimler.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (ders is null) return NotFound();
        if (!await erisim.Izinli(HttpContext, ders, ct)) return Unauthorized();

        var gorevler = await db.Gorevler.AsNoTracking().Where(g => g.EgitimId == id && g.Aktif)
            .OrderBy(g => g.No).ThenBy(g => g.Id).ToListAsync(ct);
        return gorevler.Select(GorevBilgisi.Olustur).ToArray();
    }

    [HttpGet("{no:int:min(1)}"), IstegeBagliBearer]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GorevBilgisi>> Detay(int id, int no, CancellationToken ct)
    {
        var ders = await db.Egitimler.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (ders is null) return NotFound();
        if (!await erisim.Izinli(HttpContext, ders, ct)) return Unauthorized();

        var gorev = await db.Gorevler.AsNoTracking()
            .SingleOrDefaultAsync(g => g.EgitimId == id && g.No == no && g.Aktif, ct);
        return gorev is null ? NotFound() : GorevBilgisi.Olustur(gorev);
    }
}
