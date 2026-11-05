using System.ComponentModel.DataAnnotations;

namespace EgitimPlatformu.Web.Models;

// Görev türü: eski-pytest (v1), kod veya aciklama (v2).
public class Gorev
{
    public int Id { get; set; }
    public bool Aktif { get; set; } = true;
    public string Tur { get; set; } = "eski-pytest";
    public string? BaslangicDosyasi { get; set; }
    public string BaslangicKodu { get; set; } = "";
    public string? KontrolJson { get; set; }


    [Required(ErrorMessage = "Ders seçiniz.")]
    [Display(Name = "Ders")]
    public int? EgitimId { get; set; }

    public Egitim? Egitim { get; set; }

    [Required(ErrorMessage = "Görev numarası zorunludur.")]
    [Range(1, 100, ErrorMessage = "Görev numarası 1-100 olmalı.")]
    [Display(Name = "Görev No")]
    public int? No { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur.")]
    [StringLength(200, ErrorMessage = "En fazla 200 karakter.")]
    [Display(Name = "Başlık")]
    public string Baslik { get; set; } = "";

    [Display(Name = "Açıklama")]
    public string Aciklama { get; set; } = "";
}
