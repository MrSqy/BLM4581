using System.ComponentModel.DataAnnotations;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EgitimPlatformu.Web.Controllers;

[ApiController, Route("api/gonderimler"), EnableRateLimiting("mobil")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GonderimlerController(GonderimServisi gonderimler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GonderimListeYaniti>> Liste(CancellationToken ct,
        [FromQuery, Range(1, 1000000)] int sayfa = 1,
        [FromQuery, Range(1, 100)] int sayfaBoyutu = 20)
    {
        var kullaniciId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(kullaniciId)) return Unauthorized();
        var sonuc = await gonderimler.ListeAsync(kullaniciId, sayfa, sayfaBoyutu, ct);
        return new GonderimListeYaniti(sonuc.Toplam, sayfa, sayfaBoyutu,
            sonuc.Kayitlar.Select(GonderimOzeti.Olustur).ToArray());
    }

    [HttpGet("{id:int:min(1)}")]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GonderimDetayYaniti>> Detay(int id, CancellationToken ct)
    {
        var kullaniciId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(kullaniciId)) return Unauthorized();
        var g = await gonderimler.DetayAsync(kullaniciId, id, ct);
        return g is null ? NotFound() : new GonderimDetayYaniti(GonderimOzeti.Olustur(g), g.Kod, g.Cikti, g.Hata);
    }
}
