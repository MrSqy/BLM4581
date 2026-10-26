namespace EgitimPlatformu.Web.Services;

/// <summary>
/// 2.3 sayfalarının 2.1 Identity altyapısıyla entegrasyon noktası.
/// Uygulama UserManager/SignInManager kullanmalı; parolaları kendisi saklamamalı.
/// </summary>
public interface IHesapServisi
{
    bool Kullanilabilir { get; }
    Task<HesapSonucu> KaydolAsync(string eposta, string sifre);
    Task<HesapSonucu> GirisYapAsync(string eposta, string sifre, bool beniHatirla);
    Task<HesapSonucu> CikisYapAsync();
}

public sealed record HesapSonucu(bool Basarili, string Mesaj);

/// <summary>Identity eksikken hiçbir hesap/oturum oluşturmayan açık bekleme durumu.</summary>
public sealed class BekleyenHesapServisi : IHesapServisi
{
    public const string Mesaj = "Üyelik altyapısı henüz hazır değil. Şu anda kayıt, giriş ve çıkış işlemleri kullanılamıyor.";
    public bool Kullanilabilir => false;
    public Task<HesapSonucu> KaydolAsync(string eposta, string sifre) => Bekliyor();
    public Task<HesapSonucu> GirisYapAsync(string eposta, string sifre, bool beniHatirla) => Bekliyor();
    public Task<HesapSonucu> CikisYapAsync() => Bekliyor();
    private static Task<HesapSonucu> Bekliyor() => Task.FromResult(new HesapSonucu(false, Mesaj));
}
