using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EgitimPlatformu.Web.Contracts;

public sealed class IcerikPaketi
{
    [JsonRequired]
    [Range(1, 2)] public int Surum { get; set; }
    [JsonRequired]
    [Required, MinLength(1), MaxLength(50)] public List<KategoriAktarimi> Kategoriler { get; set; } = [];
}

public sealed class KategoriAktarimi
{
    [JsonRequired]
    [Required, RegularExpression("[A-Z][A-Z0-9-]{0,9}")] public string Kisaltma { get; set; } = "";
    [JsonRequired]
    [Required, StringLength(100)] public string Ad { get; set; } = "";
    [JsonRequired]
    [Required(AllowEmptyStrings = true), StringLength(500)] public string Aciklama { get; set; } = "";
    [JsonRequired]
    [Range(0, int.MaxValue)] public int Sira { get; set; }
    [JsonRequired]
    [Required, MaxLength(500)] public List<EgitimAktarimi> Egitimler { get; set; } = [];
}

public sealed class EgitimAktarimi
{
    [JsonRequired]
    [Required, RegularExpression("[A-Z][A-Z0-9-]{0,19}")] public string Kod { get; set; } = "";
    [JsonRequired]
    [Required, StringLength(150)] public string Baslik { get; set; } = "";
    [JsonRequired]
    [Range(0, int.MaxValue)] public int Sira { get; set; }
    [JsonRequired]
    [Required(AllowEmptyStrings = true), StringLength(200000)] public string IcerikMarkdown { get; set; } = "";
    [JsonRequired]
    [Required(AllowEmptyStrings = true), StringLength(100000)] public string BaslangicKodu { get; set; } = "";
    [JsonRequired]
    [Required(AllowEmptyStrings = true), StringLength(100000)] public string TestKodu { get; set; } = "";
    [JsonRequired]
    [Required, MaxLength(100)] public List<GorevAktarimi> Gorevler { get; set; } = [];
}

public sealed class GorevAktarimi
{
    public string? Tur { get; set; }
    public string? BaslangicDosyasi { get; set; }
    [StringLength(100000)] public string? BaslangicKodu { get; set; }
    public GorevKontrolu? Kontrol { get; set; }

    [JsonRequired]
    [Range(1, 100)] public int No { get; set; }
    [JsonRequired]
    [Required, StringLength(200)] public string Baslik { get; set; } = "";
    [JsonRequired]
    [Required(AllowEmptyStrings = true), StringLength(50000)] public string Aciklama { get; set; } = "";
}

public sealed class GorevKontrolu
{
    [JsonRequired, Required] public string Tur { get; set; } = "";
    [JsonRequired, Required(AllowEmptyStrings = true), StringLength(64000)] public string BeklenenCikti { get; set; } = "";
    [JsonRequired, Required(AllowEmptyStrings = true), StringLength(32000)] public string Girdi { get; set; } = "";
    [JsonRequired, Range(0, 255)] public int BeklenenCikisKodu { get; set; }
    [JsonRequired, Required, MaxLength(30)] public List<string> Kullanilmamali { get; set; } = [];
}
