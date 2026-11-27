using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Pages.Yonetim;

public class IstatistiklerModel(UygulamaDbContext db, ILogger<IstatistiklerModel> logger) : YonetimPageModel(db)
{
    public bool Hazir { get; private set; }
    public int Toplam { get; private set; }
    public int Basarili { get; private set; }
    public int Basarisiz => Toplam - Basarili;
    public int Katilimci { get; private set; }
    public decimal? BasariYuzdesi => Toplam == 0 ? null : Math.Round(100m * Basarili / Toplam, 1);
    public IReadOnlyList<DersIstatistigi> Dersler { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            // Aynı veritabanı anlık görüntüsü, eşzamanlı yeni gönderimlerde tutarlı sayımlar sağlar.
            await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var sayim = await Db.Gonderimler.AsNoTracking().GroupBy(g => 1)
                .Select(g => new { Toplam = g.Count(), Basarili = g.Count(x => x.Basarili), Katilimci = g.Select(x => x.KullaniciId).Distinct().Count() })
                .SingleOrDefaultAsync(ct);
            Toplam = sayim?.Toplam ?? 0; Basarili = sayim?.Basarili ?? 0; Katilimci = sayim?.Katilimci ?? 0;
            // Başlıklar gönderim anındaki kopyadır; ders başlığı değişmiş olsa da aynı eğitim kimliğinde gruplanır.
            var dersler = await Db.Gonderimler.AsNoTracking().GroupBy(g => g.EgitimId)
                .Select(g => new { EgitimId = g.Key,
                    Ders = g.OrderByDescending(x => x.TarihUtc).ThenByDescending(x => x.Id).Select(x => x.Ders).First(),
                    Toplam = g.Count(), Basarili = g.Count(x => x.Basarili) })
                .OrderByDescending(g => g.Toplam).ThenBy(g => g.EgitimId).ToListAsync(ct);
            Dersler = dersler.Select(g => new DersIstatistigi(g.EgitimId, g.Ders, g.Toplam, g.Basarili)).ToArray();
            await transaction.CommitAsync(ct);
            Hazir = true;
        }
        catch (Exception ex) when (GonderimGecmisiHatasi.VeritabaniHatasiMi(ex))
        {
            logger.LogWarning(ex, "Gönderim istatistikleri okunamadı.");
            Response.StatusCode = 503;
        }
        return Page();
    }

    public sealed record DersIstatistigi(int EgitimId, string Ders, int Toplam, int Basarili);
}
