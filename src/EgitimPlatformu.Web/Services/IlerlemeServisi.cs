using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Services;

public sealed record DersIlerlemesi(int EgitimId, string Kod, string Baslik, int Gecilen, int Toplam,
    DateTime? TamamlanmaUtc, bool ElleTamamlanabilir)
{
    public bool Tamamlandi => TamamlanmaUtc.HasValue;
    public int Yuzde => Gecilen * 100 / Toplam;
}

public enum TamamlamaSonucu { Tamamlandi, Bulunamadi, TestGerekli }

public sealed class IlerlemeServisi(UygulamaDbContext db)
{
    public async Task<List<DersIlerlemesi>> ListeAsync(string kullaniciId, CancellationToken ct, int? egitimId = null)
    {
        var dersler = db.Egitimler.AsNoTracking();
        if (egitimId.HasValue) dersler = dersler.Where(e => e.Id == egitimId);
        var sonuclar = await dersler.OrderBy(e => e.KategoriId).ThenBy(e => e.Sira).ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id, e.Kod, e.Baslik,
                GorevBazli = e.Gorevler.Any(g => g.Aktif && g.Tur != "eski-pytest"),
                EskiTestVar = e.TestKodu.Trim() != "",
                Toplam = e.Gorevler.Count(g => g.Aktif && g.Tur == "kod" && g.KontrolJson != null),
                Gecilen = e.Gorevler.Count(g => g.Aktif && g.Tur == "kod" && g.KontrolJson != null &&
                    db.Gonderimler.Any(s => s.KullaniciId == kullaniciId && s.EgitimId == e.Id &&
                        s.GorevId == g.Id && s.Basarili && s.Durum == "basarili")),
                Tamamlanma = db.Ilerlemeler.Where(i => i.KullaniciId == kullaniciId && i.EgitimId == e.Id)
                    .Select(i => i.TamamlanmaUtc).SingleOrDefault()
            }).ToListAsync(ct);
        return sonuclar.Select(s =>
        {
            var toplam = Math.Max(1, s.Toplam);
            var elle = s.GorevBazli ? s.Toplam == 0 : !s.EskiTestVar;
            return new DersIlerlemesi(s.Id, s.Kod, s.Baslik, s.Tamamlanma.HasValue ? toplam : s.Gecilen,
                toplam, s.Tamamlanma, elle && !s.Tamamlanma.HasValue);
        }).ToList();
    }

    public async Task<TamamlamaSonucu> ElleTamamlaAsync(string kullaniciId, int egitimId, CancellationToken ct)
    {
        var ders = await db.Egitimler.AsNoTracking().Include(e => e.Gorevler)
            .SingleOrDefaultAsync(e => e.Id == egitimId, ct);
        if (ders is null) return TamamlamaSonucu.Bulunamadi;
        if (!OkumaDersi(ders)) return TamamlamaSonucu.TestGerekli;
        var tarih = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Ilerlemeler" ("KullaniciId", "EgitimId", "TamamlanmaUtc")
            VALUES ({kullaniciId}, {egitimId}, {tarih})
            ON CONFLICT ("KullaniciId", "EgitimId") DO UPDATE
                SET "TamamlanmaUtc" = COALESCE("Ilerlemeler"."TamamlanmaUtc", EXCLUDED."TamamlanmaUtc")
            """, ct);
        return TamamlamaSonucu.Tamamlandi;
    }

    public static bool OkumaDersi(Egitim ders) =>
        ders.Gorevler.Any(g => g.Aktif && g.Tur != "eski-pytest")
            ? !ders.Gorevler.Any(TestEdilebilir)
            : string.IsNullOrWhiteSpace(ders.TestKodu);

    private static bool TestEdilebilir(Gorev gorev) =>
        gorev.Aktif && gorev.Tur == "kod" && gorev.KontrolJson != null;

    internal static bool IlerlemeyeUygun(Egitim ders, Gorev? gorev, GonderimDegerlendirmesi sonuc) =>
        sonuc.Basarili && (gorev is null
            ? !sonuc.AtlananTestVar && !string.IsNullOrWhiteSpace(ders.TestKodu) && sonuc.Durum == "tamamlandi"
            : TestEdilebilir(gorev) && sonuc.Durum == "basarili");

    // Gönderimle aynı transaction içinde kullanıcı/ders satırını kilitler.
    // İki görev aynı anda geçilirse ikinci transaction ilk gönderimi de görür.
    internal Task KilitleAsync(string kullaniciId, int egitimId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Ilerlemeler" ("KullaniciId", "EgitimId") VALUES ({kullaniciId}, {egitimId})
            ON CONFLICT ("KullaniciId", "EgitimId") DO UPDATE
                SET "TamamlanmaUtc" = "Ilerlemeler"."TamamlanmaUtc"
            """, ct);

    internal async Task TesttenTamamlaAsync(Gonderim kayit, CancellationToken ct)
    {
        if (kayit.GorevId.HasValue && await db.Gorevler.AnyAsync(g =>
            g.EgitimId == kayit.EgitimId && g.Aktif && g.Tur == "kod" && g.KontrolJson != null &&
            !db.Gonderimler.Any(s => s.KullaniciId == kayit.KullaniciId && s.EgitimId == kayit.EgitimId &&
                s.GorevId == g.Id && s.Basarili && s.Durum == "basarili"), ct)) return;

        await db.Ilerlemeler.Where(i => i.KullaniciId == kayit.KullaniciId &&
            i.EgitimId == kayit.EgitimId && i.TamamlanmaUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.TamamlanmaUtc, kayit.TarihUtc), ct);
    }
}
