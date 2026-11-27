using System.ComponentModel.DataAnnotations;
using System.Data;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Pages.Yonetim;

public class KullanicilarModel(UygulamaDbContext db, UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles, ILogger<KullanicilarModel> logger) : YonetimPageModel(db)
{
    [BindProperty(SupportsGet = true), Range(1, 1000000)] public int Sayfa { get; set; } = 1;
    [BindProperty] public RolFormu Input { get; set; } = new();
    public const int Boyut = 20;
    public int Toplam { get; private set; }
    public bool Hazir { get; private set; } = true;
    public IReadOnlyList<KullaniciSatiri> Kayitlar { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        // Rol formu yalnız POST'ta doğrulanır.
        ModelState.Remove("Input.KullaniciId"); ModelState.Remove("Input.Rol");
        if (!ModelState.IsValid) return BadRequest();
        await Listele(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) { await Listele(ct); return Page(); }
        try
        {
            // Son yönetici kontrolü ve tüm Identity yazmaları aynı transaction'dadır.
            // PostgreSQL Serializable, eşzamanlı yönetici düşürmelerindeki write-skew'i engeller.
            await using var transaction = await Db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var user = await users.FindByIdAsync(Input.KullaniciId);
            if (user is null) return NotFound();
            if (!await roles.RoleExistsAsync(Input.Rol))
                ModelState.AddModelError("", "Seçilen rol henüz hazırlanmamış. Identity kurulumunu kontrol edin.");
            else
            {
                var mevcutRoller = await users.GetRolesAsync(user);
                var kaldirilacak = mevcutRoller.Where(r => (r == Roller.Yonetici || r == Roller.Ogrenci) && r != Input.Rol).ToArray();
                if (kaldirilacak.Contains(Roller.Yonetici) && (await users.GetUsersInRoleAsync(Roller.Yonetici)).Count <= 1)
                    ModelState.AddModelError("", "Son yöneticinin yetkisi kaldırılamaz. Önce başka bir yönetici atayın.");
                else if (kaldirilacak.Length == 0 && mevcutRoller.Contains(Input.Rol))
                {
                    TempData["Bildirim"] = "Kullanıcı zaten seçilen rolde.";
                    return RedirectToPage(new { Sayfa });
                }
                else
                {
                    if (!mevcutRoller.Contains(Input.Rol)) SonucuKontrolEt(await users.AddToRoleAsync(user, Input.Rol));
                    if (ModelState.IsValid && kaldirilacak.Length > 0) SonucuKontrolEt(await users.RemoveFromRolesAsync(user, kaldirilacak));
                    if (ModelState.IsValid) SonucuKontrolEt(await users.UpdateSecurityStampAsync(user));
                    if (ModelState.IsValid)
                    {
                        await transaction.CommitAsync(ct);
                        TempData["Bildirim"] = "Kullanıcının rolü güncellendi. Önceki oturumları geçersiz kılındı; yeniden giriş yapmalı.";
                        return RedirectToPage(new { Sayfa });
                    }
                }
            }
            await transaction.RollbackAsync(ct);
        }
        catch (Exception ex) when (ex is DbUpdateException || GonderimGecmisiHatasi.VeritabaniHatasiMi(ex))
        {
            logger.LogWarning(ex, "Kullanıcı rolü güncellenemedi.");
            ModelState.AddModelError("", "Rol güncellenemedi. Eşzamanlı bir değişiklik veya bağlantı sorunu olabilir; sayfayı yenileyip yeniden deneyin.");
        }
        Db.ChangeTracker.Clear();
        await Listele(ct);
        return Page();
    }

    private void SonucuKontrolEt(IdentityResult sonuc)
    {
        if (!sonuc.Succeeded) ModelState.AddModelError("", "Rol işlemi kaydedilemedi. Sayfayı yenileyip yeniden deneyin.");
    }

    private async Task Listele(CancellationToken ct)
    {
        try
        {
            Toplam = await users.Users.CountAsync(ct);
            var sayfa = await users.Users.AsNoTracking().OrderBy(u => u.Email).ThenBy(u => u.Id)
                .Skip((Math.Max(1, Sayfa) - 1) * Boyut).Take(Boyut).ToListAsync(ct);
            var kimlikler = sayfa.Select(u => u.Id).ToArray();
            var kullaniciRolleri = await (from ur in Db.UserRoles join r in Db.Roles on ur.RoleId equals r.Id
                where kimlikler.Contains(ur.UserId) select new { ur.UserId, r.Name }).ToListAsync(ct);
            Kayitlar = sayfa.Select(u => new KullaniciSatiri(u.Id, u.Email ?? u.UserName ?? "Hesap",
                kullaniciRolleri.Where(r => r.UserId == u.Id).Select(r => r.Name!).Order().ToArray())).ToArray();
        }
        catch (Exception ex) when (GonderimGecmisiHatasi.VeritabaniHatasiMi(ex))
        {
            logger.LogWarning(ex, "Kullanıcı listesi okunamadı.");
            Hazir = false; Response.StatusCode = 503;
        }
    }

    public sealed record KullaniciSatiri(string Id, string Eposta, IReadOnlyList<string> Roller);
    public sealed class RolFormu
    {
        [Required(ErrorMessage = "Kullanıcı zorunludur."), StringLength(450)] public string KullaniciId { get; set; } = "";
        [Required(ErrorMessage = "Rol seçin."), RegularExpression("^(" + Roller.Yonetici + "|" + Roller.Ogrenci + ")$", ErrorMessage = "Geçerli bir rol seçin.")]
        public string Rol { get; set; } = "";
    }
}
