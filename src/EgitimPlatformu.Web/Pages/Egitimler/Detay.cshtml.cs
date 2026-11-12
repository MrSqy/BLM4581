using System.Data.Common;
using System.Security.Claims;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using EgitimPlatformu.Web.Controllers;
using Microsoft.AspNetCore.RateLimiting;

namespace EgitimPlatformu.Web.Pages.Egitimler;

[EnableRateLimiting("mobil")]
[RequestSizeLimit(256 * 1024)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DetayModel(UygulamaDbContext db, MarkdownServisi markdown, ILogger<DetayModel> logger,
    DersErisimi erisim, IGorevKodServisi gorevRunner, IMobilKodServisi dersRunner, GonderimServisi gonderimler) : PageModel
{
    public Egitim? Egitim { get; private set; }
    public string IcerikHtml { get; private set; } = "";
    public List<GorevIcerigi> Gorevler { get; private set; } = [];
    public string? Hata { get; private set; }
    public bool CalistirmayaIzinVar { get; private set; }
    public bool GorevBazli => Gorevler.Any(g => g.Tur != "eski-pytest");

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || id is null or <= 0) return Bulunamadi();
        try
        {
            Egitim = await db.Egitimler.AsNoTracking().Include(e => e.Kategori).Include(e => e.Gorevler)
                .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (Egitim is null) return Bulunamadi();
            IcerikHtml = markdown.Html(Egitim.IcerikMarkdown);
            Gorevler = Egitim.Gorevler.Where(g => g.Aktif).OrderBy(g => g.No).ThenBy(g => g.Id)
                .Select(g => new GorevIcerigi(g.Id, g.No, g.Baslik, markdown.Html(g.Aciklama),
                    g.Tur, g.BaslangicKodu, g.Tur == "kod" && g.KontrolJson != null)).ToList();
            CalistirmayaIzinVar = User.Identity?.IsAuthenticated == true || await erisim.IlkDers(Egitim, cancellationToken);
        }
        catch (Exception ex) when (ex is DbException || ex is InvalidOperationException { InnerException: DbException })
        {
            logger.LogError(ex, "Ders okunamadı: {EgitimId}", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            Hata = "Ders içeriğine şu anda ulaşılamıyor. Lütfen daha sonra yeniden deneyin.";
        }
        return Page();
    }

    private IActionResult Bulunamadi()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        Hata = "Aradığın ders bulunamadı.";
        return Page();
    }

    // Cookie + Razor antiforgery adaptörü. Mobil controller/JWT ve sandbox değiştirilmez.
    // İstek DTO'su ve sonuçlar mevcut çalıştırma servislerinin sözleşmesidir.
    public Task<IActionResult> OnPostCalistirAsync(int? id, int? no, [FromBody] MobilKodIstegi? input, CancellationToken ct)
        => KoduGonderAsync(id, no, input, false, ct);

    public Task<IActionResult> OnPostTestAsync(int? id, int? no, [FromBody] MobilKodIstegi? input, CancellationToken ct)
        => KoduGonderAsync(id, no, input, true, ct);

    private async Task<IActionResult> KoduGonderAsync(int? id, int? no, MobilKodIstegi? input, bool test, CancellationToken ct)
    {
        if (id is null or <= 0) return NotFound(new { mesaj = "Ders bulunamadı." });
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input?.Kod) || input.Kod.Length > 32000)
            return BadRequest(new { mesaj = "1–32000 karakter arasında çalıştırılacak kod yazın; görev numarasını kontrol edin." });
        try
        {
            var ders = await db.Egitimler.AsNoTracking().Include(e => e.Gorevler).SingleOrDefaultAsync(e => e.Id == id, ct);
            if (ders is null) return NotFound(new { mesaj = "Ders bulunamadı." });
            if (User.Identity?.IsAuthenticated != true && !await erisim.IlkDers(ders, ct))
                return StatusCode(401, new { mesaj = "Bu derste kod çalıştırmak için oturum açın." });

            var aktifGorevler = ders.Gorevler.Where(g => g.Aktif).ToArray();
            if (aktifGorevler.Any(g => g.Tur != "eski-pytest"))
            {
                var gorev = aktifGorevler.SingleOrDefault(g => g.No == no);
                if (gorev is null) return NotFound(new { mesaj = "Aktif görev bulunamadı." });
                if (gorev.Tur != "kod" || gorev.KontrolJson is null)
                    return StatusCode(409, new { mesaj = "Bu görevde otomatik program kontrolü yok." });
                var baslangic = System.Diagnostics.Stopwatch.GetTimestamp();
                var sonuc = await gorevRunner.CalistirAsync(gorev, input.Kod, test, ct);
                var sure = System.Diagnostics.Stopwatch.GetElapsedTime(baslangic);
                if (!test) return new JsonResult(sonuc);
                var gonderimId = await gonderimler.KaydetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier),
                    ders, gorev, input.Kod, new(sonuc.Basarili, sonuc.Durum, sonuc.Cikti, sonuc.Hata, sonuc.CikisKodu), sure, ct);
                return new JsonResult(sonuc with { GonderimId = gonderimId });
            }

            if (no.HasValue) return StatusCode(409, new { mesaj = "Bu derste dersin tamamı için kod yazın." });
            if (test && string.IsNullOrWhiteSpace(ders.TestKodu))
                return StatusCode(409, new { mesaj = "Bu derse henüz otomatik test eklenmemiş." });
            var dersBaslangic = System.Diagnostics.Stopwatch.GetTimestamp();
            var dersSonucu = await dersRunner.CalistirAsync(input.Kod, ders.TestKodu, test,
                aktifGorevler.Where(g => g.No.HasValue).Select(g => g.No!.Value).ToArray(), ct);
            var dersSuresi = System.Diagnostics.Stopwatch.GetElapsedTime(dersBaslangic);
            if (!test) return new JsonResult(dersSonucu);
            var dersGonderimId = await gonderimler.KaydetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier),
                ders, null, input.Kod, new(dersSonucu.Basarili, dersSonucu.Durum, dersSonucu.Cikti, dersSonucu.Hata), dersSuresi, ct);
            return new JsonResult(dersSonucu with { GonderimId = dersGonderimId });
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Gönderim kaydedilemedi: {Id}", id);
            return StatusCode(503, new { mesaj = "Gönderim kaydedilemedi." });
        }
        catch (Exception ex) when (ex is DbException || ex is InvalidOperationException { InnerException: DbException })
        {
            logger.LogWarning(ex, "Editör için ders okunamadı: {Id}", id);
            return StatusCode(503, new { mesaj = "Derse şu anda ulaşılamıyor. Daha sonra yeniden deneyin." });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            logger.LogWarning(ex, "Editör sandbox servisine ulaşamadı: {Id}", id);
            return StatusCode(503, new { mesaj = "Kod çalıştırma servisi şu anda kullanılamıyor veya meşgul. Daha sonra yeniden deneyin." });
        }
    }

    public sealed record GorevIcerigi(int Id, int? No, string Baslik, string AciklamaHtml,
        string Tur, string BaslangicKodu, bool OtomatikKontrolVar);
}
