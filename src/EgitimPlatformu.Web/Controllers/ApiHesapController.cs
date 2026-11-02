using System.ComponentModel.DataAnnotations;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace EgitimPlatformu.Web.Controllers;

public sealed class TokenGiris
{
    [Required, EmailAddress, StringLength(254)] public string Eposta { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 1)] public string Sifre { get; set; } = "";
}
[ApiController, Route("api/hesap"), EnableRateLimiting("mobil")]
public sealed class ApiHesapController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signin, JwtServisi jwt) : ControllerBase
{
    [HttpPost("giris"), AllowAnonymous, RequestSizeLimit(4096)]
    [ProducesResponseType<TokenYaniti>(200), ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(503)]
    public async Task<IActionResult> Giris(TokenGiris veri)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        if (jwt.Anahtar() is null) return Problem(statusCode: 503, title: "Mobil giriş şu anda kullanılamıyor.");
        var user = await users.FindByEmailAsync(veri.Eposta.Trim());
        if (user is null) return GirisHatasi();
        var sonuc = await signin.CheckPasswordSignInAsync(user, veri.Sifre, lockoutOnFailure: true);
        if (!sonuc.Succeeded || await users.GetTwoFactorEnabledAsync(user)) return GirisHatasi();
        return Ok(jwt.Uret(user, await users.GetRolesAsync(user)));
    }
    private ObjectResult GirisHatasi() => Problem(statusCode: 401, title: "E-posta veya şifre hatalı ya da hesap girişe uygun değil.");
}
