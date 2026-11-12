using EgitimPlatformu.Web.Services;

namespace EgitimPlatformu.Web.Contracts;

public sealed record GonderimOzeti(int Id, DateTimeOffset Tarih, int EgitimId, string Ders,
    int? GorevId, int? GorevNo, string? Gorev, string Durum, bool Basarili, int? CikisKodu, long SureMs)
{
    public static GonderimOzeti Olustur(GonderimGorunumu g) =>
        new(g.Id, g.Tarih, g.EgitimId, g.Ders, g.GorevId, g.GorevNo, g.Gorev,
            g.Durum, g.Basarili, g.CikisKodu, g.SureMs);
}

public sealed record GonderimListeYaniti(int Toplam, int Sayfa, int SayfaBoyutu, IReadOnlyList<GonderimOzeti> Kayitlar);
public sealed record GonderimDetayYaniti(GonderimOzeti Gonderim, string Kod, string Cikti, string Hata);
