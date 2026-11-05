using System.Text.Json;
using System.Text.RegularExpressions;
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
        if (paket.Surum is not (1 or 2)) return "Paket sürümü 1 veya 2 olmalı.";
        if (paket.Kategoriler.Any(k => k is null || k.Egitimler is null || k.Egitimler.Any(e => e is null || e.Gorevler is null || e.Gorevler.Any(g => g is null))))
            return "Kategori, ders ve görev listelerinde null öğe bulunamaz.";
        if (paket.Kategoriler.Select(k => k.Kisaltma).Distinct().Count() != paket.Kategoriler.Count)
            return "Pakette aynı kategori kısaltması birden fazla kez bulunamaz.";
        var dersler = paket.Kategoriler.SelectMany(k => k.Egitimler).ToList();
        if (dersler.Select(e => e.Kod).Distinct().Count() != dersler.Count)
            return "Pakette aynı ders kodu birden fazla kez bulunamaz.";
        if (dersler.Any(e => e.Gorevler.Select(g => g.No).Distinct().Count() != e.Gorevler.Count))
            return "Bir derste aynı görev numarası birden fazla kez bulunamaz.";
        foreach (var g in dersler.SelectMany(e => e.Gorevler))
        {
            if (paket.Surum == 1 && (g.Tur is not null || g.BaslangicKodu is not null || g.BaslangicDosyasi is not null || g.Kontrol is not null))
                return "Görev tabanlı alanlar için surum:2 kullanın.";
            if (paket.Surum != 2) continue;
            if (g.Tur is not ("kod" or "aciklama")) return "V2 görev türü kod veya aciklama olmalı.";
            if (g.BaslangicKodu is null) return "V2 baslangicKodu alanı zorunlu.";
            if (g.Tur == "aciklama" && (g.Kontrol is not null || g.BaslangicDosyasi is not null || g.BaslangicKodu != ""))
                return "Açıklama görevinin dosyası/kodu/kontrolü olamaz.";
            if (g.Tur == "kod" && (g.Kontrol is null || g.Kontrol.Tur != "standart-cikti" ||
                !Regex.IsMatch(g.BaslangicDosyasi ?? "", @"\Agorevler/[a-zA-Z0-9_-]+\.py\z")))
                return "Kod görevi güvenli başlangıç dosyası ve standart-cikti kontrolü gerektirir.";
            if (g.Kontrol?.Kullanilmamali is { } yasak && yasak.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100 || x.Contains('\n')))
                return "Kullanılmamalı kuralları boş olmayan en fazla 100 karakterlik metinler olmalı.";
        }
        if (paket.Surum == 2 && dersler.Any(e => e.Gorevler.Where(g => g.Tur == "kod").Select(g => g.BaslangicDosyasi).Distinct().Count() != e.Gorevler.Count(g => g.Tur == "kod")))
            return "Bir derste başlangıç dosyaları benzersiz olmalı.";
        return null;
    }

    public async Task<AktarimSonucu> AktarAsync(IcerikPaketi paket, CancellationToken cancellationToken)
    {
        var hata = TutarlilikHatasi(paket);
        if (hata is not null) throw new ArgumentException(hata);
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
                if (paket.Surum == 1 && ders.Gorevler.Any(g => g.Tur != "eski-pytest"))
                    throw new ArgumentException("V2 ders v1 paketle değiştirilemez.");
                ders.Kategori = kategori;
                ders.Baslik = kaynakDers.Baslik;
                ders.Sira = kaynakDers.Sira;
                ders.IcerikMarkdown = kaynakDers.IcerikMarkdown;
                ders.BaslangicKodu = kaynakDers.BaslangicKodu;
                ders.TestKodu = kaynakDers.TestKodu;
                if (paket.Surum == 2)
                    foreach (var eski in ders.Gorevler.Where(g => !kaynakDers.Gorevler.Any(k => k.No == g.No))) eski.Aktif = false;
                foreach (var kaynakGorev in kaynakDers.Gorevler)
                {
                    var gorev = ders.Gorevler.SingleOrDefault(g => g.No == kaynakGorev.No);
                    if (gorev is null)
                    {
                        gorev = new Gorev { No = kaynakGorev.No };
                        ders.Gorevler.Add(gorev);
                    }
                    gorev.Aktif = true;
                    if (paket.Surum == 2)
                    {
                        gorev.Tur = kaynakGorev.Tur!;
                        gorev.BaslangicDosyasi = kaynakGorev.BaslangicDosyasi;
                        gorev.BaslangicKodu = kaynakGorev.BaslangicKodu!;
                        gorev.KontrolJson = kaynakGorev.Kontrol is null ? null : JsonSerializer.Serialize(kaynakGorev.Kontrol);
                    }
                    gorev.Baslik = kaynakGorev.Baslik;
                    gorev.Aciklama = kaynakGorev.Aciklama;
                    gorevSayisi++;
                }
                dersSayisi++;
            }
        }
        // V1 eksikleri korur; v2 listedeki dersin eksik görevlerini pasifleştirir. Hiçbir görev silinmez.
        await db.SaveChangesAsync(cancellationToken);
        await islem.CommitAsync(cancellationToken);
        return new(paket.Kategoriler.Count, dersSayisi, gorevSayisi);
    }
}
