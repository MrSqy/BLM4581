using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EgitimPlatformu.Web.Controllers;

public sealed record MobilGiris([Required, EmailAddress, StringLength(254)] string Eposta, [Required, StringLength(256)] string Sifre);
public sealed record MobilKayit([Required, StringLength(100)] string AdSoyad,
    [Required, EmailAddress, StringLength(254)] string Eposta, [Required, StringLength(256, MinimumLength = 8)] string Sifre);

[ApiController, Route("api/mobil/hesap"), EnableRateLimiting("mobil"), RequestSizeLimit(4096)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MobilHesapController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn,
    RoleManager<IdentityRole> roles, UygulamaDbContext db, MobilOturum tokens) : ControllerBase
{
    [HttpPost("kayit"), AllowAnonymous]
    public async Task<IActionResult> Kayit(MobilKayit input)
    {
        if (!tokens.Kullanilabilir) return StatusCode(503, new { mesaj = "Mobil giriş şu anda kullanılamıyor." });
        if (string.IsNullOrWhiteSpace(input.AdSoyad)) return BadRequest(new { mesaj = "Ad soyad zorunludur." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new IdentityUser { UserName = input.Eposta.Trim(), Email = input.Eposta.Trim() };
        var result = await users.CreateAsync(user, input.Sifre);
        if (!result.Succeeded) return BadRequest(new { mesaj = "Kayıt tamamlanamadı. E-postanızı ve şifre kurallarını kontrol edin." });
        if (!await roles.RoleExistsAsync(Roller.Ogrenci))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole(Roller.Ogrenci));
            if (!roleResult.Succeeded) return StatusCode(503, new { mesaj = "Kayıt şu anda kullanılamıyor." });
        }
        var role = await users.AddToRoleAsync(user, Roller.Ogrenci);
        var claim = await users.AddClaimAsync(user, new Claim("adSoyad", input.AdSoyad.Trim()));
        if (!role.Succeeded || !claim.Succeeded) return StatusCode(503, new { mesaj = "Kayıt tamamlanamadı." });
        await transaction.CommitAsync();
        return Ok(await tokens.Olustur(user, input.AdSoyad.Trim()));
    }

    [HttpPost("giris"), AllowAnonymous]
    public async Task<IActionResult> Giris(MobilGiris input)
    {
        if (!tokens.Kullanilabilir) return StatusCode(503, new { mesaj = "Mobil giriş şu anda kullanılamıyor." });
        var user = await users.FindByEmailAsync(input.Eposta.Trim());
        if (user == null || await users.GetTwoFactorEnabledAsync(user) || !(await signIn.CheckPasswordSignInAsync(user, input.Sifre, lockoutOnFailure: true)).Succeeded)
            return Unauthorized(new { mesaj = "E-posta veya şifre hatalı ya da hesap geçici olarak kilitli." });
        var name = (await users.GetClaimsAsync(user)).FirstOrDefault(c => c.Type == "adSoyad")?.Value ?? user.Email!;
        return Ok(await tokens.Olustur(user, name));
    }

    [HttpGet("ben")]
    public async Task<IActionResult> Ben()
    {
        var user = await users.FindByIdAsync(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var name = (await users.GetClaimsAsync(user!)).FirstOrDefault(c => c.Type == "adSoyad")?.Value ?? user!.Email;
        return Ok(new { id = user!.Id, eposta = user.Email, adSoyad = name });
    }

    [HttpPost("cikis")]
    public async Task<IActionResult> Cikis()
    {
        var user = await users.FindByIdAsync(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        // Çıkış, bu hesaba ait eski mobil tokenları da iptal eder.
        var result = await users.UpdateSecurityStampAsync(user!);
        return result.Succeeded ? NoContent() : StatusCode(503, new { mesaj = "Oturum kapatılamadı." });
    }
}
