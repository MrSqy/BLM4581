namespace EgitimPlatformu.Web.Models;

public sealed class Gonderim
{
    public int Id { get; set; }
    public string KullaniciId { get; set; } = "";
    // Eğitim kimliği ve başlıklar değerlendirme anındaki değerlerdir.
    public int EgitimId { get; set; }
    public string Ders { get; set; } = "";
    public int? GorevId { get; set; }
    public int? GorevNo { get; set; }
    public string? GorevBasligi { get; set; }
    public string Kod { get; set; } = "";
    public string Cikti { get; set; } = "";
    public string Hata { get; set; } = "";
    public string Durum { get; set; } = "";
    public bool Basarili { get; set; }
    public int? CikisKodu { get; set; }
    public long SureMs { get; set; }
    public DateTime TarihUtc { get; set; }
}
