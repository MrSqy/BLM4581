using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace EgitimPlatformu.Web.Contracts;
public sealed class EgitimYazma
{
    [JsonRequired, Range(1, int.MaxValue)] public int KategoriId { get; set; }
    [JsonRequired, Required, RegularExpression("[A-Z][A-Z0-9-]{0,19}")] public string Kod { get; set; } = "";
    [JsonRequired, Required, StringLength(150)] public string Baslik { get; set; } = "";
    [JsonRequired, Range(0, int.MaxValue)] public int Sira { get; set; }
    [JsonRequired, Required(AllowEmptyStrings = true), StringLength(200000)] public string IcerikMarkdown { get; set; } = "";
    [JsonRequired, Required(AllowEmptyStrings = true), StringLength(100000)] public string BaslangicKodu { get; set; } = "";
    [JsonRequired, Required(AllowEmptyStrings = true), StringLength(100000)] public string TestKodu { get; set; } = "";
}
public sealed class EgitimSorgusu
{
    [Range(1, int.MaxValue)] public int? KategoriId { get; set; }
    [StringLength(100)] public string? Q { get; set; }
    [Range(1, 1000000)] public int Sayfa { get; set; } = 1;
    [Range(1, 100)] public int SayfaBoyutu { get; set; } = 20;
}
public sealed record EgitimOzet(int Id, int? KategoriId, string Kod, string Baslik, int Sira);
public sealed record GorevDetay(int Id, int? No, string Baslik, string Aciklama);
public sealed record EgitimDetay(int Id, int? KategoriId, string Kod, string Baslik, int Sira,
    string IcerikMarkdown, string BaslangicKodu, List<GorevBilgisi> Gorevler);
public sealed record EgitimSayfasi(int Toplam, int Sayfa, int SayfaBoyutu, List<EgitimOzet> Egitimler);
