namespace EgitimPlatformu.Web.Models;

public sealed class Ilerleme
{
    public string KullaniciId { get; set; } = "";
    public int EgitimId { get; set; }
    public DateTime? TamamlanmaUtc { get; set; }
}
