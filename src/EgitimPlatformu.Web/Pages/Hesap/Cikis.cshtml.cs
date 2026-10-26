using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EgitimPlatformu.Web.Pages.Hesap;

public class CikisModel(IHesapServisi hesap) : HesapPageModel(hesap)
{
    public void OnGet() => DonusAdresiniDogrula(); // GET hiçbir oturumu değiştirmez.

    public async Task<IActionResult> OnPostAsync()
    {
        DonusAdresiniDogrula();
        if (User.Identity?.IsAuthenticated != true)
        {
            TempData["Bildirim"] = "Açık bir oturum bulunmuyor.";
            return LocalRedirect(GuvenliDonusAdresi);
        }
        if (!AltyapiHazirMi()) return Page();

        var sonuc = await Hesap.CikisYapAsync();
        if (!sonuc.Basarili)
        {
            ModelState.AddModelError(string.Empty, sonuc.Mesaj);
            return Page();
        }
        TempData["Bildirim"] = sonuc.Mesaj;
        return LocalRedirect(GuvenliDonusAdresi);
    }
}
