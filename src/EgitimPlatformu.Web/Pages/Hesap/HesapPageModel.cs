using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EgitimPlatformu.Web.Pages.Hesap;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public abstract class HesapPageModel(IHesapServisi hesap) : PageModel
{
    protected IHesapServisi Hesap { get; } = hesap;
    public bool HesapKullanilabilir => Hesap.Kullanilabilir;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    protected string GuvenliDonusAdresi => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Page("/Egitimler/Index")!;

    protected void DonusAdresiniDogrula() => ReturnUrl = GuvenliDonusAdresi;

    protected bool AltyapiHazirMi()
    {
        if (HesapKullanilabilir) return true;
        ModelState.AddModelError(string.Empty, BekleyenHesapServisi.Mesaj);
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return false;
    }
}
