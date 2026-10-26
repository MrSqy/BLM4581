using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;

namespace EgitimPlatformu.Web.Data;

public static class IdentityBaslangic
{
    // Açık komutla çalışır; normal web başlangıcı veritabanını değiştirmez.
    public static async Task HazirlaAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var roller = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var kullanicilar = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var eposta = configuration["BaslangicYoneticisi:Eposta"];
        var sifre = configuration["BaslangicYoneticisi:Sifre"];
        if (string.IsNullOrWhiteSpace(eposta) || string.IsNullOrWhiteSpace(sifre))
            throw new InvalidOperationException("Başlangıç yöneticisinin e-posta ve şifresini user-secrets veya ortam değişkenleriyle tanımlayın.");
        var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        await using var islem = await db.Database.BeginTransactionAsync();
        foreach (var rol in new[] { Roller.Yonetici, Roller.Ogrenci })
            if (!await roller.RoleExistsAsync(rol)) Dogrula(await roller.CreateAsync(new IdentityRole(rol)));

        var kullanici = await kullanicilar.FindByEmailAsync(eposta);
        if (kullanici is null)
        {
            kullanici = new IdentityUser { UserName = eposta, Email = eposta };
            Dogrula(await kullanicilar.CreateAsync(kullanici, sifre));
            Dogrula(await kullanicilar.AddToRoleAsync(kullanici, Roller.Yonetici));
        }
        else if (!await kullanicilar.IsInRoleAsync(kullanici, Roller.Yonetici))
        {
            // Açık kayıttan gelen bir hesabı sessizce yöneticiye yükseltmeyiz.
            throw new InvalidOperationException("Bu e-posta ile yönetici olmayan bir hesap var. Yeni bir başlangıç yöneticisi e-postası seçin.");
        }
        await islem.CommitAsync();
    }

    private static void Dogrula(IdentityResult sonuc)
    {
        if (!sonuc.Succeeded)
            throw new InvalidOperationException("Identity başlangıç işlemi başarısız: " + string.Join(", ", sonuc.Errors.Select(e => e.Code)));
    }
}
