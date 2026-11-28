using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace EgitimPlatformu.Web.Contracts;

public sealed record NotebookOlusturIstegi(
    [Required, StringLength(150)] string Baslik,
    JsonElement Icerik,
    [Range(1, int.MaxValue)] int? EgitimId = null);

public sealed record NotebookGuncelleIstegi(
    [Required, StringLength(150)] string Baslik,
    JsonElement Icerik,
    [Range(1, int.MaxValue - 1)] int Surum);

public sealed record NotebookOzeti(int Id, string Baslik, int? EgitimId, int Surum, DateTime GuncellendiUtc);
public sealed record NotebookDetayi(int Id, string Baslik, int? EgitimId, int Surum,
    DateTime GuncellendiUtc, JsonElement Icerik);
