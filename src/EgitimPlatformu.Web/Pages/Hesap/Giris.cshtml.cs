using System.ComponentModel.DataAnnotations;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgitimPlatformu.Web.Pages.Hesap;

[AllowAnonymous]
public class GirisModel(IHesapServisi hesap) : HesapPageModel(hesap)
{
    [BindProperty]
    public GirisFormu Input { get; set; } = new();

    public IActionResult OnGet()
    {
        DonusAdresiniDogrula();
        return User.Identity?.IsAuthenticated == true ? LocalRedirect(GuvenliDonusAdresi) : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        DonusAdresiniDogrula();
        if (!ModelState.IsValid) return Page();
        if (!AltyapiHazirMi()) return Page();

        var sonuc = await Hesap.GirisYapAsync(Input.Eposta.Trim(), Input.Sifre, Input.BeniHatirla);
        if (!sonuc.Basarili)
        {
            // Adaptör e-posta varlığı/parola farkını açıklamayan genel bir mesaj döndürmeli.
            ModelState.AddModelError(string.Empty, sonuc.Mesaj);
            return Page();
        }

        TempData["Bildirim"] = sonuc.Mesaj;
        return LocalRedirect(GuvenliDonusAdresi);
    }

    public sealed class GirisFormu
    {
        [Required(ErrorMessage = "E-posta adresinizi girin.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        [StringLength(254, ErrorMessage = "E-posta adresi en fazla 254 karakter olabilir.")]
        [Display(Name = "E-posta adresi")]
        public string Eposta { get; set; } = "";

        [Required(ErrorMessage = "Şifrenizi girin.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Sifre { get; set; } = "";

        [Display(Name = "Beni hatırla")]
        public bool BeniHatirla { get; set; }
    }
}
