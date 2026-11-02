using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace EgitimPlatformu.Web.Services;

// Mobil yanıt biçimini korur; imzalama ve doğrulama ortak JWT altyapısındadır.
public sealed class MobilOturum(JwtServisi jwt, UserManager<IdentityUser> users)
{
    public bool Kullanilabilir => jwt.Anahtar() is not null;
    public async Task<object> Olustur(IdentityUser user, string adSoyad)
    {
        var token = jwt.Uret(user, await users.GetRolesAsync(user));
        return new { token = token.AccessToken, bitis = token.ExpiresAt,
            kullanici = new { id = user.Id, eposta = user.Email, adSoyad } };
    }
}

public static class MobilApiKaydi
{
    public static void MobilApiEkle(this IServiceCollection services)
    {
        services.AddScoped<MobilOturum>();
        services.AddSingleton<IMobilKodServisi, MobilKodServisi>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("mobil", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "yerel",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }
}
