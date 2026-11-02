using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Services;

public sealed class DersErisimi(UygulamaDbContext db)
{
    public async Task<bool> IlkDers(Egitim ders, CancellationToken ct = default) =>
        await db.Egitimler.AsNoTracking().Where(e => e.KategoriId == ders.KategoriId)
            .OrderBy(e => e.Sira).ThenBy(e => e.Id).Select(e => e.Id).FirstOrDefaultAsync(ct) == ders.Id;

    public async Task<bool> Izinli(HttpContext context, Egitim ders, CancellationToken ct = default) =>
        await IlkDers(ders, ct) || (await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)).Succeeded;
}
