namespace EgitimPlatformu.Web.Models;

public sealed class Notebook
{
    public int Id { get; set; }
    public string KullaniciId { get; set; } = "";
    public int? EgitimId { get; set; }
    public string Baslik { get; set; } = "";
    public string IcerikJson { get; set; } = "";
    public int Surum { get; set; } = 1;
    public DateTime GuncellendiUtc { get; set; }
}
