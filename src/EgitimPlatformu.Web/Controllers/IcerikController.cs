using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Controllers;

[ApiController]
[Route("api/icerik")]
[Authorize(Roles = Roller.Yonetici)]
public sealed class IcerikController(IcerikAktarimServisi aktarim) : ControllerBase
{
    [HttpGet("antiforgery")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Token([FromServices] IAntiforgery antiforgery)
        => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("aktar")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Aktar(IcerikPaketi paket, CancellationToken cancellationToken)
    {
        var hata = IcerikAktarimServisi.TutarlilikHatasi(paket);
        if (hata is not null)
        {
            ModelState.AddModelError(nameof(paket.Kategoriler), hata);
            return ValidationProblem(ModelState);
        }
        try
        {
            return Ok(await aktarim.AktarAsync(paket, cancellationToken));
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "İçerik kaydedilemedi. Paket bütünüyle geri alındı; veriyi kontrol edip yeniden deneyin.");
        }
    }
}
