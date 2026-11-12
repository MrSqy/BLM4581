using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Services;

public sealed record GonderimDegerlendirmesi(bool Basarili, string Durum, string Cikti, string Hata, int? CikisKodu = null);

public sealed class GonderimServisi(UygulamaDbContext db) : IGonderimGecmisi
{
    public bool Kullanilabilir => true;

    public async Task<int?> KaydetAsync(string? kullaniciId, Egitim ders, Gorev? gorev, string kod,
        GonderimDegerlendirmesi sonuc, TimeSpan sure, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(kullaniciId) || sonuc.Durum == "test_yok") return null;

        var kayit = new Gonderim
        {
            KullaniciId = kullaniciId,
            EgitimId = ders.Id,
            Ders = ders.Baslik,
            GorevId = gorev?.Id,
            GorevNo = gorev?.No,
            GorevBasligi = gorev?.Baslik,
            Kod = kod,
            Basarili = sonuc.Basarili,
            Durum = sonuc.Durum,
            Cikti = sonuc.Cikti,
            Hata = sonuc.Hata,
            CikisKodu = sonuc.CikisKodu,
            SureMs = (long)sure.TotalMilliseconds,
            TarihUtc = DateTime.UtcNow
        };
        db.Gonderimler.Add(kayit);
        await db.SaveChangesAsync(ct);
        return kayit.Id;
    }

    public async Task<GonderimSayfasi> ListeAsync(string kullaniciId, int sayfa, int boyut, CancellationToken ct)
    {
        var sorgu = db.Gonderimler.AsNoTracking().Where(g => g.KullaniciId == kullaniciId);
        var toplam = await sorgu.CountAsync(ct);
        var kayitlar = await sorgu.OrderByDescending(g => g.TarihUtc).ThenByDescending(g => g.Id)
            .Skip((sayfa - 1) * boyut).Take(boyut)
            .Select(g => new GonderimGorunumu(g.Id, g.KullaniciId, new DateTimeOffset(g.TarihUtc, TimeSpan.Zero),
                g.EgitimId, g.Ders, g.GorevBasligi, g.Durum, "", "", "")
            {
                GorevId = g.GorevId, GorevNo = g.GorevNo, Basarili = g.Basarili,
                CikisKodu = g.CikisKodu, SureMs = g.SureMs
            }).ToListAsync(ct);
        return new(toplam, kayitlar);
    }

    public Task<GonderimGorunumu?> DetayAsync(string kullaniciId, int id, CancellationToken ct)
        => db.Gonderimler.AsNoTracking().Where(g => g.KullaniciId == kullaniciId && g.Id == id)
            .Select(g => new GonderimGorunumu(g.Id, g.KullaniciId, new DateTimeOffset(g.TarihUtc, TimeSpan.Zero),
                g.EgitimId, g.Ders, g.GorevBasligi, g.Durum, g.Kod, g.Cikti, g.Hata)
            {
                GorevId = g.GorevId, GorevNo = g.GorevNo, Basarili = g.Basarili,
                CikisKodu = g.CikisKodu, SureMs = g.SureMs
            }).SingleOrDefaultAsync(ct);
}
