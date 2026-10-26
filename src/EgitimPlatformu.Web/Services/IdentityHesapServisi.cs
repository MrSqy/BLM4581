using EgitimPlatformu.Web.Data;
using Microsoft.AspNetCore.Identity;

namespace EgitimPlatformu.Web.Services;

public sealed class IdentityHesapServisi(
    UserManager<IdentityUser> kullanicilar,
    SignInManager<IdentityUser> oturum,
    UygulamaDbContext db) : IHesapServisi
{
    public bool Kullanilabilir => true;

    public async Task<HesapSonucu> KaydolAsync(string eposta, string sifre)
    {
        // Kullanıcı ve rol ataması tek işlem: yarım kayıt bırakılmaz.
        await using var islem = await db.Database.BeginTransactionAsync();
        var kullanici = new IdentityUser { UserName = eposta, Email = eposta };
        var sonuc = await kullanicilar.CreateAsync(kullanici, sifre);
        if (!sonuc.Succeeded)
            return new(false, "Kayıt tamamlanamadı. E-posta adresinizi ve şifre kurallarını kontrol edin.");
        sonuc = await kullanicilar.AddToRoleAsync(kullanici, Roller.Ogrenci);
        if (!sonuc.Succeeded)
            return new(false, "Kayıt tamamlanamadı. Lütfen daha sonra yeniden deneyin.");
        await islem.CommitAsync();
        return new(true, "Hesabın oluşturuldu. Giriş yapabilirsin.");
    }

    public async Task<HesapSonucu> GirisYapAsync(string eposta, string sifre, bool beniHatirla)
    {
        var sonuc = await oturum.PasswordSignInAsync(eposta, sifre, beniHatirla, lockoutOnFailure: true);
        return sonuc.Succeeded
            ? new(true, "Oturum açıldı.")
            : new(false, "E-posta veya şifre hatalı ya da hesap geçici olarak kilitli.");
    }

    public async Task<HesapSonucu> CikisYapAsync()
    {
        await oturum.SignOutAsync();
        return new(true, "Oturum kapatıldı.");
    }
}
