using System.Linq.Expressions;
using EgitimPlatformu.Web.Models;

namespace EgitimPlatformu.Web.ViewModels;

public sealed record EgitimKartModel(int Id, string Kod, string Baslik, string KategoriAdi,
    string KategoriKisaltma, string KategoriAciklamasi, int GorevSayisi)
{
    public string KapakSinifi => KategoriKisaltma == "PY" ? "course-python" : "course-algorithm";

    // Ders modelinde özet/seviye/süre yok. Açıklama kategorinin gerçek açıklamasıdır.
    public static readonly Expression<Func<Egitim, EgitimKartModel>> Secim = e => new(
        e.Id, e.Kod, e.Baslik,
        e.Kategori == null ? "Kategorisiz" : e.Kategori.Ad,
        e.Kategori == null ? "" : e.Kategori.Kisaltma,
        e.Kategori == null ? "" : e.Kategori.Aciklama,
        e.Gorevler.Count);
}
