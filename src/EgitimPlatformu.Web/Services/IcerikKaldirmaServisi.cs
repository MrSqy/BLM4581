using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Services;

public sealed record KaldirmaSonucu(int KategoriSayisi, int EgitimSayisi, int GorevSayisi);

// Bir aktarım paketinin eklediği dersleri geri alır. Yalnız pakette adı geçen
// kategori/ders kodlarına dokunur; paket dışındaki kayıtlar değişmez.
// Görevler dersle birlikte silinir; gönderim geçmişi kalır, görev bağlantısı boşalır.
public sealed class IcerikKaldirmaServisi(UygulamaDbContext db)
{
    public async Task<KaldirmaSonucu> KaldirAsync(IcerikPaketi paket, bool kategoriDahil, CancellationToken cancellationToken)
    {
        var hata = IcerikAktarimServisi.TutarlilikHatasi(paket);
        if (hata is not null) throw new InvalidOperationException(hata);
        await using var islem = await db.Database.BeginTransactionAsync(cancellationToken);
        int kategoriSayisi = 0, dersSayisi = 0, gorevSayisi = 0;
        foreach (var kaynak in paket.Kategoriler)
        {
            var kategori = await db.Kategoriler.SingleOrDefaultAsync(k => k.Kisaltma == kaynak.Kisaltma, cancellationToken);
            var kodlar = kaynak.Egitimler.Select(e => e.Kod).ToList();
            var dersler = await db.Egitimler.Include(e => e.Gorevler)
                .Where(e => kodlar.Contains(e.Kod)).ToListAsync(cancellationToken);
            var yabanci = dersler.FirstOrDefault(e => kategori is null || e.KategoriId != kategori.Id);
            if (yabanci is not null)
                throw new InvalidOperationException($"{yabanci.Kod} başka bir kategoride; silinmedi.");
            if (kategori is null) continue;
            if (kategoriDahil && await db.Egitimler.AnyAsync(e => e.KategoriId == kategori.Id && !kodlar.Contains(e.Kod), cancellationToken))
                throw new InvalidOperationException($"{kategori.Kisaltma} kategorisinde paket dışı dersler var; kategori silinmedi.");
            gorevSayisi += dersler.Sum(e => e.Gorevler.Count);
            dersSayisi += dersler.Count;
            db.Egitimler.RemoveRange(dersler);
            if (kategoriDahil)
            {
                db.Kategoriler.Remove(kategori);
                kategoriSayisi++;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await islem.CommitAsync(cancellationToken);
        return new(kategoriSayisi, dersSayisi, gorevSayisi);
    }
}
