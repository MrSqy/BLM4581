using System.ComponentModel.DataAnnotations;

namespace EgitimPlatformu.Web.Contracts;

public sealed class IcerikPaketi
{
    [Range(1, 1)] public int Surum { get; set; }
    [Required, MinLength(1), MaxLength(50)] public List<KategoriAktarimi> Kategoriler { get; set; } = [];
}

public sealed class KategoriAktarimi
{
    [Required, RegularExpression("[A-Z][A-Z0-9-]{0,9}")] public string Kisaltma { get; set; } = "";
    [Required, StringLength(100)] public string Ad { get; set; } = "";
    [Required(AllowEmptyStrings = true), StringLength(500)] public string Aciklama { get; set; } = "";
    [Range(0, int.MaxValue)] public int Sira { get; set; }
    [Required, MaxLength(500)] public List<EgitimAktarimi> Egitimler { get; set; } = [];
}

public sealed class EgitimAktarimi
{
    [Required, RegularExpression("[A-Z][A-Z0-9-]{0,19}")] public string Kod { get; set; } = "";
    [Required, StringLength(150)] public string Baslik { get; set; } = "";
    [Range(0, int.MaxValue)] public int Sira { get; set; }
    [Required(AllowEmptyStrings = true), StringLength(200000)] public string IcerikMarkdown { get; set; } = "";
    [Required(AllowEmptyStrings = true), StringLength(100000)] public string BaslangicKodu { get; set; } = "";
    [Required(AllowEmptyStrings = true), StringLength(100000)] public string TestKodu { get; set; } = "";
    [Required, MaxLength(100)] public List<GorevAktarimi> Gorevler { get; set; } = [];
}

public sealed class GorevAktarimi
{
    [Range(1, 100)] public int No { get; set; }
    [Required, StringLength(200)] public string Baslik { get; set; } = "";
    [Required(AllowEmptyStrings = true), StringLength(50000)] public string Aciklama { get; set; } = "";
}
