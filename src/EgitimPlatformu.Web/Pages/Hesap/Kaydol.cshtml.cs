using System.ComponentModel.DataAnnotations;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgitimPlatformu.Web.Pages.Hesap;

[AllowAnonymous]
public class KaydolModel(IHesapServisi hesap) : HesapPageModel(hesap)
{
    [BindProperty]
    public KayitFormu Input { get; set; } = new();

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

        var sonuc = await Hesap.KaydolAsync(Input.Eposta.Trim(), Input.Sifre);
        if (!sonuc.Basarili)
        {
            ModelState.AddModelError(string.Empty, sonuc.Mesaj);
            return Page();
        }

        TempData["Bildirim"] = sonuc.Mesaj;
        return RedirectToPage("/Hesap/Giris", new { ReturnUrl = GuvenliDonusAdresi });
    }

    public sealed class KayitFormu
    {
        [Required(ErrorMessage = "E-posta adresinizi girin.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        [StringLength(254, ErrorMessage = "E-posta adresi en fazla 254 karakter olabilir.")]
        [Display(Name = "E-posta adresi")]
        public string Eposta { get; set; } = "";

        [Required(ErrorMessage = "Bir şifre belirleyin.")]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Şifreniz 8–128 karakter arasında olmalı.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Sifre { get; set; } = "";

        [Required(ErrorMessage = "Şifrenizi tekrar girin.")]
        [Compare(nameof(Sifre), ErrorMessage = "Şifreler eşleşmiyor.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre tekrarı")]
        public string SifreTekrar { get; set; } = "";
    }
}
