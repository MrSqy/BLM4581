using EgitimPlatformu.Web.Models;

namespace EgitimPlatformu.Web.Services;

public static class NotebookErisimi
{
    public static IQueryable<Notebook> Okunabilir(this IQueryable<Notebook> sorgu, string kullanici) =>
        sorgu.Where(n => n.EgitimId.HasValue || n.KullaniciId == kullanici);

    public static IQueryable<Notebook> Duzenlenebilir(this IQueryable<Notebook> sorgu, string kullanici, bool yonetici) =>
        sorgu.Where(n => n.EgitimId.HasValue ? yonetici : n.KullaniciId == kullanici);
}
