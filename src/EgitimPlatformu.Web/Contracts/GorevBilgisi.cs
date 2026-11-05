using System.Text.Json;
using EgitimPlatformu.Web.Models;
namespace EgitimPlatformu.Web.Contracts;

public sealed record GorevBilgisi(int Id, int? EgitimId, int? No, string Baslik, string Aciklama,
    string Tur, string? BaslangicDosyasi, string BaslangicKodu, GorevKontrolu? Kontrol)
{
    public static GorevBilgisi Olustur(Gorev g) => new(g.Id, g.EgitimId, g.No, g.Baslik, g.Aciklama,
        g.Tur, g.BaslangicDosyasi, g.BaslangicKodu,
        g.KontrolJson is null ? null : JsonSerializer.Deserialize<GorevKontrolu>(g.KontrolJson));
}
