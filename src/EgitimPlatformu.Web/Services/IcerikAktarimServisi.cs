using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Services;

public sealed record AktarimSonucu(int KategoriSayisi, int EgitimSayisi, int GorevSayisi);

public sealed class IcerikAktarimServisi(UygulamaDbContext db)
{
    public static string? TutarlilikHatasi(IcerikPaketi paket)
    {
        if (paket.Kategoriler.Any(k => k is null || k.Egitimler is null || k.Egitimler.Any(e => e is null || e.Gorevler is null || e.Gorevler.Any(g => g is null))))
            return "Kategori, ders ve görev listelerinde null öğe bulunamaz.";
        if (paket.Kategoriler.Select(k => k.Kisaltma).Distinct().Count() != paket.Kategoriler.Count)
            return "Pakette aynı kategori kısaltması birden fazla kez bulunamaz.";
        var dersler = paket.Kategoriler.SelectMany(k => k.Egitimler).ToList();
        if (dersler.Select(e => e.Kod).Distinct().Count() != dersler.Count)
            return "Pakette aynı ders kodu birden fazla kez bulunamaz.";
        if (dersler.Any(e => e.Gorevler.Select(g => g.No).Distinct().Count() != e.Gorevler.Count))
            return "Bir derste aynı görev numarası birden fazla kez bulunamaz.";
        return null;
    }

    public async Task<AktarimSonucu> AktarAsync(IcerikPaketi paket, CancellationToken cancellationToken)
    {
        await using var islem = await db.Database.BeginTransactionAsync(cancellationToken);
        var dersSayisi = 0;
        var gorevSayisi = 0;
        foreach (var kaynakKategori in paket.Kategoriler)
        {
            var kategori = await db.Kategoriler.SingleOrDefaultAsync(k => k.Kisaltma == kaynakKategori.Kisaltma, cancellationToken);
            if (kategori is null)
            {
                kategori = new Kategori { Kisaltma = kaynakKategori.Kisaltma };
                db.Kategoriler.Add(kategori);
            }
            kategori.Ad = kaynakKategori.Ad;
            kategori.Aciklama = kaynakKategori.Aciklama;
            kategori.Sira = kaynakKategori.Sira;
            foreach (var kaynakDers in kaynakKategori.Egitimler)
            {
                var ders = await db.Egitimler.Include(e => e.Gorevler).SingleOrDefaultAsync(e => e.Kod == kaynakDers.Kod, cancellationToken);
                if (ders is null)
                {
                    ders = new Egitim { Kod = kaynakDers.Kod };
                    db.Egitimler.Add(ders);
                }
                ders.Kategori = kategori;
                ders.Baslik = kaynakDers.Baslik;
                ders.Sira = kaynakDers.Sira;
                ders.IcerikMarkdown = kaynakDers.IcerikMarkdown;
                ders.BaslangicKodu = kaynakDers.BaslangicKodu;
                ders.TestKodu = kaynakDers.TestKodu;
                foreach (var kaynakGorev in kaynakDers.Gorevler)
                {
                    var gorev = ders.Gorevler.SingleOrDefault(g => g.No == kaynakGorev.No);
                    if (gorev is null)
                    {
                        gorev = new Gorev { No = kaynakGorev.No };
                        ders.Gorevler.Add(gorev);
                    }
                    gorev.Baslik = kaynakGorev.Baslik;
                    gorev.Aciklama = kaynakGorev.Aciklama;
                    gorevSayisi++;
                }
                dersSayisi++;
            }
        }
        // Eksik bırakılan kayıtlar silinmez; kod/numara eşleşmeleri kimlikleri korur.
        await db.SaveChangesAsync(cancellationToken);
        await islem.CommitAsync(cancellationToken);
        return new(paket.Kategoriler.Count, dersSayisi, gorevSayisi);
    }
}
